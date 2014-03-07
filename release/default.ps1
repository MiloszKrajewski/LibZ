Properties {
	$release = "1.1.0.0"
	$src = "..\"
	$sln = "$src\LibZ.sln"
	$snk = "$src\LibZ.snk"
	$mcpp = "$src\release\mcpp\mcpp.exe"
}

Include ".\common.ps1"

FormatTaskName (("-"*79) + "`n`n    {0}`n`n" + ("-"*79))

Task default -depends Release

Task Dist -depends Release {
	copy-item tool\* d:\bin
}

Task Release -depends Rebuild {
	Create-Folder lib
	Create-Folder lib\net35
	Create-Folder lib\net40
	Create-Folder tool
	Create-Folder temp
	Create-Folder temp\lz4
	Create-Folder temp\doboz
	
	copy-item "$src\libz\bin\Release\*.exe" tool\
	copy-item "$src\libz\bin\Release\*.dll" tool\
	copy-item tool\LibZ.Tool.Interfaces.dll lib\
	copy-item "$src\LibZ.Bootstrap.40\LibZResolver.cs" lib
	copy-item "$src\LibZ.Bootstrap.35\bin\Release\LibZ.Bootstrap.dll" lib\net35
	copy-item "$src\LibZ.Bootstrap.40\bin\Release\LibZ.Bootstrap.dll" lib\net40
	copy-item tool\* temp\
	copy-item "$src\LibZ.Codec.LZ4\bin\Release\*.dll" temp\lz4\
	copy-item "$src\LibZ.Codec.Doboz\bin\Release\*.dll" temp\doboz\
	
	exec { cmd /c temp\libz.exe inject-dll -a tool\libz.exe -i tool\*.dll -i tool\ILMerge.exe "--move" }
	exec { cmd /c temp\libz.exe add --libz tool\lz4.libzcodec -i temp\lz4\*.dll "--codec" deflate "--move" }
	exec { cmd /c temp\libz.exe add --libz tool\doboz.libzcodec -i temp\doboz\*.dll "--codec" deflate "--move" }
	
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
