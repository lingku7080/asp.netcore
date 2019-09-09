// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Internal;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.SignalR.Protocol
{
    /// <summary>
    /// Implements the SignalR Hub Protocol using MessagePack.
    /// </summary>
    public class MessagePackHubProtocol : IHubProtocol
    {
        private const int ErrorResult = 1;
        private const int VoidResult = 2;
        private const int NonVoidResult = 3;

        private IFormatterResolver _resolver;

        private static readonly string ProtocolName = "messagepack";
        private static readonly int ProtocolVersion = 1;

        /// <inheritdoc />
        public string Name => ProtocolName;

        /// <inheritdoc />
        public int Version => ProtocolVersion;

        /// <inheritdoc />
        public TransferFormat TransferFormat => TransferFormat.Binary;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackHubProtocol"/> class.
        /// </summary>
        public MessagePackHubProtocol()
            : this(Options.Create(new MessagePackHubProtocolOptions()))
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePackHubProtocol"/> class.
        /// </summary>
        /// <param name="options">The options used to initialize the protocol.</param>
        public MessagePackHubProtocol(IOptions<MessagePackHubProtocolOptions> options)
        {
            var msgPackOptions = options.Value;
            SetupResolver(msgPackOptions);
        }

        private void SetupResolver(MessagePackHubProtocolOptions options)
        {
            // if counts don't match then we know users customized resolvers so we set up the options
            // with the provided resolvers
            if (options.FormatterResolvers.Count != SignalRResolver.Resolvers.Count)
            {
                _resolver = new CombinedResolvers(options.FormatterResolvers);
                return;
            }

            for (var i = 0; i < options.FormatterResolvers.Count; i++)
            {
                // check if the user customized the resolvers
                if (options.FormatterResolvers[i] != SignalRResolver.Resolvers[i])
                {
                    _resolver = new CombinedResolvers(options.FormatterResolvers);
                    return;
                }
            }

            // Use optimized cached resolver if the default is chosen
            _resolver = SignalRResolver.Instance;
        }

        /// <inheritdoc />
        public bool IsVersionSupported(int version)
        {
            return version == Version;
        }

        /// <inheritdoc />
        public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, out HubMessage message)
        {
            if (!BinaryMessageParser.TryParseMessage(ref input, out var payload))
            {
                message = null;
                return false;
            }

            //var arraySegment = GetArraySegment(payload);

            message = ParseMessage(in payload, binder, _resolver);
            return true;
        }

        private static ArraySegment<byte> GetArraySegment(in ReadOnlySequence<byte> input)
        {
            if (input.IsSingleSegment)
            {
                var isArray = MemoryMarshal.TryGetArray(input.First, out var arraySegment);
                // This will never be false unless we started using un-managed buffers
                Debug.Assert(isArray);
                return arraySegment;
            }

            // Should be rare
            return new ArraySegment<byte>(input.ToArray());
        }

        private static HubMessage ParseMessage(in ReadOnlySequence<byte> input, IInvocationBinder binder, IFormatterResolver resolver)
        {
            var reader = new MessagePackReader(input);
            var itemCount = reader.ReadArrayHeader();

            var messageType = reader.ReadInt32();

            switch (messageType)
            {
                case HubProtocolConstants.InvocationMessageType:
                    return CreateInvocationMessage(ref reader, binder, resolver, itemCount);
                case HubProtocolConstants.StreamInvocationMessageType:
                    return CreateStreamInvocationMessage(ref reader, binder, resolver, itemCount);
                case HubProtocolConstants.StreamItemMessageType:
                    return CreateStreamItemMessage(ref reader, binder, resolver);
                case HubProtocolConstants.CompletionMessageType:
                    return CreateCompletionMessage(ref reader, binder, resolver);
                case HubProtocolConstants.CancelInvocationMessageType:
                    return CreateCancelInvocationMessage(ref reader);
                case HubProtocolConstants.PingMessageType:
                    return PingMessage.Instance;
                case HubProtocolConstants.CloseMessageType:
                    return CreateCloseMessage(ref reader);
                default:
                    // Future protocol changes can add message types, old clients can ignore them
                    return null;
            }
        }

        private static HubMessage CreateInvocationMessage(ref MessagePackReader reader, IInvocationBinder binder, IFormatterResolver resolver, int itemCount)
        {
            var headers = ReadHeaders(ref reader);
            //var invocationId = ReadInvocationId(input, ref offset);
            var invocationId = reader.ReadString();

            // For MsgPack, we represent an empty invocation ID as an empty string,
            // so we need to normalize that to "null", which is what indicates a non-blocking invocation.
            if (string.IsNullOrEmpty(invocationId))
            {
                invocationId = null;
            }

            //var target = ReadString(input, ref offset, "target");
            var target = reader.ReadString();

            object[] arguments = null;
            try
            {
                var parameterTypes = binder.GetParameterTypes(target);
                arguments = BindArguments(ref reader, parameterTypes, resolver);
            }
            catch (Exception ex)
            {
                return new InvocationBindingFailureMessage(invocationId, target, ExceptionDispatchInfo.Capture(ex));
            }

            string[] streams = null;
            // Previous clients will send 5 items, so we check if they sent a stream array or not
            if (itemCount > 5)
            {
                streams = ReadStreamIds(ref reader);
            }

            return ApplyHeaders(headers, new InvocationMessage(invocationId, target, arguments, streams));
        }

        private static HubMessage CreateStreamInvocationMessage(ref MessagePackReader reader, IInvocationBinder binder, IFormatterResolver resolver, int itemCount)
        {
            var headers = ReadHeaders(ref reader);
            //var invocationId = ReadInvocationId(input, ref offset);
            var invocationId = reader.ReadString();
            //var target = ReadString(input, ref offset, "target");
            var target = reader.ReadString();

            object[] arguments = null;
            try
            {
                var parameterTypes = binder.GetParameterTypes(target);
                arguments = BindArguments(ref reader, parameterTypes, resolver);
            }
            catch (Exception ex)
            {
                return new InvocationBindingFailureMessage(invocationId, target, ExceptionDispatchInfo.Capture(ex));
            }

            string[] streams = null;
            // Previous clients will send 5 items, so we check if they sent a stream array or not
            if (itemCount > 5)
            {
                streams = ReadStreamIds(ref reader);
            }

            return ApplyHeaders(headers, new StreamInvocationMessage(invocationId, target, arguments, streams));
        }

        private static HubMessage CreateStreamItemMessage(ref MessagePackReader reader, IInvocationBinder binder, IFormatterResolver resolver)
        {
            var headers = ReadHeaders(ref reader);
            //var invocationId = ReadInvocationId(input, ref offset);
            var invocationId = reader.ReadString();
            object value;
            try
            {
                var itemType = binder.GetStreamItemType(invocationId);
                value = DeserializeObject(ref reader, itemType, "item", resolver);
            }
            catch (Exception ex)
            {
                return new StreamBindingFailureMessage(invocationId, ExceptionDispatchInfo.Capture(ex));
            }

            return ApplyHeaders(headers, new StreamItemMessage(invocationId, value));
        }

        private static CompletionMessage CreateCompletionMessage(ref MessagePackReader reader, IInvocationBinder binder, IFormatterResolver resolver)
        {
            var headers = ReadHeaders(ref reader);
            //var invocationId = ReadInvocationId(input, ref offset);
            var invocationId = reader.ReadString();
            //var resultKind = ReadInt32(input, ref offset, "resultKind");
            var resultKind = reader.ReadInt32();

            string error = null;
            object result = null;
            var hasResult = false;

            switch (resultKind)
            {
                case ErrorResult:
                    //error = ReadString(input, ref offset, "error");
                    error = reader.ReadString();
                    break;
                case NonVoidResult:
                    var itemType = binder.GetReturnType(invocationId);
                    result = DeserializeObject(ref reader, itemType, "argument", resolver);
                    hasResult = true;
                    break;
                case VoidResult:
                    hasResult = false;
                    break;
                default:
                    throw new InvalidDataException("Invalid invocation result kind.");
            }

            return ApplyHeaders(headers, new CompletionMessage(invocationId, error, result, hasResult));
        }

        private static CancelInvocationMessage CreateCancelInvocationMessage(ref MessagePackReader reader)
        {
            var headers = ReadHeaders(ref reader);
            //var invocationId = ReadInvocationId(input, ref offset);
            var invocationId = reader.ReadString();
            return ApplyHeaders(headers, new CancelInvocationMessage(invocationId));
        }

        private static CloseMessage CreateCloseMessage(ref MessagePackReader reader)
        {
            //var error = ReadString(input, ref offset, "error");
            var error = reader.ReadString();
            return new CloseMessage(error);
        }

        private static Dictionary<string, string> ReadHeaders(ref MessagePackReader reader)
        {
            //var headerCount = ReadMapLength(input, ref offset, "headers");
            var headerCount = reader.ReadMapHeader();
            if (headerCount > 0)
            {
                var headers = new Dictionary<string, string>(StringComparer.Ordinal);

                for (var i = 0; i < headerCount; i++)
                {
                    //var key = ReadString(input, ref offset, $"headers[{i}].Key");
                    //var value = ReadString(input, ref offset, $"headers[{i}].Value");
                    var key = reader.ReadString();
                    var value = reader.ReadString();
                    headers.Add(key, value);
                }
                return headers;
            }
            else
            {
                return null;
            }
        }

        private static string[] ReadStreamIds(ref MessagePackReader reader)
        {
            //var streamIdCount = ReadArrayLength(input, ref offset, "streamIds");
            var streamIdCount = reader.ReadArrayHeader();
            List<string> streams = null;

            if (streamIdCount > 0)
            {
                streams = new List<string>();
                for (var i = 0; i < streamIdCount; i++)
                {
                    streams.Add(reader.ReadString());
                    //streams.Add(MessagePackBinary.ReadString(input, offset, out var read));
                    //offset += read;
                }
            }

            return streams?.ToArray();
        }

        private static object[] BindArguments(ref MessagePackReader reader, IReadOnlyList<Type> parameterTypes, IFormatterResolver resolver)
        {
            //var argumentCount = ReadArrayLength(input, ref offset, "arguments");
            var argumentCount = reader.ReadArrayHeader();

            if (parameterTypes.Count != argumentCount)
            {
                throw new InvalidDataException(
                    $"Invocation provides {argumentCount} argument(s) but target expects {parameterTypes.Count}.");
            }

            try
            {
                var arguments = new object[argumentCount];
                for (var i = 0; i < argumentCount; i++)
                {
                    arguments[i] = DeserializeObject(ref reader, parameterTypes[i], "argument", resolver);
                }

                return arguments;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Error binding arguments. Make sure that the types of the provided values match the types of the hub method being invoked.", ex);
            }
        }

        private static T ApplyHeaders<T>(IDictionary<string, string> source, T destination) where T : HubInvocationMessage
        {
            if (source != null && source.Count > 0)
            {
                destination.Headers = source;
            }

            return destination;
        }

        /// <inheritdoc />
        public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
        {
            var memoryWriter = MemoryBufferWriter.Get();

            try
            {
                var writer = new MessagePackWriter(output);
                // Write message to a buffer so we can get its length
                WriteMessageCore(message, ref writer);

                // Write length then message to output
                BinaryMessageFormatter.WriteLengthPrefix(memoryWriter.Length, output);
                memoryWriter.CopyTo(output);
            }
            finally
            {
                MemoryBufferWriter.Return(memoryWriter);
            }
        }

        /// <inheritdoc />
        public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
        {
            var memoryWriter = MemoryBufferWriter.Get();

            try
            {
                var writer = new MessagePackWriter();
                // Write message to a buffer so we can get its length
                WriteMessageCore(message, ref writer);

                var dataLength = memoryWriter.Length;
                var prefixLength = BinaryMessageFormatter.LengthPrefixLength(memoryWriter.Length);

                var array = new byte[dataLength + prefixLength];
                var span = array.AsSpan();

                // Write length then message to output
                var written = BinaryMessageFormatter.WriteLengthPrefix(memoryWriter.Length, span);
                Debug.Assert(written == prefixLength);
                memoryWriter.CopyTo(span.Slice(prefixLength));

                return array;
            }
            finally
            {
                MemoryBufferWriter.Return(memoryWriter);
            }
        }

        private void WriteMessageCore(HubMessage message, ref MessagePackWriter writer)
        {
            switch (message)
            {
                case InvocationMessage invocationMessage:
                    WriteInvocationMessage(invocationMessage, ref writer);
                    break;
                case StreamInvocationMessage streamInvocationMessage:
                    WriteStreamInvocationMessage(streamInvocationMessage, ref writer);
                    break;
                case StreamItemMessage streamItemMessage:
                    WriteStreamingItemMessage(streamItemMessage, ref writer);
                    break;
                case CompletionMessage completionMessage:
                    WriteCompletionMessage(completionMessage, ref writer);
                    break;
                case CancelInvocationMessage cancelInvocationMessage:
                    WriteCancelInvocationMessage(cancelInvocationMessage, ref writer);
                    break;
                case PingMessage pingMessage:
                    WritePingMessage(pingMessage, ref writer);
                    break;
                case CloseMessage closeMessage:
                    WriteCloseMessage(closeMessage, ref writer);
                    break;
                default:
                    throw new InvalidDataException($"Unexpected message type: {message.GetType().Name}");
            }
        }

        private void WriteInvocationMessage(InvocationMessage message, ref MessagePackWriter writer)
        {
            writer.WriteArrayHeader(6);

            writer.WriteInt32(HubProtocolConstants.InvocationMessageType);
            PackHeaders(ref writer, message.Headers);
            if (string.IsNullOrEmpty(message.InvocationId))
            {
                writer.WriteNil();
            }
            else
            {
                writer.WriteString(Encoding.UTF8.GetBytes(message.InvocationId));
            }
            writer.WriteString(Encoding.UTF8.GetBytes(message.Target));
            writer.WriteArrayHeader(message.Arguments.Length);
            foreach (var arg in message.Arguments)
            {
                WriteArgument(arg, ref writer);
            }

            WriteStreamIds(message.StreamIds, ref writer);
        }

        private void WriteStreamInvocationMessage(StreamInvocationMessage message, ref MessagePackWriter writer)
        {
            writer.WriteArrayHeader(6);

            writer.WriteInt16(HubProtocolConstants.StreamInvocationMessageType);
            PackHeaders(ref writer, message.Headers);
            writer.WriteString(Encoding.UTF8.GetBytes(message.InvocationId));
            writer.WriteString(Encoding.UTF8.GetBytes(message.Target));

            writer.WriteArrayHeader(message.Arguments.Length);
            foreach (var arg in message.Arguments)
            {
                WriteArgument(arg, ref writer);
            }

            WriteStreamIds(message.StreamIds, ref writer);
        }

        private void WriteStreamingItemMessage(StreamItemMessage message, ref MessagePackWriter writer)
        {
            writer.WriteArrayHeader(4);
            writer.WriteInt16(HubProtocolConstants.StreamItemMessageType);
            PackHeaders(ref writer, message.Headers);
            writer.WriteString(Encoding.UTF8.GetBytes(message.InvocationId));
            WriteArgument(message.Item, ref writer);
        }

        private void WriteArgument(object argument, ref MessagePackWriter writer)
        {
            if (argument == null)
            {
                writer.WriteNil();
            }
            else
            {
                // TODO
                MessagePackSerializer.NonGeneric.Serialize(argument.GetType(), stream, argument, _resolver);
            }
        }

        private void WriteStreamIds(string[] streamIds, ref MessagePackWriter writer)
        {
            if (streamIds != null)
            {
                writer.WriteArrayHeader(streamIds.Length);
                foreach (var streamId in streamIds)
                {
                    writer.WriteString(Encoding.UTF8.GetBytes(streamId));
                }
            }
            else
            {
                writer.WriteArrayHeader(0);
            }
        }

        private void WriteCompletionMessage(CompletionMessage message, ref MessagePackWriter writer)
        {
            var resultKind =
                message.Error != null ? ErrorResult :
                message.HasResult ? NonVoidResult :
                VoidResult;

            writer.WriteArrayHeader(4 + (resultKind != VoidResult ? 1 : 0));
            writer.WriteInt32(HubProtocolConstants.CompletionMessageType);
            PackHeaders(ref writer, message.Headers);
            writer.WriteString(Encoding.UTF8.GetBytes(message.InvocationId));
            writer.WriteInt32(resultKind);
            switch (resultKind)
            {
                case ErrorResult:
                    writer.WriteString(Encoding.UTF8.GetBytes(message.Error));
                    break;
                case NonVoidResult:
                    WriteArgument(message.Result, ref writer);
                    break;
            }
        }

        private void WriteCancelInvocationMessage(CancelInvocationMessage message, ref MessagePackWriter writer)
        {
            writer.WriteArrayHeader(3);
            writer.WriteInt16(HubProtocolConstants.CancelInvocationMessageType);
            PackHeaders(ref writer, message.Headers);
            writer.WriteString(Encoding.UTF8.GetBytes(message.InvocationId));
        }

        private void WriteCloseMessage(CloseMessage message, ref MessagePackWriter writer)
        {
            writer.WriteArrayHeader(2);
            //MessagePackBinary.WriteArrayHeader(packer, 2);
            //MessagePackBinary.WriteInt16(packer, HubProtocolConstants.CloseMessageType);
            writer.WriteInt16(HubProtocolConstants.CloseMessageType);
            if (string.IsNullOrEmpty(message.Error))
            {
                //MessagePackBinary.WriteNil(packer);
                writer.WriteNil();
            }
            else
            {
                //MessagePackBinary.WriteString(packer, message.Error);
                writer.WriteString(Encoding.UTF8.GetBytes(message.Error));
            }
        }

        private void WritePingMessage(PingMessage pingMessage, ref MessagePackWriter writer)
        {
            writer.WriteArrayHeader(1);
            writer.WriteInt32(HubProtocolConstants.PingMessageType);
        }

        private void PackHeaders(ref MessagePackWriter writer, IDictionary<string, string> headers)
        {
            if (headers != null)
            {
                writer.WriteMapHeader(headers.Count);
                if (headers.Count > 0)
                {
                    foreach (var header in headers)
                    {
                        writer.WriteString(Encoding.UTF8.GetBytes(header.Key));
                        writer.WriteString(Encoding.UTF8.GetBytes(header.Value));
                    }
                }
            }
            else
            {
                writer.WriteMapHeader(0);
            }
        }

        //private static string ReadInvocationId(byte[] input, ref int offset)
        //{
        //    return ReadString(input, ref offset, "invocationId");
        //}

        //private static int ReadInt32(byte[] input, ref int offset, string field)
        //{
        //    Exception msgPackException = null;
        //    try
        //    {
        //        var readInt = MessagePackBinary.ReadInt32(input, offset, out var readSize);
        //        offset += readSize;
        //        return readInt;
        //    }
        //    catch (Exception e)
        //    {
        //        msgPackException = e;
        //    }

        //    throw new InvalidDataException($"Reading '{field}' as Int32 failed.", msgPackException);
        //}

        //private static string ReadString(byte[] input, ref int offset, string field)
        //{
        //    Exception msgPackException = null;
        //    try
        //    {
        //        var readString = MessagePackBinary.ReadString(input, offset, out var readSize);
        //        offset += readSize;
        //        return readString;
        //    }
        //    catch (Exception e)
        //    {
        //        msgPackException = e;
        //    }

        //    throw new InvalidDataException($"Reading '{field}' as String failed.", msgPackException);
        //}

        //private static long ReadMapLength(byte[] input, ref int offset, string field)
        //{
        //    Exception msgPackException = null;
        //    try
        //    {
        //        var readMap = MessagePackBinary.ReadMapHeader(input, offset, out var readSize);
        //        offset += readSize;
        //        return readMap;
        //    }
        //    catch (Exception e)
        //    {
        //        msgPackException = e;
        //    }

        //    throw new InvalidDataException($"Reading map length for '{field}' failed.", msgPackException);
        //}

        //private static long ReadArrayLength(byte[] input, ref int offset, string field)
        //{
        //    Exception msgPackException = null;
        //    try
        //    {
        //        var readArray = MessagePackBinary.ReadArrayHeader(input, offset, out var readSize);
        //        offset += readSize;
        //        return readArray;
        //    }
        //    catch (Exception e)
        //    {
        //        msgPackException = e;
        //    }

        //    throw new InvalidDataException($"Reading array length for '{field}' failed.", msgPackException);
        //}

        private static object DeserializeObject(ref MessagePackReader reader, Type type, string field, IFormatterResolver resolver)
        {
            Exception msgPackException = null;
            try
            {
                // TODO
                var obj = MessagePackSerializer.NonGeneric.Deserialize(type, new ArraySegment<byte>(input, offset, input.Length - offset), resolver);
                offset += MessagePackBinary.ReadNextBlock(input, offset);
                return obj;
            }
            catch (Exception ex)
            {
                msgPackException = ex;
            }

            throw new InvalidDataException($"Deserializing object of the `{type.Name}` type for '{field}' failed.", msgPackException);
        }

        internal static List<IFormatterResolver> CreateDefaultFormatterResolvers()
        {
            // Copy to allow users to add/remove resolvers without changing the static SignalRResolver list
            return new List<IFormatterResolver>(SignalRResolver.Resolvers);
        }

        internal class SignalRResolver : IFormatterResolver
        {
            public static readonly IFormatterResolver Instance = new SignalRResolver();

            public static readonly IList<IFormatterResolver> Resolvers = new IFormatterResolver[]
            {
                MessagePack.Resolvers.DynamicEnumAsStringResolver.Instance,
                MessagePack.Resolvers.ContractlessStandardResolver.Instance,
            };

            public IMessagePackFormatter<T> GetFormatter<T>()
            {
                return Cache<T>.Formatter;
            }

            private static class Cache<T>
            {
                public static readonly IMessagePackFormatter<T> Formatter;

                static Cache()
                {
                    foreach (var resolver in Resolvers)
                    {
                        Formatter = resolver.GetFormatter<T>();
                        if (Formatter != null)
                        {
                            return;
                        }
                    }
                }
            }
        }

        // Support for users making their own Formatter lists
        internal class CombinedResolvers : IFormatterResolver
        {
            private readonly IList<IFormatterResolver> _resolvers;

            public CombinedResolvers(IList<IFormatterResolver> resolvers)
            {
                _resolvers = resolvers;
            }

            public IMessagePackFormatter<T> GetFormatter<T>()
            {
                foreach (var resolver in _resolvers)
                {
                    var formatter = resolver.GetFormatter<T>();
                    if (formatter != null)
                    {
                        return formatter;
                    }
                }

                return null;
            }
        }
    }
}
