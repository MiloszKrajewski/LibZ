#r "packages/FAKE/tools/FakeLib.dll"

open System
open System.IO
open System.Text.RegularExpressions
open Fake
open Fake.ConfigurationHelper
open Fake.ReleaseNotesHelper
open Fake.StrongNamingHelper

let outDir = "./../out"
let testDir = outDir @@ "test"
let buildDir = outDir @@ "build"
let releaseDir = outDir @@ "release"
let strongFile = "../LibZ.snk"
let secretFile = "../passwords.user"

let releaseNotes = "../CHANGES.md" |> LoadReleaseNotes

let getSecret key defaultValue =
    let result =
        match TestFile secretFile with
        | false -> defaultValue
        | _ ->
            try
                let xml = readConfig secretFile
                let xpath = sprintf "/secret/%s" key
                let node = xml.SelectSingleNode(xpath)
                node.InnerText |> Some
            with _ -> defaultValue
    match result with
    | None -> failwithf "Secret value '%s' is required" key
    | Some x -> x

let assemblyVersionRxDef =
    [
        """(?<=^\s*\[assembly:\s*AssemblyVersion(Attribute)?\(")[0-9]+(\.([0-9]+|\*)){1,3}(?="\))""", false, false
        """(?<=^\s*PRODUCTVERSION\s+)[0-9]+(\,([0-9]+|\*)){1,3}(?=\s*$)""", false, true
        """(?<=^\s*VALUE\s+"ProductVersion",\s*")[0-9]+(\.([0-9]+|\*)){1,3}(?="\s*$)""", false, false
        """(?<=^\s*\[assembly:\s*AssemblyFileVersion(Attribute)?\(")[0-9]+(\.([0-9]+|\*)){1,3}(?="\))""", true, false
        """(?<=^\s*FILEVERSION\s+)[0-9]+(\,([0-9]+|\*)){1,3}(?=\s*$)""", true, true
        """(?<=^\s*VALUE\s+"FileVersion",\s*")[0-9]+(\.([0-9]+|\*)){1,3}(?="\s*$)""", true, false
    ] |> List.map (fun (rx, p, c) -> (Regex(rx, RegexOptions.Multiline), p, c))

let updateVersionInfo productVersion (version: string) fileName =
    let fixVersion commas =
        match commas with | true -> version.Replace(".", ",") | _ -> version

    let allRx =
        assemblyVersionRxDef
        |> Seq.filter (fun (_, p, _) -> p || productVersion)
        |> Seq.map (fun (rx, _, c) -> (rx, c))
        |> List.ofSeq

    let source = File.ReadAllText(fileName)
    let replace s (rx: Regex, c) = rx.Replace(s, fixVersion c)
    let target = allRx |> Seq.fold replace source
    if source <> target then
        trace (sprintf "Updating: %s" fileName)
        File.WriteAllText(fileName, target)

Target "KeyGen" (fun _ ->
    match TestFile strongFile with
    | true -> ()
    | _ -> strongFile |> sprintf "-k %s" |> StrongName id
)

Target "Clean" (fun _ ->
    !! "**/bin" ++ "**/obj" |> CleanDirs
    "./../out" |> DeleteDir
)

Target "Build" (fun _ ->
    let build sln =
        sln
        |> MSBuildRelease null "Build"
        |> Log "Build-Output: "

    !! "*.sln" |> build
)

Target "Version" (fun _ ->
    !! "**/Properties/AssemblyInfo.cs"
    |> Seq.iter (updateVersionInfo false releaseNotes.AssemblyVersion)

    !! "**/Properties/AssemblyInfo.cs"
    -- "LibZ.Tool.Interfaces/Properties/AssemblyInfo.cs"
    |> Seq.iter (updateVersionInfo true releaseNotes.AssemblyVersion)
)

Target "Release" (fun _ ->
    [ ""; "lib"; "lib/net35"; "lib/net40"; "tool" ]
    |> Seq.map (sprintf "./../out/%s")
    |> CleanDirs

    !! "./libz/bin/Release/*.exe"
    ++ "./libz/bin/Release/*.dll"
    -- "**/*.vshost.*"
    |> Copy "./../out/tool"

    !! "./../out/tool/LibZ.Tool.Interfaces.dll"
    |> Copy "./../out/lib"

    [ "./LibZ.Bootstrap.35/LibZResolver.header.cs"; "./LibZ.Bootstrap.40/LibZResolver.cs" ]
    |> AppendTextFiles "./../out/lib/net35/LibZResolver.cs"

    !! "./LibZ.Bootstrap.40/LibZResolver.cs" |> Copy "./../out/lib/net40"
    !! "./LibZ.Bootstrap.35/bin/Release/LibZ.Bootstrap.dll" |> Copy "./../out/lib/net35"
    !! "./LibZ.Bootstrap.40/bin/Release/LibZ.Bootstrap.dll" |> Copy "./../out/lib/net40"

    let temp = (environVarOrDefault "TEMP" "./../out/temp") @@ "libz"
    !! "./../out/tool/**/*" |> Copy temp

    { defaultParams with
        Program = temp @@ "libz.exe"
        WorkingDirectory = "./../out/tool"
        CommandLine = " inject-dll -a libz.exe -i *.dll --move " }
    |> shellExec |> ignore

    let zipName suffix = sprintf "libz-%s-%s.zip" releaseNotes.AssemblyVersion suffix
    let zipDir suffix dirName =
        !! ("./../out" @@ dirName @@ "**/*")
        |> Zip ("./../out" @@ dirName) ("./../out" @@ (zipName suffix))
    "lib" |> zipDir "lib"
    "tool" |> zipDir "tool"
)

