## Scenario 1: Single assembly with no container
The only thing you need to do is to run:
```
libz inject-dll --assembly MyApplication.exe --include *.dll --move
```
or if you want to embed multiple DLLs inside a DLL:
```
libz inject-dll --assembly MyAssembly.dll --include *.dll --exclude MyAssembly.dll --move
```
*NOTE*: ```MyAssembly.dll``` is excluded so it won't be include into itself (it sounds stupid, but it would actually work).

And that's about trivial cases. It should be sufficient for 90% of applications. But sometimes you need more.

*NOTE*: injecting dll (skipping the container part, see below) injects very simple assembly resolver. It is actually that simple that it cannot resolve assemblies by partial names nor use 'safe-load' mode. If you use partial names (for example, you use NHibernate and configure it with .hbm files) please use .libz container (embedded or not, doesn't matter). Only container resolver support partial names. I would recommend "Scenario 4".

## Scenario 2: Multiple executables sharing same assemblies
Build the container:
```
libz add --libz MyApplicationSuite.libz --include *.dll --move
```
then you need to instrument all the executables which are going to use this container:
```
... foreach N ...
libz instrument --assembly MyApplicationN.exe --libz-file MyApplicationSuite.libz
```

## Scenario 3a: Application with multiple containers
Build the containers:

```
libz add --libz DevExpress.libz --include DevExpress*.dll --move
libz add --libz NHibernate.libz --include NHibernate*.dll --move
libz add --libz Utilities.libz --include *.dll --move
```
then you need to instrument the executable to actually use the containers:
```
libz instrument --assembly MyApplication.exe --libz-file DevExpress.libz --libz-file NHibernate.libz --libz-file Utilities.libz
```

## Scenario 3b: Application with multiple containers
As an exercise, this time it will be expected to have all .libz files in subfolder "libraries" (relatively to .exe).
Build the containers:

```
mkdir libraries
libz add --libz libraries\DevExpress.libz --include DevExpress*.dll --move
libz add --libz libraries\NHibernate.libz --include NHibernate*.dll --move
libz add --libz libraries\Utilities.libz --include *.dll --codec deflate --move
```
then you need to instrument the executable to actually use the containers:
```
libz instrument --assembly MyApplication.exe --libz-pattern libraries\*.libz
```

## Scenario 4: Application with multiple embedded containers:
Build the containers:
```
libz add --libz DevExpress.libz --include DevExpress*.dll --move
libz add --libz NHibernate.libz --include NHibernate*.dll --move
libz add --libz Utilities.libz --include *.dll --move
```
inject them into main executable:
```
libz inject-libz -assembly MyApplication.exe --libz DevExpress.libz --move
libz inject-libz -assembly MyApplication.exe --libz NHibernate.libz --move
libz inject-libz -assembly MyApplication.exe --libz Utilities.libz --move
```
then you need to instrument executable to make it actually use the containers:
```
libz instrument --assembly MyApplication.exe --libz-resources
```

## Scenario 5: Getting more control
If you need more control what is loaded you can't entirely rely on instrumentation.
Add reference to ```LibZ.Bootstrap.dll``` from your main executable (for example, by referencing [LibZ.Library](https://www.nuget.org/packages/LibZ.Library) nuget package)

```c#
using LibZ.Bootstrap;

static int Main(string[] args)
{
    LibZResolver.RegisterFileContainer("Common.libz");

    LibZResolver.Startup(() => {
        if (Configuration.GetValue("Mode") == "A")
        {
            LibZResolver.RegisterFileContainer("ModeA.libz");
        }
        else
        {
            LibZResolver.RegisterFileContainer("ModeB.libz");
        }
    });

    LibZResolver.Startup(() => {
        // here goes your code
    });
}
```
Please note, this time ```LibZ.Bootstrap.dll``` will be in your output folder. You shouldn't add it to container files, because it is the assembly which actually knows how to read them. Chick and egg problem.
So we are going to inject it without a container:
```
libz inject-dll --assembly MyApplication.exe --include LibZ.Bootstrap.dll --move
```
The other thing you could notice is the use of mysterious ```LibZResolver.Startup``` method. It doesn't do anything special it just executes the action, but it is used to isolate fragment of code from calling method (physically compiler generates anonymous class for this). To understand "why?" you need to understand how JIT works. JIT tries to resolve assembly references before it runs (compiles) the method. So if the actions were not isolated by using Startup JIT would try to resolve references before Main method is called - means before you registered "Common.libz" container (which probably contains those classes) - crashing with FileNotFoundException.

You can build your containers now:
```
libz add --libz Common.libz --include *.dll --move
libz add --libz ModeA.libz --include ModeA\*.dll --move
libz add --libz ModeB.libz --include ModeB\*.dll --move
```

## Scenario 6: Using source only approach (no instrumentation at all)
Sometime you don't want your assemblies to be rewritten (which happens if you inject assemblies or containers). Note, this is actually important when you would like to use .pdb files in production. In such case you will need to include ```LibZResolver.cs``` in your application (for example, by referencing [LibZ.Source](https://www.nuget.org/packages/LibZ.Source) nuget package). You don't need to reference ```LibZ.Bootstrap.dll``` because you have it embedded already (as source).

```c#
using LibZ.Bootstrap;

static int Main(string[] args)
{
    LibZResolver.RegisterFileContainer("DevExpress.libz");
    LibZResolver.RegisterFileContainer("NHibernate.libz");
    LibZResolver.RegisterFileContainer("Utilities.libz");
    LibZResolver.Startup(() => {
        // here goes your code
    });
}
```

## Scenario 7: Using LibZ containers as MEF catalogs
When putting all the assemblies into .libz files you lose ability to use ```DirectoryCatalog```. To give you ability to discover extensions hidden inside .libz files LibZResolver implements three methods: ```LibZResolver.GetCatalog(Guid)```, ```LibZResolver.GetCatalogs(IEnumerable<Guid>)``` and ```LibZResolver.GetAllCatalogs()```. All calls to ```LibZResolver.RegisterXXX``` return a ```Guid``` or ```IEnumerable<Guid>```. By calling ```LibZResolver.GetCatalog(Guid)``` you can retrieve the catalogue for given container file.

```c#
using LibZ.Bootstrap;

static int Main(string[] args)
{
    var core = LibZResolver.RegisterFileContainer("Core.libz");
    var plugins = LibZResolver.RegisterMultipleFileContainers("plugins\*.libz");

    var coreCatalog = LibZResolver.GetCatalog(core);
    var pluginsCatalog = new AggregateCatalog(LibZResolver.GetCatalog(plugins));
    var globalCatalog = LibZResolver.GetAllCatalogs();

    LibZResolver.Startup(() => {
        // here goes your code
    });
}
```
