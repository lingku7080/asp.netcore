// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace Microsoft.AspNetCore.Components.Server.Circuits
{
    internal class RemoteJSRuntime : JSRuntime
    {
        private readonly CircuitOptions _options;
        private readonly ILogger<RemoteJSRuntime> _logger;
        private CircuitClientProxy _clientProxy;

        public RemoteJSRuntime(IOptions<CircuitOptions> options, ILogger<RemoteJSRuntime> logger)
        {
            _options = options.Value;
            _logger = logger;
            DefaultAsyncTimeout = _options.JSInteropDefaultCallTimeout;
            JsonSerializerOptions.Converters.Add(new ElementReferenceJsonConverter());
        }

        internal void Initialize(CircuitClientProxy clientProxy)
        {
            _clientProxy = clientProxy ?? throw new ArgumentNullException(nameof(clientProxy));
        }

        protected override void EndInvokeDotNet(JSCallInfo callInfo, in DotNetInvocationResult result)
        {
            if (!result.Success)
            {
                Log.InvokeDotNetMethodException(_logger, callInfo, result.Exception);
                string failureText;
                if (_options.DetailedErrors)
                {
                    failureText = result.Exception.ToString();
                }
                else
                {
                    failureText = $"There was an exception invoking '{callInfo.MethodIdentifier}'. For more details turn on " +
                        $"detailed exceptions in '{nameof(CircuitOptions)}.{nameof(CircuitOptions.DetailedErrors)}'";
                }

                EndInvokeDotNetCore(callInfo, success: false, failureText);
            }
            else
            {
                Log.InvokeDotNetMethodSuccess(_logger, callInfo);
                EndInvokeDotNetCore(callInfo, success: true, result.Result);
            }
        }

        private void EndInvokeDotNetCore(in JSCallInfo callInfo, bool success, object resultOrException)
        {
            _clientProxy.SendAsync(
                "JS.EndInvokeDotNet",
                JsonSerializer.Serialize(new[] { callInfo.CallId, success, resultOrException }, JsonSerializerOptions));
        }

        protected override void BeginInvokeJS(long asyncHandle, string identifier, string argsJson)
        {
            if (!_clientProxy.Connected)
            {
                throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time. This is because the component is being " +
                    "prerendered and the page has not yet loaded in the browser or because the circuit is currently disconnected. " +
                    "Components must wrap any JavaScript interop calls in conditional logic to ensure those interop calls are not " +
                    "attempted during prerendering or while the client is disconnected.");
            }

            Log.BeginInvokeJS(_logger, asyncHandle, identifier);

            _clientProxy.SendAsync("JS.BeginInvokeJS", asyncHandle, identifier, argsJson);
        }

        public static class Log
        {
            private static readonly Action<ILogger, long, string, Exception> _beginInvokeJS =
                LoggerMessage.Define<long, string>(
                    LogLevel.Debug,
                    new EventId(1, "BeginInvokeJS"),
                    "Begin invoke JS interop '{AsyncHandle}': '{FunctionIdentifier}'");

            private static readonly Action<ILogger, string, string, long?, Exception> _invokeStaticDotNetMethodException =
                LoggerMessage.Define<string, string, long?>(
                    LogLevel.Debug,
                    new EventId(2, "InvokeDotNetMethodException"),
                    "There was an error invoking the static method '[{AssemblyName}]::{MethodIdentifier}' with callback id '{CallbackId}'.");

            private static readonly Action<ILogger, string, long, long?, Exception> _invokeInstanceDotNetMethodException =
                LoggerMessage.Define<string, long, long?>(
                    LogLevel.Debug,
                    new EventId(2, "InvokeDotNetMethodException"),
                    "There was an error invoking the instance method '{MethodIdentifier}' on reference '{DotNetObjectReference}' with callback id '{CallbackId}'.");

            private static readonly Action<ILogger, string, string, long?, Exception> _invokeStaticDotNetMethodSuccess =
                LoggerMessage.Define<string, string, long?>(
                    LogLevel.Debug,
                    new EventId(3, "InvokeDotNetMethodSuccess"),
                    "Invocation of '[{AssemblyName}]::{MethodIdentifier}' with callback id '{CallbackId}' completed successfully.");

            private static readonly Action<ILogger, string, long, long?, Exception> _invokeInstanceDotNetMethodSuccess =
                LoggerMessage.Define<string, long, long?>(
                    LogLevel.Debug,
                    new EventId(3, "InvokeDotNetMethodSuccess"),
                    "Invocation of '{MethodIdentifier}' on reference '{DotNetObjectReference}' with callback id '{CallbackId}' completed successfully.");


            internal static void BeginInvokeJS(ILogger logger, long asyncHandle, string identifier) =>
                _beginInvokeJS(logger, asyncHandle, identifier, null);

            internal static void InvokeDotNetMethodException(ILogger logger, in JSCallInfo callInfo, Exception exception)
            {
                var assemblyName = callInfo.AssemblyName;
                if (assemblyName != null)
                {
                    _invokeStaticDotNetMethodException(logger, assemblyName, callInfo.MethodIdentifier, callInfo.CallId, exception);
                }
                else
                {
                    _invokeInstanceDotNetMethodException(logger, callInfo.MethodIdentifier, callInfo.DotNetObjectId.Value, callInfo.CallId, exception);
                }
            }

            internal static void InvokeDotNetMethodSuccess(ILogger logger, in JSCallInfo callInfo)
            {
                var assemblyName = callInfo.AssemblyName;
                if (assemblyName != null)
                {
                    _invokeStaticDotNetMethodSuccess(logger, assemblyName, callInfo.MethodIdentifier, callInfo.CallId, null);
                }
                else
                {
                    _invokeInstanceDotNetMethodSuccess(logger, callInfo.MethodIdentifier, callInfo.DotNetObjectId.Value, callInfo.CallId, null);
                }

            }
        }
    }
}
