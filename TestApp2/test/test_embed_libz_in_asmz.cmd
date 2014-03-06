@echo off
setlocal
call :test x86
call :test x64
goto :end

:test
call :enter %1 libz-in-asmz

libz add -l LZ4.libz -i LZ4*.dll --move
libz inject-libz -a ModuleLZ4.dll -l LZ4.libz --move
libz instrument -a ModuleLZ4.dll --libz-resources
libz inject-dll -a TestApp.exe -i *.dll --move
TestApp.exe

popd
exit /b

:enter
rmdir /q /s %1.%2
xcopy /y /d %1\* %1.%2\
pushd %1.%2
exit /b

:end
