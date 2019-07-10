// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ignitor;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure.ServerFixtures;
using Microsoft.AspNetCore.E2ETesting;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Components.E2ETest.ServerExecutionTests
{
    public class InteropReliabilityTests : ServerTestBase<AspNetSiteServerFixture>
    {
        private const int DefaultLatencyTimeout = 500;

        public InteropReliabilityTests(
            BrowserFixture browserFixture,
            AspNetSiteServerFixture serverFixture,
            ITestOutputHelper output)
            : base(browserFixture, serverFixture, output)
        {
            serverFixture.BuildWebHostMethod = TestServer.Program.BuildWebHost;
        }

        public BlazorClient Client { get; set; } = new BlazorClient();

        public override Task InitializeAsync()
        {
            // Do nothing.
            return Task.CompletedTask;
        }

        [Fact]
        public async Task CannotInvokeNonJSInvokableMethods()
        {
            // Arrange
            var expectedError = "[\"1\"," +
                "false," +
                "\"There was an exception invoking \\u0027WriteAllText\\u0027 on assembly \\u0027System.IO.FileSystem\\u0027. For more details turn on detailed exceptions in \\u0027CircuitOptions.JSInteropDetailedErrors\\u0027\"]";

            var (interopCalls, batches) = ConfigureClient();
            await GoToTestComponent(batches);

            // Act
            Client.InvokeDotNetMethod(
                "1",
                "System.IO.FileSystem",
                "WriteAllText",
                null,
                JsonSerializer.Serialize(new[] { ".\\log.txt", "log" }));

            await Task.Delay(DefaultLatencyTimeout);

            // Assert
            Assert.Single(interopCalls, (0, "DotNet.jsCallDispatcher.endInvokeDotNetFromJS", expectedError));

            await ValidateClientKeepsWorking(Client, batches);
        }

        [Fact]
        public async Task CannotInvokeNonExistingMethods()
        {
            // Arrange
            var expectedError = "[\"1\"," +
                "false," +
                "\"There was an exception invoking \\u0027MadeUpMethod\\u0027 on assembly \\u0027BasicTestApp\\u0027. For more details turn on detailed exceptions in \\u0027CircuitOptions.JSInteropDetailedErrors\\u0027\"]";

            var (interopCalls, batches) = ConfigureClient();
            await GoToTestComponent(batches);

            // Act
            Client.InvokeDotNetMethod(
                "1",
                "BasicTestApp",
                "MadeUpMethod",
                null,
                JsonSerializer.Serialize(new[] { ".\\log.txt", "log" }));

            await Task.Delay(DefaultLatencyTimeout);

            // Assert
            Assert.Single(interopCalls, (0, "DotNet.jsCallDispatcher.endInvokeDotNetFromJS", expectedError));
            await ValidateClientKeepsWorking(Client, batches);
        }

        [Fact]
        public async Task CannotInvokeJSInvokableMethodsWithWrongNumberOfArguments()
        {
            // Arrange
            var expectedError = "[\"1\"," +
                "false," +
                "\"There was an exception invoking \\u0027NotifyLocationChanged\\u0027 on assembly \\u0027Microsoft.AspNetCore.Components.Server\\u0027. For more details turn on detailed exceptions in \\u0027CircuitOptions.JSInteropDetailedErrors\\u0027\"]";

            var (interopCalls, batches) = ConfigureClient();
            await GoToTestComponent(batches);

            // Act
            Client.InvokeDotNetMethod(
                "1",
                "Microsoft.AspNetCore.Components.Server",
                "NotifyLocationChanged",
                null,
                JsonSerializer.Serialize(new[] { _serverFixture.RootUri }));

            await Task.Delay(DefaultLatencyTimeout);

            // Assert
            Assert.Single(interopCalls, (0, "DotNet.jsCallDispatcher.endInvokeDotNetFromJS", expectedError));

            await ValidateClientKeepsWorking(Client, batches);
        }

        [Fact]
        public async Task CannotInvokeJSInvokableMethodsEmptyAssemblyName()
        {
            // Arrange
            var expectedError = "[\"1\"," +
                "false," +
                "\"There was an exception invoking \\u0027NotifyLocationChanged\\u0027 on assembly \\u0027\\u0027. For more details turn on detailed exceptions in \\u0027CircuitOptions.JSInteropDetailedErrors\\u0027\"]";

            var (interopCalls, batches) = ConfigureClient();
            await GoToTestComponent(batches);

            // Act
            Client.InvokeDotNetMethod(
                "1",
                "",
                "NotifyLocationChanged",
                null,
                JsonSerializer.Serialize(new object[] { _serverFixture.RootUri + "counter", false }));

            await Task.Delay(DefaultLatencyTimeout);

            // Assert
            Assert.Single(interopCalls, (0, "DotNet.jsCallDispatcher.endInvokeDotNetFromJS", expectedError));

            await ValidateClientKeepsWorking(Client, batches);
        }

        [Fact]
        public async Task CannotInvokeJSInvokableMethodsEmptyMethodName()
        {
            // Arrange
            var expectedError = "[\"1\"," +
                "false," +
                "\"There was an exception invoking \\u0027\\u0027 on assembly \\u0027Microsoft.AspNetCore.Components.Server\\u0027. For more details turn on detailed exceptions in \\u0027CircuitOptions.JSInteropDetailedErrors\\u0027\"]";

            var (interopCalls, batches) = ConfigureClient();
            await GoToTestComponent(batches);

            // Act
            Client.InvokeDotNetMethod(
                "1",
                "Microsoft.AspNetCore.Components.Server",
                "",
                null,
                JsonSerializer.Serialize(new object[] { _serverFixture.RootUri + "counter", false }));

            await Task.Delay(DefaultLatencyTimeout);

            // Assert
            Assert.Single(interopCalls, (0, "DotNet.jsCallDispatcher.endInvokeDotNetFromJS", expectedError));

            await ValidateClientKeepsWorking(Client, batches);
        }

        [Fact(Skip = "Pending changes from extensions")]
        public async Task CannotInvokeJSInvokableMethodsWithWrongReferenceId()
        {
            // Arrange
            var expectedDotNetObjectRef = "[\"1\",true,{\"__dotNetObject\":1}]";

            var (interopCalls, batches) = ConfigureClient();
            await GoToTestComponent(batches);

            // Act
            Client.InvokeDotNetMethod(
                "1",
                "BasicTestApp",
                "CreateInformation",
                null,
                JsonSerializer.Serialize(Array.Empty<object>()));

            await Task.Delay(DefaultLatencyTimeout);
            Assert.Single(interopCalls, (0, "DotNet.jsCallDispatcher.endInvokeDotNetFromJS", expectedDotNetObjectRef));

            Client.InvokeDotNetMethod(
                "1",
                null,
                "Reverse",
                1,
                JsonSerializer.Serialize(Array.Empty<object>()));

            await Task.Delay(DefaultLatencyTimeout);

            // Assert
            Assert.Single(interopCalls, (0, "DotNet.jsCallDispatcher.endInvokeDotNetFromJS", "[\"1\",true,\"egasseM\"]"));

            Client.InvokeDotNetMethod(
                "1",
                null,
                "Reverse",
                3, // non existing ref
                JsonSerializer.Serialize(Array.Empty<object>()));

            await Task.Delay(5000);
            Assert.Single(interopCalls, (0, "DotNet.jsCallDispatcher.endInvokeDotNetFromJS", "[\"1\",true,\"egasseM\"]"));

            await ValidateClientKeepsWorking(Client, batches);
        }

        [Fact]
        public async Task CannotInvokeJSInvokableMethodsWronReferenceIdType()
        {
            // Arrange
            var expectedImportantDotNetObjectRef = "[\"1\",true,{\"__dotNetObject\":1}]";
            var expectedError = "[\"1\"," +
                "false," +
                "\"There was an exception invoking \\u0027ReceiveTrivial\\u0027 on assembly \\u0027BasicTestApp\\u0027. For more details turn on detailed exceptions in \\u0027CircuitOptions.JSInteropDetailedErrors\\u0027\"]";

            var (interopCalls, batches) = ConfigureClient();
            await GoToTestComponent(batches);

            Client.InvokeDotNetMethod(
                "1",
                "BasicTestApp",
                "CreateImportant",
                null,
                JsonSerializer.Serialize(Array.Empty<object>()));

            await Task.Delay(DefaultLatencyTimeout);

            Assert.Single(interopCalls, (0, "DotNet.jsCallDispatcher.endInvokeDotNetFromJS", expectedImportantDotNetObjectRef));

            // Act
            Client.InvokeDotNetMethod(
                "1",
                "BasicTestApp",
                "ReceiveTrivial",
                null,
                JsonSerializer.Serialize(new object[] { new { __dotNetObject = 1 } }));

            await Task.Delay(DefaultLatencyTimeout);

            // Assert
            Assert.Single(interopCalls, (0, "DotNet.jsCallDispatcher.endInvokeDotNetFromJS", expectedError));

            await ValidateClientKeepsWorking(Client, batches);
        }

        [Fact]
        public async Task ContinuesWorkingAfterInvalidAsyncReturnCallback()
        {
            // Arrange
            var expectedError = "An exception occurred executing JS interop: The JSON value could not be converted to System.Int32. Path: $ | LineNumber: 0 | BytePositionInLine: 3.. See InnerException for more details.";

            var (interopCalls, batches) = ConfigureClient();
            await GoToTestComponent(batches);

            // Act
            var jsInteropTriggered = Client.PrepareForNextBatch();
            await Client.ClickAsync("triggerjsinterop");

            await Task.Delay(DefaultLatencyTimeout);
            Assert.Single(interopCalls, (4, "sendMalformedCallbackReturn", (string)null));

            var invalidJSInteropResponse = Client.PrepareForNextBatch();
            Client.InvokeDotNetMethod(
                0,
                "Microsoft.JSInterop",
                "DotNetDispatcher.EndInvoke",
                null,
                "[4, true, \"{\"]");

            await invalidJSInteropResponse;
            var text = Assert.Single(
                Client.FindElementById("errormessage").Children.OfType<TextNode>(),
                e => expectedError == e.TextContent);

            await ValidateClientKeepsWorking(Client, batches);
        }

        [Fact]
        public async Task CannotGrowMemoryIndefinitelyWhenRendersAreNotAcknowledged()
        {
            // Arrange
            var client = new BlazorClient();
            client.ConfirmRenderBatch = false;
            var batchCount = 0;
            client.RenderBatchReceived += (int rid, int bid, byte[] data) => batchCount++;

            var rootUri = _serverFixture.RootUri;
            var initialRender = client.PrepareForNextBatch();
            Assert.True(await client.ConnectAsync(new Uri(rootUri, "/subdir"), prerendered: false), "Couldn't connect to the app");
            await initialRender;

            var selectComponentRender = client.PrepareForNextBatch();
            await client.SelectAsync("test-selector-select", "BasicTestApp.ReliabilityComponent");
            await selectComponentRender;

            await client.ClickAsync("expensiveComputationsDisabled");
            await Task.Delay(DefaultLatencyTimeout);

            for (int i = 0; i < 1_000_000; i++)
            {
                await ValidateClientKeepsWorking(client, () => batchCount);
            }
        }

        [Fact]
        public async Task CannotGrowMemoryIndefinitelyBySpammingTheServerWithEvents()
        {
            // Arrange
            await Task.WhenAll(Enumerable.Range(1, 100).Select(async i =>
              {
                  var client = new BlazorClient();
                  var batchCount = 0;
                  client.RenderBatchReceived += (int rid, int bid, byte[] data) => batchCount++;

                  var rootUri = _serverFixture.RootUri;
                  var initialRender = client.PrepareForNextBatch();
                  Assert.True(await client.ConnectAsync(new Uri(rootUri, "/subdir"), prerendered: false), "Couldn't connect to the app");
                  await initialRender;

                  var selectComponentRender = client.PrepareForNextBatch();
                  await client.SelectAsync("test-selector-select", "BasicTestApp.ReliabilityComponent");
                  await selectComponentRender;

                  for (int j = 0; j < 10000; j++)
                  {
                      await ValidateClientKeepsWorking(client, () => batchCount);
                  }
              }));
        }

        [Fact]
        public async Task CannotGrowMemoryIndefinitelyBySpammingTheServerWithExpensiveEvents()
        {
            // Arrange
            var rnd = RandomNumberGenerator.Create();
            var data = new byte[1024];
            rnd.GetBytes(data);
            var alphabet = "abcdefghijklmnopqrstuvwxyz123456ABCDEFGHIJKLMNOPQRSTUVWXYZ7890+_";
            var value = new string('0', 31 * 1024);
            UpdateValueInPlace(value, data, alphabet);
            var oldValue = "";
            var client = new BlazorClient();
            var batchCount = 0;
            client.RenderBatchReceived += (int rid, int bid, byte[] data) => Interlocked.Increment(ref batchCount);

            var rootUri = _serverFixture.RootUri;
            var initialRender = client.PrepareForNextBatch();
            Assert.True(await client.ConnectAsync(new Uri(rootUri, "/subdir"), prerendered: false), "Couldn't connect to the app");
            await initialRender;

            var selectComponentRender = client.PrepareForNextBatch();
            await client.SelectAsync("test-selector-select", "BasicTestApp.ReliabilityComponent");
            await selectComponentRender;

            for (int j = 0; j < 1_000_000; j++)
            {
                var currentBatches = batchCount;
                // Fire and forget mode
                var _ = client.ChangeAsync("boundTextInput", value, oldValue);

                rnd.GetBytes(data);
                UpdateValueInPlace(value, data, alphabet);
            }
        }

        private unsafe static void UpdateValueInPlace(string oldValue, string value)
        {
            fixed (char* ptr = oldValue)
            {
                for (int i = 0; i < 1024; i++)
                {
                    ptr[i] = value[i];
                }
            }
        }

        private unsafe static void UpdateValueInPlace(string value, byte[] data, string alphabet)
        {
            fixed (char* ptr = value)
            {
                for (int i = 0; i < 1024; i++)
                {
                    ptr[i] = alphabet[data[i] % 64];
                }
            }
        }

        [Fact]
        public async Task CannotInvokeJSInvokableMethodsWithInvalidArgumentsPayload()
        {
            // Arrange
            var expectedError = "[\"1\"," +
                "false," +
                "\"There was an exception invoking \\u0027NotifyLocationChanged\\u0027 on assembly \\u0027Microsoft.AspNetCore.Components.Server\\u0027. For more details turn on detailed exceptions in \\u0027CircuitOptions.JSInteropDetailedErrors\\u0027\"]";

            var (interopCalls, batches) = ConfigureClient();
            await GoToTestComponent(batches);

            // Act
            Client.InvokeDotNetMethod(
                "1",
                "Microsoft.AspNetCore.Components.Server",
                "NotifyLocationChanged",
                null,
                "[ \"invalidPayload\"}");

            await Task.Delay(DefaultLatencyTimeout);

            // Assert
            Assert.Single(interopCalls, (0, "DotNet.jsCallDispatcher.endInvokeDotNetFromJS", expectedError));
            await ValidateClientKeepsWorking(Client, batches);
        }

        private Task ValidateClientKeepsWorking(BlazorClient Client, List<(int, int, byte[])> batches) =>
            ValidateClientKeepsWorking(Client, () => batches.Count);

        private async Task ValidateClientKeepsWorking(BlazorClient Client, Func<int> countAccessor)
        {
            var currentBatches = countAccessor();
            var nextClickRendered = Client.PrepareForNextBatch();
            await Client.ClickAsync("thecounter");
            await nextClickRendered;

            Assert.Equal(currentBatches + 1, countAccessor());
        }

        private async Task GoToTestComponent(List<(int, int, byte[])> batches)
        {
            var rootUri = _serverFixture.RootUri;
            var initialRender = Client.PrepareForNextBatch();
            Assert.True(await Client.ConnectAsync(new Uri(rootUri, "/subdir"), prerendered: false), "Couldn't connect to the app");

            await initialRender;
            Assert.Single(batches);

            var selectComponentRender = Client.PrepareForNextBatch();
            await Client.SelectAsync("test-selector-select", "BasicTestApp.ReliabilityComponent");
            await selectComponentRender;
            Assert.Equal(2, batches.Count);
        }

        private (List<(int, string, string)>, List<(int, int, byte[])>) ConfigureClient()
        {
            var interopCalls = new List<(int, string, string)>();
            Client.JSInterop += (int arg1, string arg2, string arg3) => interopCalls.Add((arg1, arg2, arg3));
            var batches = new List<(int, int, byte[])>();
            Client.RenderBatchReceived += (id, renderer, data) => batches.Add((id, renderer, data));
            return (interopCalls, batches);
        }
    }
}
