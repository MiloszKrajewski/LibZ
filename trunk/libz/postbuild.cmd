@echo off
setlocal
set DIR=%1
set KEY=-k ..\..\..\LibZ.snk
del /q libz.libz 2> nul
rmdir /q /s merge 2> nul
xcopy /y /d * merge\
merge\libz merge-bootstrap --exe libz.exe --move
merge\libz sign-and-fix %KEY% *.dll *.exe
merge\libz add --libz libz.libz --codec deflate --move *.dll ILMerge.exe
merge\libz inject --libz libz.libz --exe libz.exe %KEY% --move
rmdir /q /s merge 2> nul