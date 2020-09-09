using System;

namespace Lokad.LargeImmutable.Mapping
{
    /// <summary>
    ///     A fully in-memory implementation of <see cref="IBigMemory"/>
    /// </summary>
    public sealed class VolatileMemory : IBigMemory
    {
        /// <summary> In-memory byte array that pretends to be in a file. </summary>
        private readonly ReadOnlyMemory<byte> _backing;

        public VolatileMemory(ReadOnlyMemory<byte> backing) =>
            _backing = backing;

        /// <see cref="IBigMemory.Length"/>
        public long Length => _backing.Length;

        /// <see cref="IBigMemory.AsMemory"/>
        public ReadOnlyMemory<byte> AsMemory(long offset, int length) =>
            _backing.Slice((int)offset, length);

        public void Dispose() { }

        /// <see cref="IBigMemory.Slice"/>
        public IBigMemory Slice(long offset, long length) =>
            new VolatileMemory(_backing.Slice((int)offset, (int)length));
    }
}
