@echo off
setlocal
set LIBZ=..\..\..\libz\bin\Release\libz.exe
del /q zlib.libzcodec 2> nul
%LIBZ% add --libz zlib.libzcodec --include *.dll --codec deflate --move 
xcopy /y /d zlib.libzcodec ..\..\..\libz\bin\Release\