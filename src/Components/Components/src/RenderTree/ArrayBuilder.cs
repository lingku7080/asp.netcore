// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.AspNetCore.Components.RenderTree
{
    /// <summary>
    /// Implements a list that uses an array of objects to store the elements.
    /// 
    /// This differs from a <see cref="System.Collections.Generic.List{T}"/> in that
    /// it not only grows as required but also shrinks if cleared with significant
    /// excess capacity. This makes it useful for component rendering, because
    /// components can be long-lived and re-render frequently, with the rendered size
    /// varying dramatically depending on the user's navigation in the app.
    /// </summary>
    internal class ArrayBuilder<T> : IDisposable
    {
        private static readonly T[] Empty = Array.Empty<T>();
        private readonly ArrayPool<T> _arrayPool;
        private readonly int _minCapacity;
        private bool _disposed;

        /// <summary>
        /// Constructs a new instance of <see cref="ArrayBuilder{T}"/>.
        /// </summary>
        public ArrayBuilder(int minCapacity = 32, ArrayPool<T> arrayPool = null)
        {
            _arrayPool = arrayPool ?? ArrayPool<T>.Shared;
            _minCapacity = minCapacity;
            Buffer = Empty;
        }

        /// <summary>
        /// Gets the number of items.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Gets the underlying buffer.
        /// </summary>
        public T[] Buffer { get; private set; }

        /// <summary>
        /// Appends a new item, automatically resizing the underlying array if necessary.
        /// </summary>
        /// <param name="item">The item to append.</param>
        /// <returns>The index of the appended item.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // Just like System.Collections.Generic.List<T>
        public int Append(in T item)
        {
            if (Count == Buffer.Length)
            {
                GrowBuffer(Buffer.Length * 2);
            }

            var indexOfAppendedItem = Count++;
            Buffer[indexOfAppendedItem] = item;
            return indexOfAppendedItem;
        }

        internal int Append(T[] source, int startIndex, int length)
        {
            // Expand storage if needed. Using same doubling approach as would
            // be used if you inserted the items one-by-one.
            var requiredCapacity = Count + length;
            if (Buffer.Length < requiredCapacity)
            {
                var candidateCapacity = Math.Max(Buffer.Length * 2, _minCapacity);
                while (candidateCapacity < requiredCapacity)
                {
                    candidateCapacity *= 2;
                }

                GrowBuffer(candidateCapacity);
            }

            Array.Copy(source, startIndex, Buffer, Count, length);
            var startIndexOfAppendedItems = Count;
            Count += length;
            return startIndexOfAppendedItems;
        }

        /// <summary>
        /// Sets the supplied value at the specified index. The index must be within
        /// range for the array.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Overwrite(int index, in T value)
        {
            if (index > Count)
            {
                ThrowIndexOutOfBoundsException();
            }

            Buffer[index] = value;
        }

        /// <summary>
        /// Removes the last item.
        /// </summary>
        public void RemoveLast()
        {
            if (Count == 0)
            {
                ThrowIndexOutOfBoundsException();
            }

            Count--;
            Buffer[Count] = default; // Release to GC
        }

        /// <summary>
        /// Inserts the item at the specified index, moving the contents of the subsequent entries along by one.
        /// </summary>
        /// <param name="index">The index at which the value is to be inserted.</param>
        /// <param name="value">The value to insert.</param>
        public void InsertExpensive(int index, T value)
        {
            if (index > Count)
            {
                ThrowIndexOutOfBoundsException();
            }

            // Same expansion logic as elsewhere
            if (Count == Buffer.Length)
            {
                GrowBuffer(Buffer.Length * 2);
            }

            Array.Copy(Buffer, index, Buffer, index + 1, Count - index);
            Count++;

            Buffer[index] = value;
        }

        /// <summary>
        /// Marks the array as empty, also shrinking the underlying storage if it was
        /// not being used to near its full capacity.
        /// </summary>
        public void Clear()
        {
            ReturnBuffer();
            Buffer = Empty;
            Count = 0;
        }

        /// <summary>
        /// Produces an <see cref="ArrayRange{T}"/> structure describing the current contents.
        /// </summary>
        /// <returns>The <see cref="ArrayRange{T}"/>.</returns>
        public ArrayRange<T> ToRange()
            => new ArrayRange<T>(Buffer, Count);

        /// <summary>
        /// Produces an <see cref="ArrayBuilderSegment{T}"/> structure describing the selected contents.
        /// </summary>
        /// <param name="fromIndexInclusive">The index of the first item in the segment.</param>
        /// <param name="toIndexExclusive">One plus the index of the last item in the segment.</param>
        /// <returns>The <see cref="ArraySegment{T}"/>.</returns>
        public ArrayBuilderSegment<T> ToSegment(int fromIndexInclusive, int toIndexExclusive)
            => new ArrayBuilderSegment<T>(this, fromIndexInclusive, toIndexExclusive - fromIndexInclusive);

        private void GrowBuffer(int desiredCapacity)
        {
            var newCapacity = Math.Max(desiredCapacity, _minCapacity);
            Debug.Assert(newCapacity > Buffer.Length);

            var newItems = _arrayPool.Rent(newCapacity);
            Array.Copy(Buffer, newItems, Count);

            // Return the old buffer and start using the new buffer
            ReturnBuffer();
            Buffer = newItems;
        }

        private void ReturnBuffer()
        {
            if (!ReferenceEquals(Buffer, Empty))
            {
                // ArrayPool<>.Return with clearArray: true calls Array.Clear on the entire buffer.
                // In the most common case, Count would be much smaller than Buffer.Length so we'll specifically clear that subset.
                Array.Clear(Buffer, 0, Count);
                _arrayPool.Return(Buffer);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                ReturnBuffer();
            }
        }

        private static void ThrowIndexOutOfBoundsException()
        {
            throw new ArgumentOutOfRangeException("index");
        }
    }
}
