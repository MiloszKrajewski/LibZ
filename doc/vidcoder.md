## VidCoder
[VidCoder](https://vidcoder.codeplex.com) is excellent application for video encoding. As a proof to myself I used it for testing. I am not it's author, I don't know the author, and I didn't touch the source code.
I just took some .NET application and tried to reduce the number of files.

## The riddle of System.Data.SQLite.dll
VidCoder has two native assemblies: ```hb.dll``` and ```System.Data.SQLite.dll```. There is a funny thing with SQLite assembly. It seems to be managed assembly but it P/Invokes itself so it requires to be physically in application folder. Strange. Why it wasn't done as mixed mode? I don't know. Anyway it cannot be embedded.

## Test 1: Inject all .dlls into .exe
Inject all .dlls into main executable:
```
libz inject-dll -a VidCoder.exe -i *.dll -e *sql* --move
```
Works.

## Test 2: Inject .dlls into external .libz container
Put all .dlls into .libz container and instrument main executable to use it:
```
libz add -l VidCoder.libz -i *.dll -e *sql* --move
libz instrument -a VidCoder.exe --libz-file VidCoder.libz
```
Works.

## Test 3: Inject .dlls into embedded .libz container
Put all .dlls into .libz container, embed it into main executable then instrument it to use this container:
```
libz add -l VidCoder.libz -i *.dll -e *sql* --move
libz inject-libz -a VidCoder.exe -l VidCoder.libz --move
libz instrument -a VidCoder.exe --libz-resources
```
Works.
