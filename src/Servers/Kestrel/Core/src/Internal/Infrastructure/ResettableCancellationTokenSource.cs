// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO.Pipelines;
using System.Threading;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure
{
    internal class ResettableCancellationTokenSource
    {
        private readonly PipeScheduler _scheduler;
        private readonly IKestrelTrace _trace;
        private readonly string _connectionId;

        private readonly object _cancellationLock = new object();
        private CancellationState _cancellationState;
        private bool _preventCancellation;
        private CancellationTokenSource _backingCts;
        private CancellationToken? _manuallySetToken;

        public ResettableCancellationTokenSource(PipeScheduler scheduler, IKestrelTrace trace, string connectionId)
        {
            _scheduler = scheduler;
            _trace = trace;
            _connectionId = connectionId;
        }

        public CancellationToken Token
        {
            get
            {
                // If a request abort token was previously explicitly set, return it.
                if (_manuallySetToken.HasValue)
                {
                    return _manuallySetToken.Value;
                }

                lock (_cancellationLock)
                {
                    if (_preventCancellation)
                    {
                        return new CancellationToken(false);
                    }

                    if (_backingCts == null)
                    {
                        _backingCts = new CancellationTokenSource();

                        if (_cancellationState == CancellationState.Canceled)
                        {
                            // TODO: What if _cancellationState is disposed?

                            // Only cancel newly-created CTSs that we know cannot have any registrations yet.
                            // CTSs created previously will be canceled by CancelInternal().
                            _backingCts.Cancel();
                        }
                    }

                    return _backingCts.Token;
                }
            }
            set
            {
                // Set a token overriding one we create internally. This setter and associated
                // field exist purely to support IHttpRequestLifetimeFeature.set_RequestAborted.
                _manuallySetToken = value;
            }
        }

        public void Cancel()
        {
            lock (_cancellationLock)
            {
                if (_cancellationState != CancellationState.NotCanceled)
                {
                    return;
                }


                if (_backingCts != null)
                {
                    // Potentially calling user code. CancelInternal() logs any exceptions.
                    // This allocates a closure. Don't bother with a state object since it
                    // would allocate anyway.
                    _cancellationState = CancellationState.PendingCancellation;
                    _scheduler.Schedule(_ => CancelInternal(_backingCts), null);
                }
            }
        }

        // Prevents the token from firing until the next call to Reset().
        public void PreventCancellation()
        {
            lock (_cancellationLock)
            {
                if (_cancellationState != CancellationState.NotCanceled)
                {
                    return;
                }

                _preventCancellation = true;
            }
        }

        public void Reset()
        {
            _manuallySetToken = null;
            _preventCancellation = false;

            // Lock to prevent CancelInternal from attempting to cancel an disposed CTS.
            lock (_cancellationLock)
            {
                if (_backingCts != null)
                {
                    if (_cancellationState != CancellationState.NotCanceled)
                    {
                        _cancellationState = CancellationState.Disposed;
                    }

                    _scheduler.Schedule(state => ((CancellationTokenSource)state).Dispose(), _backingCts);
                    _backingCts = null;
                }
            }
        }

        private void CancelInternal(CancellationTokenSource cts)
        {
            lock (_cancellationLock)
            {
                // If _backingCts is null, Dispose has already been scheduled so don't cancel.
                if (_cancellationState == CancellationState.Disposed)
                {
                    return;
                }
            }

            try
            {
                cts.Cancel();
            }
            catch (Exception ex)
            {
                _trace.ApplicationError(_connectionId, traceIdentifier: null, ex: ex);
            }
        }

        private enum CancellationState
        {
            NotCanceled,
            PendingCancellation,
            Canceled,
            Disposed
        }
    }
}
