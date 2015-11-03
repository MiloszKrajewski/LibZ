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
    [ "lib"; "lib/net35"; "lib/net40"; "tool"; "dist" ]
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
        Program = temp @@ "libz.exe";
        WorkingDirectory = "./../out/tool";
        CommandLine = " inject-dll -a libz.exe -i *.dll --move " }
    |> shellExec |> ignore
    
    let zipName suffix = sprintf "libz-%s-%s.zip" releaseNotes.AssemblyVersion suffix
    let zipDir suffix dirName =
        !! ("./../out" @@ dirName @@ "**/*")
        |> Zip ("./../out" @@ dirName) ("./../out" @@ (zipName suffix))
    "lib" |> zipDir "lib"
    "tool" |> zipDir "tool"
)

!!!!!
Target "Nuget" (fun _ ->
    let apiKey = getSecret "nuget" None
    let libDir spec = spec |> sprintf @"lib\%s" |> Some
    NuGet (fun p ->
        { p with
            Version = releaseNotes.AssemblyVersion
            WorkingDir = @"../out/release"
            OutputPath = @"../out/release"
            ReleaseNotes = releaseNotes.Notes |> toLines
            References = [@"LZ4.dll"]
            AccessKey = apiKey
            Files =
                [
                    ("net2\\*.dll", libDir "net2", None)
                    ("net4\\*.dll", libDir "net4-client", None)
                    ("portable\\*.dll", libDir portableSpec, None)
                    ("silverlight\\*.dll", libDir silverlightSpec, None)
                ]
        }
    ) "lz4net.nuspec"
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

RunTargetOrDefault "Build"
