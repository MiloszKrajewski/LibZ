function test-injection-asmz([string] $netVersion, [string] $architecture) {
	$folder = "temp\net$netVersion.$architecture.asmz"
	create-folder $folder
	push-location $folder
	copy-item "$src\Tests\TestApp$netVersion\TestApp\bin\$architecture\Release\*" .\ -include *.exe,*.dll -exclude *.vshost.exe
	exec { cmd /c "$libz" inject-dll -a TestApp.exe -i *.dll --move }
	pop-location
}

function test-injection-libz([string] $netVersion, [string] $architecture) {
	$folder = "temp\net$netVersion.$architecture.libz"
	create-folder $folder
	push-location $folder
	copy-item "$src\Tests\TestApp$netVersion\TestApp\bin\$architecture\Release\*" .\ -include *.exe,*.dll -exclude *.vshost.exe
	exec { cmd /c "$libz" add -l TestApp.libz -i *.dll --move }
	exec { cmd /c "$libz" instrument -a TestApp.exe --libz-file TestApp.libz }
	pop-location
}
