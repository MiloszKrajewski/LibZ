## Trace
In case of problems, unresolved references and such you can try to debug LibZ compressed assemblies using trace. It was reported in [this issue](https://libz.codeplex.com/workitem/3) that LibZ works so well and is so reliable (ok, I'm joking) that it does not need debugging so trace just clutters when tracing other things. 

So, by default, Tracing is now turned off.
It can be turned on by settings ```HKCU\Software\Softpark\LibZ\Trace``` (for current user) or ```HKLM\Software\Softpark\LibZ\Trace``` (for whole machine) to ```DWORD(1)```.

Note, ```HKCU``` takes precedence over ```HKLM```, so setting to ```DWORD(0)``` for ```HKCU``` and ```DWORD(1)``` for ```HKLM``` will make it "turned off".

Registry file to turn if on:
```
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\Software\Softpark\LibZ]
"Trace"=dword:00000001
```
