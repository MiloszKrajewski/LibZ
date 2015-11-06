## Note on signed assemblies
Instrumenting code (and injecting resources) requires signed assemblies to be resigned. All commands modifying assemblies (inject-dll, instrument, etc) accept ```--key``` and ```--password``` arguments. You need to provide ```--key``` to update the signature and (optionally) ```--password``` if key file is password protected.
You can also completely ignore it up to the very end and sign resulting assemblies with:
```
libz sign --assembly MyApplication.exe --key MyKeyFile.snk
```

## "Sign and fix" command
It is possible to use your project using unsigned assemblies and sign all of them at the very end. It is important if you rely on unsigned third-party assemblies.
```
libz sign-and-fix --include *.dll --include *.exe -key MyKeyFile.snk
```
Sign and fix scans all the assemblies, build a dependency tree, signs assemblies and replaces unsigned references to signed ones.
There is one problem though. It does not replace references in resources, config files, and... unfortunatelly attributes (for example: ```[MyAttribute(typeof(MyType))]```). I'll try to fix it in the future (sign-and-fix is not main main concern) but for now - it won't work if assembly where MyType us gets resigned during sign-and-fix. MyAttribute will still hold the reference to unsigned assembly, which you probably won't have in your "distributable". 
It's not a big problem, you just have to remember: not all assemblies can be signed on the end, some of them need to be signed up-front. 

**Anyway I would call this feature "unreliable".** 
