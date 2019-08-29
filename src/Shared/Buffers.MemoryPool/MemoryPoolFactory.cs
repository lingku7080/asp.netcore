// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace System.Buffers
{
    internal static class SlabMemoryPoolFactory
    {
        public static MemoryPool<byte> Create()
        {
            return new DiagnosticMemoryPool(CreateSlabMemoryPool());
        }

        public static MemoryPool<byte> CreateSlabMemoryPool()
        {
            return new SlabMemoryPool();
        }
    }
}
