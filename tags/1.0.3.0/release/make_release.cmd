@echo off
setlocal

rem ----------------------------------------------------------------------------
rem Run compilation
rem ----------------------------------------------------------------------------
call rebuild.cmd ..\LibZ.sln

rem ----------------------------------------------------------------------------
rem Copy files to target folders
rem ----------------------------------------------------------------------------
rmdir /q /s tool\
rmdir /q /s lib\
xcopy /y /d ..\libz\bin\Release\libz.exe tool\
xcopy /y /d ..\libz\bin\Release\*.libzcodec tool\opt\
xcopy /y /d ..\LibZ.Tool.Interfaces\bin\Release\LibZ.Tool.Interfaces.dll tool\opt\
xcopy /y /d ..\LibZ.Bootstrap\bin\Release\LibZ.Bootstrap.dll lib\
goto :end

:end
