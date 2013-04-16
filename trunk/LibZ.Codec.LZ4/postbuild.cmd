@echo off
setlocal
set LIBZ=..\..\..\libz\bin\Release\libz.exe
del /q lz4.libzcodec 2> nul
%LIBZ% add --libz lz4.libzcodec --codec deflate --move --include *.dll
%LIBZ% list --libz lz4.libzcodec
%LIBZ% rebuild --libz lz4.libzcodec
xcopy /y /d lz4.libzcodec ..\..\..\libz\bin\Release\