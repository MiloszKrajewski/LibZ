@echo off
pushd %~dp0
..\.nuget\nuget.exe restore ..\LibZ.sln
popd
