@echo off
setlocal
set LIBZ=..\..\..\libz\bin\Release\libz.exe
del /q lz4.libzcodec 2> nul
%LIBZ% add --libz lz4.libzcodec --include *.dll --codec deflate --move
xcopy /y /d lz4.libzcodec ..\..\..\libz\bin\Release\