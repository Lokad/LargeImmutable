using MessagePack;
using MessagePack.Formatters;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Lokad.LargeImmutable
{
    /// <summary>
    ///     An immutable list that supports only appending elements. 
    /// </summary>
    /// <remarks>
    ///     Part of the elements (provided at creation time) are backed by a 
    ///     <see cref="IBigMemory"/>, the others are backed by a normal immutable list.
    /// </remarks>
    public sealed class LargeImmutableList<T> : IReadOnlyList<T>
    {
        /// <summary> Data shared by all instances mutated from this one. </summary>
        private class Shared
        {
            /// <summary> Used to serialize values of type T. </summary>
            public readonly IMessagePackFormatter<T> Formatter;

            /// <summary> Used by <see cref="Formatter"/>. </summary>
            public readonly MessagePackSerializerOptions Options;

            /// <summary> The number of elements backed by <see cref="Backing"/>. </summary>
            public readonly int Backed;

            /// <summary> Backing memory for the values that have already been backed. </summary>
            public readonly IBigMemory Backing;

            /// <summary> A range of uint64 offsets, one for each backed value, plus one. </summary>
            public readonly ReadOnlyMemory<byte> Offsets;

            public Shared(
                IMessagePackFormatter<T> formatter, 
                MessagePackSerializerOptions options,
                int backed, 
                ReadOnlyMemory<byte> offsets,
                IBigMemory backing)
            {
                Formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
                Options = options ?? throw new ArgumentNullException(nameof(options));
                Backed = backed;
                Backing = backing ?? throw new ArgumentNullException(nameof(backing));
                Offsets = offsets;
            }

            public Shared(
                MessagePackSerializerOptions options,
                int backed,
                IBigMemory backing)
            : this(
                options.Resolver.GetFormatter<T>(),
                options,
                backed,
                backing.AsMemory(
                    backing.Length - (backed + 1) * sizeof(long), 
                    (backed + 1) * sizeof(long)),
                backing)
            { }

            /// <summary> When empty. </summary>
            public Shared(
                MessagePackSerializerOptions options)
            {
                Formatter =
                options.Resolver.GetFormatter<T>();
                Options = options ?? throw new ArgumentNullException(nameof(options));
                Backed = 0;
            }
        }

        /// <see cref="Shared"/>
        private readonly Shared _shared;

        /// <summary> The elements not backed by memory. </summary>
        private readonly ImmutableList<T> _unbacked;

        /// <summary> The elements that used to be backed, but have been overwritten since. </summary>
        private readonly ImmutableDictionary<int, T> _overwritten;

        private LargeImmutableList(
            Shared shared, 
            ImmutableList<T> unbacked, 
            ImmutableDictionary<int, T> overwritten)
        {
            _shared = shared ?? throw new ArgumentNullException(nameof(shared));
            _unbacked = unbacked ?? throw new ArgumentNullException(nameof(unbacked));
            _overwritten = overwritten ?? throw new ArgumentNullException(nameof(overwritten));
        }

        public int Count => _shared.Backed + _unbacked.Count;

        /// <summary> true if the list is empty; otherwise, false. </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsEmpty => Count == 0;

        public T this[int index]
        {
            get
            {
                if (index >= _shared.Backed)
                    return _unbacked[index - _shared.Backed];

                if (index < 0)
                    throw new ArgumentOutOfRangeException();

                if (_overwritten.TryGetValue(index, out var overwritten))
                    return overwritten;

                return ReadBacked(_shared, index);
            }
        }

        /// <summary> An empty immutable list with the specified serialization options. </summary>
        public static LargeImmutableList<T> Empty(MessagePackSerializerOptions options) =>
            new LargeImmutableList<T>(
                new Shared(options),
                ImmutableList<T>.Empty,
                ImmutableDictionary<int, T>.Empty);

        /// <summary> An empty immutable list with the "standard" serialization options. </summary>
        public static LargeImmutableList<T> Empty() =>
            new LargeImmutableList<T>(
                new Shared(MessagePackSerializerOptions.Standard),
                ImmutableList<T>.Empty,
                ImmutableDictionary<int, T>.Empty);

        /// <summary> Adds the specified object to the end of the immutable list. </summary>
        public LargeImmutableList<T> Add(T value) =>
            new LargeImmutableList<T>(_shared, _unbacked.Add(value), _overwritten);

        /// <summary> Adds the elements of the specified collection to the end of the immutable list. </summary>
        public LargeImmutableList<T> AddRange(IEnumerable<T> value) =>
            new LargeImmutableList<T>(_shared, _unbacked.AddRange(value), _overwritten);

        /// <summary> 
        ///     Removes all elements from the immutable list.
        ///     Preserves the serialization options.
        /// </summary>
        public LargeImmutableList<T> Clear() => Empty(_shared.Options);

        /// <summary>
        ///     Replaces an element at a given position in the immutable list with the specified
        ///    element.
        /// </summary>
        /// <param name="index"> The position in the list of the element to replace. </param>
        /// <param name="value"> The element to replace the old element with. </param>
        /// <returns> 
        ///     The new list with the replaced element, even if it is equal to 
        ///     the old element at that position. 
        /// </returns>
        public LargeImmutableList<T> SetItem(int index, T value)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException();

            if (index >= _shared.Backed)
                return new LargeImmutableList<T>(
                    _shared,
                    _unbacked.SetItem(index - _shared.Backed, value),
                    _overwritten);

            return new LargeImmutableList<T>(
                _shared,
                _unbacked,
                _overwritten.SetItem(index, value));
        }

        /// <summary> Remove the last <paramref name="count"/> elements from the list. </summary>
        public LargeImmutableList<T> RemoveLast(int count = 1)
        {
            var newCount = Count - count;

            if (newCount == Count)
                return this;

            if (newCount < 0)
                throw new ArgumentOutOfRangeException();

            if (newCount == 0)
                return Clear();

            if (newCount >= _shared.Backed)
                return new LargeImmutableList<T>(
                    _shared,
                    _unbacked.RemoveRange(_unbacked.Count - count, count),
                    _overwritten);

            return new LargeImmutableList<T>(
                new Shared(
                    _shared.Formatter,
                    _shared.Options,
                    newCount,
                    _shared.Offsets,
                    _shared.Backing),
                ImmutableList<T>.Empty,
                _overwritten.IsEmpty 
                    ? _overwritten
                    : _overwritten.RemoveRange(_overwritten.Keys.Where(k => k >= newCount)));
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < _shared.Backed; ++i)
            {
                if (_overwritten.TryGetValue(i, out var overwritten))
                    yield return overwritten;
                else
                    yield return ReadBacked(_shared, i);
            }

            foreach (var t in _unbacked)
                yield return t;
        }

        IEnumerator IEnumerable.GetEnumerator() => 
            GetEnumerator();

        /// <summary> Read the i-th entry from the memory backing. </summary>
        private static T ReadBacked(Shared shared, int i)
        {
            // Identify the range of bytes for the entry.
            var offsets = MemoryMarshal.Cast<byte,long>(shared.Offsets.Span);
            var start = offsets[i];
            var length = (int)(offsets[i + 1] - start);

            if (length == 0)
                return default;

            // Load the byte range into a MessagePack reader
            var memory = shared.Backing.AsMemory(start, length);
            var reader = new MessagePackReader(memory);

            return shared.Formatter.Deserialize(ref reader, shared.Options);
        }

        /// <summary> Load from a stream backed by big-memory. </summary>
        /// <remarks> Uses the "standard" options. </remarks>
        public static LargeImmutableList<T> Load(
            BigMemoryStream stream)
        =>
            Load(stream, MessagePackSerializerOptions.Standard);

        /// <summary> Load from a stream backed by big-memory. </summary>
        public static LargeImmutableList<T> Load(
            BigMemoryStream stream,
            MessagePackSerializerOptions options)
        {
            long backingSize;
            int backedCount;
            using (var br = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                backingSize = br.ReadInt64();
                backedCount = br.ReadInt32();
            }

            var big = stream.AsBigMemory(backingSize);

            return new LargeImmutableList<T>(
                new Shared(options, backedCount, big),
                ImmutableList<T>.Empty,
                ImmutableDictionary<int, T>.Empty);
        }

        /// <summary> Save to a **seekable** stream. </summary>
        public void Save(Stream stream)
        {
            if (!stream.CanSeek)
                throw new ArgumentException(nameof(stream), "Stream not seekable.");

            var copyArray = Array.Empty<byte>();

            using (var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                // Remember the position and skip ahead the backing size ; it will be filled in later, 
                // once it has been computed.
                var backingSizePosition = stream.Position;
                bw.Write(0L);

                // The count is already known.
                bw.Write(Count);

                var offsetsBytes = new byte[sizeof(long) * (Count + 1)];
                var offsets = MemoryMarshal.Cast<byte, long>(offsetsBytes);
                var startPosition = stream.Position;

                // Writes the value to the stream, returns its end offset (relative to 
                // the startPosition)
                long WriteValue(T value)
                {
                    if (typeof(T).IsValueType || value != null)
                        // TODO: can likely be made faster
                        MessagePackSerializer.Serialize(stream, value, _shared.Options);

                    return stream.Position - startPosition;
                }

                // Copies a backed value to the stream, returns its end offset (relative
                // to the startPosition)
                long CopyValue(int index)
                {
                    var oldOffsets = MemoryMarshal.Cast<byte,long>(_shared.Offsets.Span);
                    var oldStart = oldOffsets[index];
                    var length = (int)(oldOffsets[index + 1] - oldStart);

                    if (length > 0)
                    {
                        if (copyArray.Length < length)
                            copyArray = new byte[Math.Max(4096, length)];

                        // TODO: don't use a buffer to copy bytes
                        _shared.Backing.AsMemory(oldStart, length).Span.CopyTo(copyArray);
                        stream.Write(copyArray, 0, length);
                    }

                    return stream.Position - startPosition;
                }

                for (var i = 0; i < _shared.Backed; ++i)
                {
                    if (_overwritten.TryGetValue(i, out var value))
                    {
                        offsets[i + 1] = WriteValue(value);
                    }
                    else
                    {
                        offsets[i + 1] = CopyValue(i);
                    }
                }

                for (var i = 0; i < _unbacked.Count; ++i)
                {
                    offsets[_shared.Backed + i + 1] = WriteValue(_unbacked[i]);
                }

                // Now, write the offsets themselves
                stream.Write(offsetsBytes, 0, offsetsBytes.Length);

                // Done writing, go back and save the length.
                var finalPosition = stream.Position;

                stream.Position = backingSizePosition;
                bw.Write(finalPosition - startPosition);

                // Length saved, go back to end of stream.
                stream.Position = finalPosition;
            }
        }
    }
}
