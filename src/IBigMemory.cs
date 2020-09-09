using System;

namespace Lokad.LargeImmutable
{
    /// <summary> 
    ///     A large chunk of memory (usually a memory-mapped file), used for reading 
    ///     backing data for the data structures.
    /// </summary>
    public interface IBigMemory : IDisposable
    {
        /// <summary> A portion of the file, as memory. </summary>
        ReadOnlyMemory<byte> AsMemory(long offset, int length);

        /// <summary> A portion of the backing memory. </summary>
        IBigMemory Slice(long offset, long length);

        /// <summary> Total length of the memory. </summary>
        long Length { get; }
    }
}
