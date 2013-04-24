@echo off
setlocal
set DIR=%1
del /q libz.libz 2> nul
rmdir /q /s merge 2> nul
xcopy /y /d * merge\
call :libz
rmdir /q /s merge 2> nul
goto :end

:asmz
merge\libz inject-dll --assembly libz.exe --include *.dll --include ILMerge.exe --move
exit /b

:libz
merge\libz inject-dll --assembly libz.exe --include LibZ.Bootstrap.dll --move
merge\libz add --libz libz.libz --codec deflate --move --include *.dll --include ILMerge.exe --overwrite
merge\libz rebuild --libz libz.libz
merge\libz inject-libz --assembly libz.exe --libz libz.libz --move
merge\libz instrument --assembly libz.exe --libz-resources
exit /b

:end
