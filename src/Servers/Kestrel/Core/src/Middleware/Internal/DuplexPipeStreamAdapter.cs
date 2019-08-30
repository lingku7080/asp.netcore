// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal
{
    /// <summary>
    /// A helper for wrapping a Stream decorator from an <see cref="IDuplexPipe"/>.
    /// </summary>
    /// <typeparam name="TStream"></typeparam>
    internal class DuplexPipeStreamAdapter<TStream> : DuplexPipeStream, IDuplexPipe where TStream : Stream
    {
        private bool _disposed;
        private readonly object _disposeLock = new object();
        private readonly Pipe _output;
        private Task _outputTask;

        public DuplexPipeStreamAdapter(IDuplexPipe duplexPipe, Func<Stream, TStream> createStream) :
            this(duplexPipe, new StreamPipeReaderOptions(leaveOpen: true), new StreamPipeWriterOptions(leaveOpen: true), createStream)
        {
        }

        public DuplexPipeStreamAdapter(IDuplexPipe duplexPipe, StreamPipeReaderOptions readerOptions, StreamPipeWriterOptions writerOptions, Func<Stream, TStream> createStream) :
            base(duplexPipe.Input, duplexPipe.Output)
        {
            var stream = createStream(this);
            Stream = stream;
            Input = PipeReader.Create(stream, readerOptions);
            var outputOptions = new PipeOptions(pool: writerOptions.Pool,
                                                readerScheduler: PipeScheduler.Inline,
                                                writerScheduler: PipeScheduler.Inline,
                                                pauseWriterThreshold: 1,
                                                resumeWriterThreshold: 1,
                                                minimumSegmentSize: writerOptions.MinimumBufferSize,
                                                useSynchronizationContext: false);
            var pipe = new Pipe();
            // We're using a pipe here because the HTTP/2 stack in Kestrel currently makes assumptions	
            // about when it is ok to write to the PipeWriter. This should be reverted back to PipeWriter.Create once	
            // those patterns are fixed.	
            _output = new Pipe(outputOptions);
        }

        public PipeWriter Output
        {
            get
            {
                if (_outputTask == null)
                {
                    RunAsync();
                }

                return _output.Writer;
            }
        }

        public void RunAsync()
        {
            _outputTask = WriteOutputAsync();
        }

        private async Task WriteOutputAsync()
        {
            try
            {
                while (true)
                {
                    var result = await _output.Reader.ReadAsync();
                    var buffer = result.Buffer;

                    try
                    {
                        if (buffer.IsEmpty)
                        {
                            if (result.IsCompleted)
                            {
                                break;
                            }

                            await Stream.FlushAsync();
                        }
                        else if (buffer.IsSingleSegment)
                        {
                            await Stream.WriteAsync(buffer.First);
                        }
                        else
                        {
                            foreach (var memory in buffer)
                            {
                                await Stream.WriteAsync(memory);
                            }
                        }
                    }
                    finally
                    {
                        _output.Reader.AdvanceTo(buffer.End);
                    }
                }
            }
            catch (Exception)
            {
                //Log?.LogCritical(0, ex, $"{GetType().Name}.{nameof(WriteOutputAsync)}");
            }
            finally
            {
                _output.Reader.Complete();
            }
        }

        public TStream Stream { get; }

        public PipeReader Input { get; }


        public override async ValueTask DisposeAsync()
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
            }

            await Output.CompleteAsync();

            if (_outputTask == null)
            {
                return;
            }

            if (_outputTask != null)
            {
                await _outputTask;
            }

            await Input.CompleteAsync();
        }

        protected override void Dispose(bool disposing)
        {
            throw new NotSupportedException();
        }
    }
}

