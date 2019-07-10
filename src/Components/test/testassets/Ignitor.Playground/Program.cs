using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Ignitor.Playground
{
    class Program
    {
        static Task Main(string[] args)
        {
            Console.WriteLine("Test starting");
            //return DoTheThing2();
            return DoTheThing();
        }

        private static async Task DoTheThing2()
        {
            var rnd = RandomNumberGenerator.Create();
            var data = new byte[1024];
            rnd.GetBytes(data);
            var alphabet = "abcdefghijklmnopqrstuvwxyz123456ABCDEFGHIJKLMNOPQRSTUVWXYZ7890+_";
            var value = new string('0', 31 * 1024);
            UpdateValueInPlace(value, data, alphabet);

            var builder = new HubConnectionBuilder();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHubProtocol, IgnitorMessagePackHubProtocol>());
            var uri = new Uri("http://localhost:5000/");
            builder.WithUrl(new Uri(uri, "_blazor/"));
            //builder.ConfigureLogging(l => l.AddConsole().SetMinimumLevel(LogLevel.Warning));

            var hubConnection = builder.Build();
            await hubConnection.StartAsync();
            Console.WriteLine("Connected");

            hubConnection.On<int, string, string>("JS.BeginInvokeJS", OnBeginInvokeJS);

            var semaphore = new SemaphoreSlim(0);
            var eventId = 5; // 1 - actual event id to account for render batch incrementing it on first render
            hubConnection.On<int, int, byte[]>("JS.RenderBatch", OnRenderBatch);
            hubConnection.On<Error>("JS.OnError", (error) => Console.WriteLine("ERROR: " + error.Stack));

            hubConnection.Closed += OnClosedAsync;

            // Now everything is registered so we can start the circuit.
            var circuitId = await hubConnection.InvokeAsync<string>("StartCircuit", new Uri(uri.GetLeftPart(UriPartial.Authority)), uri);
            Console.WriteLine(circuitId ?? "NULL");
            await Task.Delay(1000);

            Console.WriteLine("Ready to go. Press any key to start.");
            Console.ReadKey();

            var callId = "0";
            var assemblyName = "Microsoft.AspNetCore.Components.Web";
            var methodIdentifier = "DispatchEvent";
            var dotNetObjectId = 0;

            var serializationOptions = new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var changeEventArgs = new UIChangeEventArgs()
            {
                Type = "change",
                Value = value,
            };
            var serializedJson = JsonSerializer.Serialize(changeEventArgs, serializationOptions);

            for (int j = 0; j < 1_000_000; j++)
            {
                // Fire and forget mode
                var browserDescriptor = new RendererRegistryEventDispatcher.BrowserEventDescriptor()
                {
                    BrowserRendererId = 0,
                    EventHandlerId = eventId,
                    EventArgsType = "change",
                    EventFieldInfo = new EventFieldInfo
                    {
                        ComponentId = 7,
                        FieldValue = ""
                    }
                };

                var argsObject = new object[] { browserDescriptor, serializedJson };
                var changeArgs = JsonSerializer.Serialize(argsObject, serializationOptions);

                Console.Write($"Sending render with event id: "); Console.WriteLine(eventId);

                var _ = hubConnection.InvokeAsync("BeginInvokeDotNetFromJS", callId, assemblyName, methodIdentifier, dotNetObjectId, changeArgs);
            }


            static void OnBeginInvokeJS(int asyncHandle, string identifier, string argsJson)
            {
                Console.WriteLine($"{identifier}: {argsJson}");
            }

            void OnRenderBatch(int browserRendererId, int batchId, byte[] batchData)
            {
                semaphore.Release();
                Interlocked.Increment(ref eventId);

                Console.Write($"Ack batch: "); Console.WriteLine(batchId);
                hubConnection.InvokeAsync("OnRenderCompleted", batchId, /* error */ null);
            }

            Task OnClosedAsync(Exception ex)
            {
                Console.WriteLine("Connection closed");
                return Task.CompletedTask;
            }
        }

        private class Error
        {
            public string Stack { get; set; }
        }

        private static async Task DoTheThing()
        {
            var rnd = RandomNumberGenerator.Create();
            var data = new byte[1024];
            rnd.GetBytes(data);
            var alphabet = "abcdefghijklmnopqrstuvwxyz123456ABCDEFGHIJKLMNOPQRSTUVWXYZ7890+_";
            var value = new string('0', 31 * 1024);
            UpdateValueInPlace(value, data, alphabet);
            var oldValue = "";
            var client = new BlazorClient();

            var rootUri = new Uri("http://localhost:5000/");
            var initialRender = client.PrepareForNextBatch();
            if (!await client.ConnectAsync(rootUri, prerendered: false))
            {
                Console.WriteLine("Couldn't connect to the server.");
                return;
            }

            await initialRender;

            Console.WriteLine("Ready to start, press any key.");
            await Console.Out.FlushAsync();
            var __ = Console.ReadKey();

            for (int j = 0; j < 1_000_000; j++)
            {
                // Fire and forget mode
                var _ = client.ChangeAsync("boundTextInput", value, oldValue);
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
    }
}
