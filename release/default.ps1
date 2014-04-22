Properties {
	$release = "1.1.0.1"
	$src = (get-item "..\").fullname
	$sln = "$src\LibZ.sln"
	$snk = "$src\LibZ.snk"
	$zip = "7za.exe"
}

Include ".\common.ps1"
Include ".\testing.ps1"

FormatTaskName (("-"*79) + "`n`n    {0}`n`n" + ("-"*79))

Task default -depends Release

Task Dist -depends Release {
	copy-item tool\* c:\bin\
}

Task Release -depends Rebuild {
	Create-Folder lib
	Create-Folder lib\net35
	Create-Folder lib\net40
	Create-Folder tool
	Create-Folder temp
	Create-Folder temp\lz4
	Create-Folder temp\doboz
	Create-Folder dist
	
	copy-item "$src\libz\bin\Release\*.exe" tool\
	copy-item "$src\libz\bin\Release\*.dll" tool\
	copy-item tool\LibZ.Tool.Interfaces.dll lib\
	get-content "$src\LibZ.Bootstrap.35\LibZResolver.header.cs","$src\LibZ.Bootstrap.40\LibZResolver.cs" `
		| out-file "lib\net35\LibZResolver.cs"
	copy-item "$src\LibZ.Bootstrap.40\LibZResolver.cs" lib\net40
	copy-item "$src\LibZ.Bootstrap.35\bin\Release\LibZ.Bootstrap.dll" lib\net35
	copy-item "$src\LibZ.Bootstrap.40\bin\Release\LibZ.Bootstrap.dll" lib\net40
	copy-item tool\* temp\
	copy-item "$src\LibZ.Codec.LZ4\bin\Release\*.dll" temp\lz4\
	copy-item "$src\LibZ.Codec.Doboz\bin\Release\*.dll" temp\doboz\
	
	exec { cmd /c temp\libz.exe inject-dll -a tool\libz.exe -i tool\*.dll -i tool\ILMerge.exe "--move" }
	exec { cmd /c temp\libz.exe add --libz tool\lz4.libzcodec -i temp\lz4\*.dll "--codec" deflate "--move" }
	exec { cmd /c temp\libz.exe add --libz tool\doboz.libzcodec -i temp\doboz\*.dll "--codec" deflate "--move" }
	
	exec { cmd /c $zip a -tzip "dist\libz-$release-lib.zip" "lib\" }
	exec { cmd /c $zip a -tzip "dist\libz-$release-tool.zip" "tool\" }
	
	Remove-Folder temp
}

Task Version {
	Update-AssemblyVersion $src $release 'Tests','LibZ.Tool.Interfaces'
}

Task Rebuild -depends VsVars,Clean,KeyGen,Version {
	Build-Solution $sln "Any CPU"
}

Task KeyGen -depends VsVars -precondition { return !(test-path $snk) } {
	exec { cmd /c sn -k $snk }
}

Task Clean {
	Clean-BinObj $src
	remove-item * -recurse -force -include lib,tool,temp
}

Task VsVars {
	Set-VsVars
}

Task Test {
	$libz = "$src\libz\bin\Release\libz.exe"
	if (-not (test-path $libz)) {
		Build-Solution $sln "Any CPU"
	}
	
	Build-Solution "$src\Tests\TestApp20\TestApp.sln" "x86"
	Build-Solution "$src\Tests\TestApp35\TestApp.sln" "x86"
	Build-Solution "$src\Tests\TestApp40\TestApp.sln" "x86"
	Build-Solution "$src\Tests\TestApp40\TestApp.sln" "x64"
	create-folder temp
	test-injection-asmz "20" "x86"
	test-injection-libz "20" "x86"
	test-injection-asmz "35" "x86"
	test-injection-libz "35" "x86"
	test-injection-asmz "40" "x86"
	test-injection-asmz "40" "x64"
	test-injection-libz "40" "x86"
	test-injection-libz "40" "x64"
}
