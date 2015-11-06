## Matrix of things that mattered to me
| Feature | Obfuscator (obfuscate) | Obfuscator (merge) | Obfuscator (embed) | ILMerge | Costura.Fody | LibZ |
| --- | --- | --- | --- | --- | --- | --- |
| Can obfuscate | ![Y](y.png) | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![N](n.png) |
| Dead code elimination | ![Y](y.png) | ![Y](y.png) | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![N](n.png) |
| Very large projects | ![Y](y.png)<sup>1</sup> | ![Y](y.png)<sup>1</sup> | ![Y](y.png) | ![N](n.png)<sup>1</sup> | ![Y](y.png) | ![Y](y.png) |
| Safe to reference assemblies by name | ![N](n.png) | ![N](n.png) | ![Y](y.png) | ![N](n.png) | ![Y](y.png) | ![Y](y.png) |
| Safe to use reflection on objects | ![N](n.png)<sup>2</sup> | ![N](n.png)<sup>2</sup> | ![Y](y.png) | ![Y](y.png) | ![Y](y.png) | ![Y](y.png) |
| Sharing assemblies between executables | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![Y](y.png) |
| Does not alter compiled assemblies | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![Y](y.png)<sup>3</sup> |
| Compress assemblies | ![Y](y.png)<sup>4</sup> | ![Y](y.png)<sup>4</sup> | ![Y](y.png) | ![N](n.png) | ![Y](y.png) | ![Y](y.png) |
| Compress resources | ![Y](y.png) | ![Y](y.png) | ![Y](y.png) | ![N](n.png) | ![N](n.png)<sup>5</sup> | ![N](n.png)<sup>5</sup> |
| Use alternative compression algorithms | ![N](n.png)<sup>6</sup> | ![N](n.png)<sup>6</sup> | ![N](n.png)<sup>6</sup> | ![N](n.png) | ![N](n.png)<sup>7</sup> | ![Y](y.png)<sup>8</sup> |
| Merge assemblies with conflicting resources | ![N](n.png)<sup>9</sup> | ![N](n.png)<sup>9</sup> | ![Y](y.png) | ![N](n.png) | ![Y](y.png) | ![Y](y.png) |
| Mixing 32-bit and 64-bit assemblies | ![N](n.png) | ![N](n.png) | ![Y](y.png)<sup>10</sup> | ![N](n.png) | ![Y](y.png) | ![Y](y.png) |
| MEF discovery<sup>11</sup> | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![N](n.png) | ![Y](y.png) |
| Risk that it just won't work | High | Medium | Low | Medium | Low | Low |
| Intellectual property protection<sup>12</sup> | Medium | None | Low | None | None | None |

1. It's yes, but I've seen ILMerge and commercial obfuscator crashing with *OutOfMemoryException* on 15MB executable (actually ILMerge could crash much earlier)
2. With obfuscation and pruning it's not safe. Obfuscation changes member names, pruning can remove member completely when they are not used "conventionally". You need to do some mumbo-jumbo with attributes and/or configuration files to prevent that. But you need to know upfront which classes are you going to use reflection on. If you don't know on compile time it makes the whole obfuscation/pruning useless. Please note that [Dapper](https://code.google.com/p/dapper-dot-net) uses reflection on anonymous classes. You can't add attributes to anonymous classes. There are workarounds but the whole thing requires some (up to "a lot of") work.
3. The standard approach to LibZ does alter compiled assemblies (so it should be 'No' like the others). There is a way, though, requiring dropping one file into a project to avoid post-processing completely.
4. Pruning is king of compression (removing redundancy). It has some disadvantages (again: reflection), but it's quite useful to remove code which does not get called at all. Additionally you can create mini-bootstrap application which embeds (and compresses) merged/pruned assembly. It's two stage process, but gives best results.
5. Resources are compressed because assemblies are compressed. It's not real resource compression.
6. The obfuscator I know uses zlib.
7. Costura.Fody uses built-in deflate.
8. By default LibZ uses built-in deflate, but you can plug in any algorithm you want.
9. To be honest I actually don't know (never tried) but it doesn't seems possible as you could build resource names to load dynamically in your code.
10. Unless you handle it yourself I'm not sure if it is able to decide which one to load.
11. Of course, with all those solutions you can get MEF catalogues knowing the assembly, but this is about *discovery*.
12. Some may say that "all of them give zero protection". But, let's face it, wannabe hacker can be stopped by good obfuscator. With protection is like with encryption: you can call protection unbreakable if cost of breaking it is greater than price of the thing it is protecting.
