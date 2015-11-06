## LibZ, the alternative to ILMerge
LibZ is an alternative to ILMerge. It allows to distribute your applications or libraries as single file with assemblies embedded into it or combined together into container file.

## Migration from codeplex
As source has been fully migrated some documentation and older releases are still available [here](http://libz.codeplex.com)

## License
Lib is license under [Ms-PL](LICENSE.md). Long story short - you can use it, you can modify it as long as you don't claim you wrote it.

## Change log
Changed can found [here](CHANGES.md)

## Why not ILMerge?
ILMerge in many cases is the first thing you tried and it does what it does just fine, sometimes you just don't want it to do what it does, you just don't know that yet. Problems start to appear when you application uses reflection, or even worse, when 3rd part libraries use reflection. The alternative is "embedding" instead of "merging".
Jeffrey Richter described this technique on his [blog](http://blogs.msdn.com/b/microsoft_press/archive/2010/02/03/jeffrey-richter-excerpt-2-from-clr-via-c-third-edition.aspx) (and in his book) few years ago. I think the best recommendation is the comment from Mike Barnett, author of ILMerge: "As the author of ILMerge, I think this is fantastic! If I had known about this, I never would have written ILMerge. Many people have run into problems using ILMerge for WPF applications because WPF encodes assembly identities in binary resources that ILMerge is unable to modify. But this should work great for them."

## Download
Probably the best way to download it is to use [NuGet](https://nuget.org/packages/LibZ.Bootstrap).

## Before you start
LibZ Container is not better than [ILMerge](https://nuget.org/packages/ilmerge), [IL-Repack](https://github.com/gluck/il-repack), [Costura.Fody](http://nuget.org/packages/Costura.Fody), etc. They do what they do well, they just lacked some features I needed (so I implemented them in LibZ). They also had some features which I didn't need (so I didn't implement them in LibZ).
Read carefully what LibZ does and doesn't do before making a call.
Some things I didn't like are just bugs (so they can be fixed sooner of later) some of them are just by design (and let me repeat, not bad design, just design for different purpose).

----
## Motivation
There are multiple things which motivated me to write this library / tool.
* First of all I do like small single-purpose assemblies, so they can be composed in many different ways and not introduce too much unused code. Frequently, when looking for some libraries (github, codeplex) I find them interesting, I'm just put off by the fact that 90% of that library is something I've already written for myself (and I prefer mine). I just won't use a library saying "many utilities doing many things". Sorry. Be specific. Do one thing. Do it well. With all the respect for the author of [Json.NET](|https://json.codeplex.com/] I just don't think his [Utilities.NET](https://utilities.codeplex.com) should be distributed as one assembly. I've implemented 90% of it myself already, why can't I just pick parts which I need.
* So I do use small assemblies. I don't like a lot of assemblies in distributables, though. They (distributables) should be sleek. One file preferably.
* I was using ILMerge for quite some time, but ILMerge has a lot of disadvantages. First of all it does not work (by definition) if assemblies / classes are referenced from code using their strong name. During merge assemblies lose their identity and they all get injected into one big bag of code. WPF? NHibernate (XML driven)? Nope. It won't work.
* Obfuscators can merge assemblies but they do have same problem as ILMerge - assemblies lose identity. Even worse - classes lose their names (that's, of course, what you want when using obfuscation), so you should be very careful with it and the only proof that it really works is full system test including UI. Using a framework which uses reflection? Say goodbye to it. See: [NHibrnate](http://nhforge.org), [Caliburn.Micro](https://caliburnmicro.codeplex.com/), [Dapper](https://code.google.com/p/dapper-dot-net). It can be done, of course with configuration files/attributes saying "do no obfuscate this, do not obfuscate that". It's just a lot of work. And you constantly ask yourself: "did I miss something?". Some obfuscators (I have one particular in mind, but I won't name it) allow to embed assemblies without merging them, but in such case it becomes quite overpriced toy.
* So, LibZ allows to embed required assemblies as resource and automatically instrument your main assembly (executable) with resolver. No code needed. You write absolutely nothing.
* I had one project though, where there was multiple entry points. 5 console applications, and 2 GUIs - all in one folder. They all shared a lot of code-base (2 MB for console applications and 15MB for GUIs). I was something like 50 assemblies. NHibernate, DevExpress, NLog - you name it. The choice was to not embed assemblies (and have 50 assemblies in one folder) or embed them and have 7 enormous executables with lot of assemblies duplicated inside them). So I came with an idea of 'container file' (something like .jar in Java or .xap in Silverlight) which can be shared by all of them. I ended up with 7 small executables (30KB each) and some containers (for example: NHibernate.libz, DevExpress.libz, MyCompany.Core.libz and Utilities.libz).
* I didn't want to use Zip as container, because it would introduce the dependency on some Zip library (probably [DotNetZip](https://dotnetzip.codeplex.com). So I came up with much simpler container using built-in deflate, but extensible with additional compression algorithms on run-time.
* I was porting come code from C/C++ to .NET. I wanted native implementation for desktop and safe implementation when running in restricted environment. This lead to the situation where I had 6 assemblies doing exactly the same thing: native-x86, native-x64, c++/cli-x86, c++/cli-x64, c#-unsafe-anycpu, c#-safe-anycpu and one additional assembly which was a façade which was redirecting all the calls to fastest one (depending on platform and trust level). I just didn't like 7 assemblies polluting executable folder. With LibZ I was able to embed all of them in the façade and distribute it as single file, handling all platforms and trust levels.
* The only problem with that is you don't have assemblies as files in folder, so they won't be picked up by [MEF](https://msdn.microsoft.com/en-us/library/dd460648%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396). Good news, LibZ allows to get MEF catalogue for LibZ files.

## Comparing LibZ with similar products
You can find matrix of features which were important to me [here](doc/matrix.md).

## If you don't like reading manuals
I strongly recommend reading [Documentation](doc/index.md), but if you just don't like reading manuals, and you need the simplest case just install the tool run it using command-line:

```
libz inject-dll --assembly MyApplication.exe --include *.dll --move
```

Done.

Please note that this approach works for trivial applications only. If your application (or 3rd party libraries) use reflection, application domains, native DLLs you might need more "personalised" approach. Please refer to [Scenarios](doc/scenarios.md).
