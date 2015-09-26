@echo off
pushd %~dp0
call ..\packages\NuGet.exe restore ..\LibZ.sln
call ..\packages\psake.4.4.1\tools\psake.cmd .\default.ps1 %*
popd
