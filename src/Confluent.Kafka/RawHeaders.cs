using System;
using System.Runtime.InteropServices;
using Confluent.Kafka.Impl;

namespace Confluent.Kafka
{
    /// <summary>
    ///     A single Kafka message header as two <see cref="ReadOnlySpan{T}"/>
    ///     slices pointing directly into librdkafka's buffer. Valid only for the
    ///     lifetime of the enclosing <see cref="RawMessage"/>.
    /// </summary>
    public unsafe readonly ref struct RawHeader
    {
        /// <summary>The header name as UTF-8 bytes.</summary>
        public ReadOnlySpan<byte> Name { get; }

        /// <summary>The header value bytes. Empty if the header has a null value.</summary>
        public ReadOnlySpan<byte> Value { get; }

        internal RawHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            Name = name;
            Value = value;
        }

        /// <summary>Enables <c>foreach (var (name, value) in msg.Headers)</c> deconstruction.</summary>
        public void Deconstruct(out ReadOnlySpan<byte> name, out ReadOnlySpan<byte> value)
        {
            name = Name;
            value = Value;
        }
    }

    /// <summary>
    ///     Allocation-free, foreachable view over a message's headers. Obtained
    ///     via <see cref="RawMessage.Headers"/>. Valid only for the lifetime of
    ///     the enclosing <see cref="RawMessage"/>.
    /// </summary>
    public unsafe readonly ref struct RawHeaders
    {
        private readonly IntPtr hdrsPtr;

        internal RawHeaders(IntPtr hdrsPtr)
        {
            this.hdrsPtr = hdrsPtr;
        }

        /// <summary>True if the message carries no headers.</summary>
        public bool IsEmpty => hdrsPtr == IntPtr.Zero;

        /// <summary>Returns an enumerator for <c>foreach</c>.</summary>
        public Enumerator GetEnumerator() => new Enumerator(hdrsPtr);

        /// <summary>
        ///     Ref-struct enumerator backing <c>foreach</c>. Iterates by calling
        ///     <c>rd_kafka_header_get_all</c> per <c>MoveNext</c>.
        /// </summary>
        public unsafe ref struct Enumerator
        {
            private readonly IntPtr hdrsPtr;
            private int index;
            private RawHeader current;

            internal Enumerator(IntPtr hdrsPtr)
            {
                this.hdrsPtr = hdrsPtr;
                this.index = -1;
                this.current = default;
            }

            /// <summary>The header yielded by the most recent successful <see cref="MoveNext"/>.</summary>
            public RawHeader Current => current;

            /// <summary>Advances to the next header. Returns false past the end.</summary>
            public bool MoveNext()
            {
                if (hdrsPtr == IntPtr.Zero) return false;
                index++;

                var err = Librdkafka.header_get_all(hdrsPtr, (IntPtr)index,
                    out IntPtr namep, out IntPtr valuep, out IntPtr sizep);
                if (err != ErrorCode.NoError) return false;

                ReadOnlySpan<byte> nameSpan = ReadOnlySpan<byte>.Empty;
                if (namep != IntPtr.Zero)
                {
                    var p = (byte*)namep.ToPointer();
#if NET7_0_OR_GREATER
                    nameSpan = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(p);
#else
                    int len = 0;
                    while (p[len] != 0) len++;
                    nameSpan = new ReadOnlySpan<byte>(p, len);
#endif
                }

                ReadOnlySpan<byte> valueSpan = valuep == IntPtr.Zero
                    ? ReadOnlySpan<byte>.Empty
                    : new ReadOnlySpan<byte>(valuep.ToPointer(), (int)sizep);

                current = new RawHeader(nameSpan, valueSpan);
                return true;
            }
        }
    }
}
