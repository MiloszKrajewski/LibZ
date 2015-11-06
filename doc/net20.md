## .NET 2.0, 3.0, 3.5 Support
LibZ now supports .NET 3.5 host assemblies (NOTE: it always supported .NET 3.5 guest assemblies). You don't have access to MEF functionality, of course.

Support for .NET 2.0 is a little bit iffy. Using ```inject-dll``` is relatively safe as ```AsmZResolver``` uses ```mscorlib``` 2.0.0.0. It does generate a warning, but it should be fine:
```
Attempting to inject AsmZResolver into assembly targeting framework '2.0.0.0'.
AsmZResolver should work but is neither designed nor tested with this framework.
```
**NOTE**, that I'm not planning to maintain .NET 2.0 compatibility. 
I think .NET 4.0 is important as it is not possible to have .NET 4.5 on Windows XP (which is still 35% of the market), but .NET 2.0 can be easily replaced.

Using ```LibZ.Bootstrap.dll``` and/or ```instrument``` command requires .NET 3.5. If you perform this command on .NET 2.0 executable you will see different warning:
```
Attempting to inject assemblies into assembly targeting '2.0.0.0'.
LibZResolver will work only if .NET 3.5 is also installed on target machine
```
Why a warning not an error then?

Detecting .NET 3.5 target is tricky. It references same ```mscorlib``` version 2.0.0.0 as .NET 2.0 does. The difference is, by default, it also references ```System.Core``` version 3.5.0.0, but this reference can be removed. So there is no way (at least I didn't find any) to check if .NET 2.0 assembly is really targeting 2.0 or maybe 3.5 but not referencing ```System.Core``` 3.5. So, giving a benefit of a doubt, I assume it is 3.5 which just does not use 3.5 features.