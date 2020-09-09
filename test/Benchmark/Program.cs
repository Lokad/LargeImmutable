using Lokad.LargeImmutable.Mapping;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace Lokad.LargeImmutable.Tests.Benchmark
{
    public static class Program
    {
        [MessagePackObject]
        public sealed class Value
        {
            [Key(0)]
            public int AsInteger { get; set; }

            [Key(1)]
            public string AsString { get; set; }
        }

        public static void Main()
        {
            var list = LargeImmutableList<Value>.Empty();
            var n = 0u;
            var sw = Stopwatch.StartNew();

            for (var i = 0; i < 100_000_000; i++)
            {
                if (i % 10_000_000 == 0)
                {
                    if (i != 0)
                        Console.WriteLine("In {0} ({1:F0} / ms)", sw.Elapsed, 10_000_000 / sw.ElapsedMilliseconds);
                    sw.Restart();

                    var file = "./temp" + (i / 1_000_000) + ".bin";
                    long length;
                    using (var stream = new FileStream(file, FileMode.Create))
                    {
                        list.Save(stream);
                        length = stream.Length;
                    }

                    Console.WriteLine("Wrote {0} bytes in {1}", length, sw.Elapsed);
                    sw.Restart();

                    var mmap = MemoryMappedFile.CreateFromFile(file, FileMode.Open);

                    using (var mem = new MemoryMapper(mmap, 0, length))
                    list = LargeImmutableList<Value>.Load(new BigMemoryStream(mem));

                    Console.WriteLine("Read {0} bytes in {1}", length, sw.Elapsed);
                    sw.Restart();
                }

                n = (n * 631) + 237;

                var pos = (int)(n % 200_000_000);
                if (pos >= list.Count)
                {
                    pos = list.Count;
                    list = list.Add(new Value { AsInteger = 0, AsString = "" });
                }

                var old = list[pos];
                list = list.SetItem(pos, new Value
                {
                    AsInteger = old.AsInteger + 1,
                    AsString = old.AsString + i
                });
            }

            Console.WriteLine("In {0}", sw.Elapsed);
            sw.Restart();
        }
    }
}
