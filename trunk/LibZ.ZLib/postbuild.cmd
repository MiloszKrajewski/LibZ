@echo off
setlocal
set LIBZ=..\..\..\libz\bin\Release\libz.exe
set KEY=-k ..\..\..\LibZ.snk
del /q LibZ.ZLib.plugin 2> nul
%LIBZ% sign-and-fix %KEY% *.dll
%LIBZ% add --libz LibZ.ZLib.plugin --codec deflate --move *.dll
