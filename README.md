# Lokad.LargeImmutable

Immutable .NET collections that can be memory-mapped from a file. Available as 
NuGet package [Lokad.LargeImmutable](https://www.nuget.org/packages/Lokad.LargeImmutable).

This package improves the memory footprint of immutable collections where only a 
small subset of the contents are modified regularly.

This is achieved by performing a snapshot of the collection, saving it to 
disk, and then [memory-mapping](https://en.wikipedia.org/wiki/Memory-mapped_file) 
the file. Any changes applied on top of the snapshot are kept in-memory using
the official .NET immutable collections. As long as the number of changed elements
between snapshots remains small, the managed memory footprint will be low.

Elements are expected to be serializable with MessagePack. 

### Quick tutorial

A `LargeImmutableList<T>` implements all relevant methods of `ImmutableList<T>` 
(but with the expectation that most changes will happen at the end of the list, 
rather than in the middle):

```csharp
using Lokad.LargeImmutable;

var list = LargeImmutableList<string>.Empty()
	.Add("Alpha")
	.Add("Barvo")
	.Add("Charles")
	.SetItem(1, "Bravo");
```

The list can be saved to a file stream:

```csharp
using System.IO;

using (var stream = File.OpenWrite("myFile.bin"))
{
	list.Save(stream);
}
```

The file must then be memory-mapped and exposed as a `BigMemoryStream`, 
and the list is reloaded from that stream:

```csharp
using System.IO.MemoryMappedFiles;
using Lokad.LargeImmutable.Mapping;

var mapper = new MemoryMapper(
	MemoryMappedFile.CreateFromFile("myFile.bin", FileMode.Open),
	0,
	new FileInfo("myFile.bin").Length);

using (mapper)
{
	var stream = new BigMemoryStream(mapper);
	list = LargeImmutableList<string>.Load(stream);
}
```

The reloaded list can still be used as a normal immutable list: 

```csharp
Assert.Equal("Bravo", list[1]);

list = list.SetItem(2, "Charlie").Add("Delta");

Assert.Equal("Charlie", list[2])
Assert.Equal("Delta", list[3]);
```

The save-reload cycle can be repeated several times.

Although the `MemoryMapper` is disposed, the `MemoryMappedFile` will 
only be disposed when the list (and all partial copies) is finalized. 
If you need to control the disposal of the `MemoryMappedFile`, you can 
force its disposal manually, but be warned that any surviving lists will 
start throwing exceptions when accessed.

The `BigMemoryStream` can be used as a `Stream`. This makes it possible
to: 

 1. Include other data in the file, in addition to the LargeImmutable 
    collection (for instance, with a `BinaryReader` and `BinaryWriter`
	pair). The `Load()` method advances the stream to the end of the 
	memory-mapped collection. 
 2. It's possible to store several LargeImmutable collections in a 
    single file, and memory-map them all from separate portions of the 
	file.