Target "NuGet" (fun _ ->
    let apiKey = getSecret "nuget" None

    let composeNuSpec suffix items =
        let mode m = items |> Seq.exists (fun c -> c = m)
        let cond m v = if mode m then Some v else None

        let componentList =
            items
            |> Seq.map (fun i ->
                match i with
                | 't' -> Some "command-line tool"
                | 'l' -> Some "precompiled bootstrapper assembly"
                | 's' -> Some "embeddable bootstrapper source in C#"
                | _ -> None)
            |> Seq.choose id
            |> (fun list -> System.String.Join(", ", list))

        let deprecationTemplate = 
            "THIS PACKAGE IS DEPRECATED. Use LibZ.Tool (most likely) and optionally LibZ.Library or LibZ.Source instead."

        let descriptionTemplate =
            sprintf ("\
                LibZ is an alternative to ILMerge. \
                It allows to distribute your applications or libraries as single file \
                with assemblies embedded into it or combined together into container file. \
                This package contains: %s. Please refer to project homepage if unsure which packages you need.")

        let description = 
            if mode 'd' then deprecationTemplate else componentList |> descriptionTemplate

        NuGet (fun p ->
            { p with
                Project = sprintf "LibZ.%s" suffix
                Title = sprintf "LibZ.%s%s" suffix (if mode 'd' then " (DEPRECATED)" else "")
                AccessKey = apiKey
                Description = description
                Version = releaseNotes.AssemblyVersion
                WorkingDir = @"../out"
                OutputPath = @"../out"
                ReleaseNotes = releaseNotes.Notes |> toLines
                References = [@"LibZ.Bootstrap.dll" |> cond 'l'] |> List.choose id
                FrameworkAssemblies = 
                    [
                        { AssemblyName = "System.ComponentModel.Composition"; FrameworkVersions = ["net4"] } |> cond 's'
                    ] |> List.choose id
                Files = 
                    [
                        (@"lib\net35\*.cs", Some @"content\net35", None) |> cond 's'
                        (@"lib\net40\*.cs", Some @"content\net4-client", None) |> cond 's' 
                        (@"lib\net35\*.dll", Some @"lib\net35", None) |> cond 'l' 
                        (@"lib\net40\*.dll", Some @"lib\net4-client", None) |> cond 'l' 
                        (@"tool\libz.exe", Some @"tools\", None) |> cond 't' 
                    ] |> List.choose id
            }
        ) "LibZ.nuspec"

    composeNuSpec "Tool" "t"
    composeNuSpec "Source" "s"
    composeNuSpec "Library" "l"
    composeNuSpec "Bootstrap" "tsld"
)

Target "TestApps" (fun _ ->
    let build sln platform =
        !! (sprintf "Tests/%s/*.sln" sln)
        |> MSBuildReleaseExt null [ ("Platform", platform) ] "Build"
        |> Log (sprintf "Build-%s-%s-Output: " sln platform)

    build "TestApp20" "x86"
    build "TestApp35" "x86"
    build "TestApp40" "x86"
    build "TestApp40" "x64"
    build "TestApp45" "x86"
    build "TestApp45" "x64"

    // test-injection-asmz "20" "x86"
    // test-injection-libz "20" "x86"
    // test-injection-asmz "35" "x86"
    // test-injection-libz "35" "x86"
    // test-injection-asmz "40" "x86"
    // test-injection-asmz "40" "x64"
    // test-injection-libz "40" "x86"
    // test-injection-libz "40" "x64"
    // test-injection-asmz "45" "x86"
    // test-injection-asmz "45" "x64"
    // test-injection-libz "45" "x86"
    // test-injection-libz "45" "x64"
)

Target "Dist" ignore

"KeyGen" ==> "Build"
"Version" ==> "Build"
"Build" ==> "TestApps"
"Build" ==> "Release"
"Release" ==> "NuGet"

RunTargetOrDefault "Build"
