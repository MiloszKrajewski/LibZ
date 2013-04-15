@echo off
setlocal
set LIBZ=..\..\..\libz\bin\Release\libz.exe
del /q zlib.libzcodec 2> nul
%LIBZ% add --libz zlib.libzcodec --codec deflate --move --include *.dll
%LIBZ% list --libz zlib.libzcodec
xcopy /y /d zlib.libzcodec ..\..\..\libz\bin\Release\