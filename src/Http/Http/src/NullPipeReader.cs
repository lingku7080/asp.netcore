// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Pipelines
{
    internal class NullPipeReader : PipeReader
    {
        public override void AdvanceTo(SequencePosition consumed)
        {
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
        }

        public override void CancelPendingRead()
        {
        }

        public override void Complete(Exception exception = null)
        {
        }

        public override void OnWriterCompleted(Action<Exception, object> callback, object state)
        {
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            return new ValueTask<ReadResult>(CompletedReadResult());
        }

        public override bool TryRead(out ReadResult result)
        {
            result = CompletedReadResult();
            return true;
        }

        private ReadResult CompletedReadResult()
        {
            return new ReadResult(new ReadOnlySequence<byte>(), isCanceled: false, isCompleted: true);
        }
    }
}
