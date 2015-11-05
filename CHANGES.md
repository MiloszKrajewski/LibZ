## 1.1.0.4
- --safe-load option forcing assemblies to be loaded from disk
- fixes for mono compatibility (still work in progress)
- removed plugabble compressor (configuration pain with no major benefit)
- minor bugfixes

## 1.1.0.2
- safer static constructor (underlying problem is still unknown, but this fix seems to work)
- changed logging from stderr to stdout

## 1.1.0.1
- removed static MD5 object (in multithreaded code it might had throw Cryptographic exception out of the blue)

## 1.1.0.0
- added support for .NET 3.5 (and .NET 2.0 but with some limitations)
- trace is now optional (so you can trace if something goes wrong, but by default there is no Trace output from LibZ)
- fixed a bug in SignAssemblyTask preventing from signing managed assemblies

## 1.0.3.7
- fixes 'portable' assemblies problem (they did load, their 'fake' dependencies did not)
