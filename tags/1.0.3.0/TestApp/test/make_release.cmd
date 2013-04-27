@echo off
setlocal

rem ----------------------------------------------------------------------------
rem Run compilation
rem ----------------------------------------------------------------------------
call rebuild.cmd ..\TestApp.sln x86
call rebuild.cmd ..\TestApp.sln x64

rem ----------------------------------------------------------------------------
rem Copy files to target folders
rem ----------------------------------------------------------------------------
rmdir /q /s x86\
rmdir /q /s x64\
xcopy /y /d ..\TestApp\bin\x86\Release\*.dll x86\
xcopy /y /d ..\TestApp\bin\x86\Release\*.exe x86\
xcopy /y /d ..\TestApp\bin\x64\Release\*.dll x64\
xcopy /y /d ..\TestApp\bin\x64\Release\*.exe x64\
del /q x86\*.vshost.exe
del /q x64\*.vshost.exe

goto :end

:end
