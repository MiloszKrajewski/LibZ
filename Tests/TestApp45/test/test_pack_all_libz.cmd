@echo off
setlocal
call :test x86
call :test x64
goto :end

:test
call :enter %1 all-packed-libz

libz add -l TestApp.libz -i *.dll --move
libz instrument -a TestApp.exe --libz-file TestApp.libz
TestApp.exe

popd
exit /b

:enter
rmdir /q /s %1.%2
xcopy /y /d %1\* %1.%2\
pushd %1.%2
exit /b

:end
