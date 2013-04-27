@echo off
setlocal
call :test x86
call :test x64
goto :end

:test
call :enter %1 asmz-in-libz

libz inject-dll -a ModuleLZ4.dll -i LZ4*.dll --move
libz add -l TestApp.libz -i *.dll --move
libz inject-libz -a TestApp.exe -l TestApp.libz --move
libz instrument -a TestApp.exe --libz-resources
TestApp.exe

popd
exit /b

:enter
rmdir /q /s %1.%2
xcopy /y /d %1\* %1.%2\
pushd %1.%2
exit /b

:end
