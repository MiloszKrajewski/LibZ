## LibZ, the alternative to ILMerge
LibZ is an alternative to ILMerge. It allows to distribute your applications or libraries as single file with assemblies embedded into it or combined together into container file.

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
* Obfuscators can merge assemblies but they do have same problem as ILMerge - assemblies lose identity. Even worse - classes lose their names (that's, of course, what you want when using obfuscation), so you should be very careful with it and the only proof that it really works is full system test including UI. Using a framework which uses reflection? Say goodbye to it. See: [NHibrnate](http://nhforge.org), [Caliburn.Micro](https://caliburnmicro.codeplex.com/), [url:Dapper|https://code.google.com/p/dapper-dot-net/]. It can be done, of course with configuration files/attributes saying "do no obfuscate this, do not obfuscate that". It's just a lot of work. And you constantly ask yourself: "did I miss something?". Some obfuscators (I have one particular in mind, but I won't name it) allow to embed assemblies without merging them, but in such case it becomes quite overpriced toy.
* So, LibZ allows to embed required assemblies as resource and automatically instrument your main assembly (executable) with resolver. No code needed. You write absolutely nothing.
* I had one project though, where there was multiple entry points. 5 console applications, and 2 GUIs - all in one folder. They all shared a lot of code-base (2 MB for console applications and 15MB for GUIs). I was something like 50 assemblies. NHibernate, DevExpress, NLog - you name it. The choice was to not embed assemblies (and have 50 assemblies in one folder) or embed them and have 7 enormous executables with lot of assemblies duplicated inside them). So I came with an idea of 'container file' (something like .jar in Java or .xap in Silverlight) which can be shared by all of them. I ended up with 7 small executables (30KB each) and some containers (for example: NHibernate.libz, DevExpress.libz, MyCompany.Core.libz and Utilities.libz).
* I didn't want to use Zip as container, because it would introduce the dependency on some Zip library (probably [DotNetZip](https://dotnetzip.codeplex.com). So I came up with much simpler container using built-in deflate, but extensible with additional compression algorithms on run-time.
* I was porting come code from C/C++ to .NET. I wanted native implementation for desktop and safe implementation when running in restricted environment. This lead to the situation where I had 6 assemblies doing exactly the same thing: native-x86, native-x64, c++/cli-x86, c++/cli-x64, c#-unsafe-anycpu, c#-safe-anycpu and one additional assembly which was a façade which was redirecting all the calls to fastest one (depending on platform and trust level). I just didn't like 7 assemblies polluting executable folder. With LibZ I was able to embed all of them in the façade and distribute it as single file, handling all platforms and trust levels.
* The only problem with that is you don't have assemblies as files in folder, so they won't be picked up by [MEF](https://msdn.microsoft.com/en-us/library/dd460648%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396). Good news, LibZ allows to get MEF catalogue for LibZ files.

## Matrix of things that mettered to me
| Feature | Obfuscator (obfuscate) | Obfuscator (merge) | Obfuscator (embed) | ILMerge | Costura.Fody | LibZ |
| --- | --- | --- | --- | --- | --- | --- |
| Can obfuscate | ![Y](doc/y.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) |
| Dead code elimination | ![Y](doc/y.png) | ![Y](doc/y.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) |
| Very large projects | ![Y](doc/y.png)<sup>1</sup> | ![Y](doc/y.png)<sup>1</sup> | ![Y](doc/y.png) | ![N](doc/n.png)<sup>1</sup> | ![Y](doc/y.png) | ![Y](doc/y.png) |
| Safe to reference assemblies by name | ![N](doc/n.png) | ![N](doc/n.png) | ![Y](doc/y.png) | ![N](doc/n.png) | ![Y](doc/y.png) | ![Y](doc/y.png) |
| Safe to use reflection on objects | ![N](doc/n.png)<sup>2</sup> | ![N](doc/n.png)<sup>2</sup> | ![Y](doc/y.png) | ![Y](doc/y.png) | ![Y](doc/y.png) | ![Y](doc/y.png) |
| Sharing assemblies between executables | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![Y](doc/y.png) |
| Does not alter compiled assemblies | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![Y](doc/y.png)<sup>3</sup> |
| Compress assemblies | ![Y](doc/y.png)<sup>4</sup> | ![Y](doc/y.png)<sup>4</sup> | ![Y](doc/y.png) | ![N](doc/n.png) | ![Y](doc/y.png) | ![Y](doc/y.png) |
| Compress resources | ![Y](doc/y.png) | ![Y](doc/y.png) | ![Y](doc/y.png) | ![N](doc/n.png) | ![N](doc/n.png)<sup>5</sup> | ![N](doc/n.png)<sup>5</sup> |
| Use alternative compression algorithms | ![N](doc/n.png)<sup>6</sup> | ![N](doc/n.png)<sup>6</sup> | ![N](doc/n.png)<sup>6</sup> | ![N](doc/n.png) | ![N](doc/n.png)<sup>7</sup> | ![Y](doc/y.png)<sup>8</sup> |
| Merge assemblies with conflicting resources | ![N](doc/n.png)<sup>9</sup> | ![N](doc/n.png)<sup>9</sup> | ![Y](doc/y.png) | ![N](doc/n.png) | ![Y](doc/y.png) | ![Y](doc/y.png) |
| Mixing 32-bit and 64-bit assemblies | ![N](doc/n.png) | ![N](doc/n.png) | ![Y](doc/y.png)<sup>10</sup> | ![N](doc/n.png) | ![Y](doc/y.png) | ![Y](doc/y.png) |
| MEF discovery<sup>11</sup> | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![N](doc/n.png) | ![Y](doc/y.png) |
| Risk that it just won't work | High | Medium | Low | Medium | Low | Low |
| Intellectual property protection<sup>12</sup> | Medium | None | Low | None | None | None |

*1.* It's yes, but I've seen ILMerge and commercial obfuscator crashing with OutOfMemoryException on 15MB executable (actually ILMerge could crash much earlier)
*2.* With obfuscation and pruning it's not safe. Obfuscation changes member names, pruning can remove member completely when they are not used "conventionally". You need to do some mumbo-jumbo with attributes and/or configuration files to prevent that. But you need to know upfront which classes are you going to use reflection on. If you don't know on compile time it makes the whole obfuscation/pruning useless. Please note that [Dapper](https://code.google.com/p/dapper-dot-net) uses reflection on anonymous classes. You can't add attributes to anonymous classes. There are workarounds but the whole thing requires some (up to "a lot of") work.
*3.* The standard approach to LibZ does alter compiled assemblies (so it should be 'No' like the others). There is a way, though, requiring dropping one file into a project to avoid post-processing completely.
*4.* Pruning is king of compression (removing redundancy). It has some disadvantages (again: reflection), but it's quite useful to remove code which does not get called at all. Additionally you can create mini-bootstrap application which embeds (and compresses) merged/pruned assembly. It's two stage process, but gives best results.
*5.* Resources are compressed because assemblies are compressed. It's not real resource compression.
*6.* The obfuscator I know uses zlib.
*7.* Costura.Fody uses built-in deflate.
*8.* By default LibZ uses built-in deflate, but you can plug in any algorithm you want.
*9.* To be honest I actually don't know (never tried) but it doesn't seems possible as you could build resource names to load dynamically in your code.
*10.* Unless you handle it yourself I'm not sure if it is able to decide which one to load.
*11.* Of course, with all those solutions you can get MEF catalogues knowing the assembly, but this is about *discovery*.
*12.* Some may say that "all of them give zero protection". But, let's face it, wannabe hacker can be stopped by good obfuscator. With protection is like with encryption: you can call protection unbreakable if cost of breaking it is greater than price of the thing it is protecting.

## If you don't like reading manuals
I will try to describe many ways you can use it in [Documentation], but if you just don't like reading manuals, and you need the simplest case just install the tool run it using command-line:
```
libz inject-dll --assembly MyApplication.exe --include *.dll --move
```
Done.
