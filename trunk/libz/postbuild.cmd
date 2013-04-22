@echo off
setlocal
set DIR=%1
del /q libz.libz 2> nul
rmdir /q /s merge 2> nul
xcopy /y /d * merge\
call :asmz
rmdir /q /s merge 2> nul
goto :end

:asmz
merge\libz inject-dll --assembly libz.exe --include *.dll --include ILMerge.exe --move
merge\libz instrument --assembly libz.exe --asmz
exit /b

:libz
merge\libz merge-bootstrap --main libz.exe --move
merge\libz add --libz libz.libz --codec deflate --move --include *.dll --include ILMerge.exe --overwrite
merge\libz list --libz libz.libz
merge\libz rebuild --libz libz.libz
merge\libz inject-libz --assembly libz.exe --libz libz.libz --move
exit /b

:end
