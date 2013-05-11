@echo off
setlocal
set LIBZ=..\..\..\libz\bin\Release\libz.exe
del /q doboz.libzcodec 2> nul
%LIBZ% add --libz doboz.libzcodec --include *.dll --codec deflate --move
xcopy /y /d doboz.libzcodec ..\..\..\libz\bin\Release\