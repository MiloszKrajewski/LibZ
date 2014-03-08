@echo off
pushd %~dp0
..\.nuget\nuget.exe restore ..\LibZ.sln
call ..\packages\psake.4.3.1.0\tools\psake.cmd .\default.ps1 %*
popd
