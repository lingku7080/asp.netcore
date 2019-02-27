using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Http.Connections.Internal.Transports
{
    class WebSocketPipeReader : PipeReader
    {
        private readonly PipeReader _internal;
        private long _payloadLength;
        private byte[] _buffer;
        private MessageHeader _header;

        public WebSocketPipeReader(PipeReader reader)
        {
            _internal = reader;
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            throw new NotImplementedException();
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            _internal.AdvanceTo(consumed, examined);
            // Rent
        }

        public override void CancelPendingRead()
        {
            throw new NotImplementedException();
        }

        public override void Complete(Exception exception = null)
        {
            throw new NotImplementedException();
        }

        public override void OnWriterCompleted(Action<Exception, object> callback, object state)
        {
            throw new NotImplementedException();
        }

        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            do
            {
                var result = await _internal.ReadAsync(cancellationToken);
                if (result.IsCanceled)
                {
                    // TODO
                }
                if (result.IsCompleted)
                {
                    // TODo
                }

                var span = result.Buffer.IsSingleSegment ? result.Buffer.First : result.Buffer.ToArray();
                if (_payloadLength == 0)
                {
                    _header = ReadHeader(span);
                    // Rent
                    _buffer = new byte[_header.PayloadLength];
                }

                if (span.Length - 6 == _payloadLength)
                {
                    span.Slice(6).CopyTo(_buffer);
                    ApplyMask(_buffer, _header.Mask, 0);
                    return new ReadResult(new ReadOnlySequence<byte>(_buffer), false, false);
                }
                else if (span.Length < _payloadLength)
                {
                    span.CopyTo(_buffer);
                    // TODO: offset

                    _payloadLength -= span.Length;
                }
                else
                {
                    // TODO
                }
            }
            while (_payloadLength != 0);

            // TODO: this is just to make it compile for early debugging
            return new ReadResult(new ReadOnlySequence<byte>(_buffer), false, false);
        }

        public override bool TryRead(out ReadResult result)
        {
            throw new NotImplementedException();
        }

        private MessageHeader ReadHeader(ReadOnlyMemory<byte> buffer)
        {
            var header = new MessageHeader();
            var offset = 0;
            var span = buffer.Span;
            header.Fin = (span[offset] & 0x80) != 0;
            bool reservedSet = (span[offset] & 0x70) != 0;
            header.Opcode = (span[offset] & 0xF);
            offset++;

            bool masked = (span[offset] & 0x80) != 0;
            header.PayloadLength = span[offset] & 0x7F;
            offset++;

            if (header.PayloadLength == 126)
            {
                header.PayloadLength = (span[offset] << 8) | span[offset + 1];
                offset += 2;
            }
            else if (header.PayloadLength == 127)
            {
                header.PayloadLength = 0;
                for (int i = 0; i < 8; i++)
                {
                    header.PayloadLength = (header.PayloadLength << 8) | span[2 + i];
                }
                offset += 8;
            }

            if (!masked)
            {
                throw new InvalidOperationException("Should be masked for now");
            }

            header.Mask = BitConverter.ToInt32(span.Slice(offset));
            offset += 4;

            _payloadLength = header.PayloadLength;

            return header;
        }

        private static unsafe int ApplyMask(Span<byte> toMask, int mask, int maskIndex)
        {
            Debug.Assert(maskIndex < sizeof(int));

            int maskShift = maskIndex * 8;
            int shiftedMask = (int)(((uint)mask >> maskShift) | ((uint)mask << (32 - maskShift)));

            // Try to use SIMD.  We can if the number of bytes we're trying to mask is at least as much
            // as the width of a vector and if the width is an even multiple of the mask.
            if (Vector.IsHardwareAccelerated &&
                Vector<byte>.Count % sizeof(int) == 0 &&
                toMask.Length >= Vector<byte>.Count)
            {
                Vector<byte> maskVector = Vector.AsVectorByte(new Vector<int>(shiftedMask));
                Span<Vector<byte>> toMaskVector = MemoryMarshal.Cast<byte, Vector<byte>>(toMask);
                for (int i = 0; i < toMaskVector.Length; i++)
                {
                    toMaskVector[i] ^= maskVector;
                }

                // Fall through to processing any remaining bytes that were less than a vector width.
                toMask = toMask.Slice(Vector<byte>.Count * toMaskVector.Length);
            }

            // If there are any bytes remaining (either we couldn't use vectors, or the count wasn't
            // an even multiple of the vector width), process them without vectors.
            int count = toMask.Length;
            if (count > 0)
            {
                fixed (byte* toMaskPtr = &MemoryMarshal.GetReference(toMask))
                {
                    byte* p = toMaskPtr;

                    // Try to go an int at a time if the remaining data is 4-byte aligned and there's enough remaining.
                    if (((long)p % sizeof(int)) == 0)
                    {
                        while (count >= sizeof(int))
                        {
                            count -= sizeof(int);
                            *((int*)p) ^= shiftedMask;
                            p += sizeof(int);
                        }

                        // We don't need to update the maskIndex, as its mod-4 value won't have changed.
                        // `p` points to the remainder.
                    }

                    // Process any remaining data a byte at a time.
                    if (count > 0)
                    {
                        byte* maskPtr = (byte*)&mask;
                        byte* end = p + count;
                        while (p < end)
                        {
                            *p++ ^= maskPtr[maskIndex];
                            maskIndex = (maskIndex + 1) & 3;
                        }
                    }
                }
            }

            // Return the updated index.
            return maskIndex;
        }

        private struct MessageHeader
        {
            internal int Opcode;
            internal bool Fin;
            internal long PayloadLength;
            internal int Mask;
        }
    }
}
