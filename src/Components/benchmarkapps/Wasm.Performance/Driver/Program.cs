// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using Wasm.Performance.Driver;
using DevHostServerProgram = Microsoft.AspNetCore.Blazor.DevServer.Server.Program;

namespace Wasm.Performance.Driver
{
    public partial class Program
    {
        // Run Selenium using a headless browser?
        static readonly bool RunHeadlessBrowser
            = !System.Diagnostics.Debugger.IsAttached;
        // = false;

        static readonly TimeSpan TestAppTimeOut = TimeSpan.FromMinutes(10);

        public static async Task Main()
        {
            var port = 4444;
            SeleniumServer.Start();
            var seleniumUri = await SeleniumServer.WaitForLaunchAsync(port);

            using var browser = CreateSeleniumBrowser(seleniumUri);
            using var testApp = StartTestApp();

            var address = testApp.Services.GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()
                .Addresses
                .First();

            browser.Url = address + "#automated";
            browser.Navigate();

            var results = await RunBenchmark(browser);
            FormatAsBenchmarksOutput(results);

            Console.WriteLine("Done executing benchmark");
        }

        private static void FormatAsBenchmarksOutput(List<BenchmarkResult> results)
        {
            // Sample of the the format: https://github.com/aspnet/Benchmarks/blob/e55f9e0312a7dd019d1268c1a547d1863f0c7237/src/Benchmarks/Program.cs#L51-L67
            var output = new BenchmarkOutput();
            foreach (var result in results)
            {
                output.Metadata.Add(new BenchmarkMetadata
                {
                    Source = "BlazorWasm",
                    Name = result.Name,
                    ShortDescription = $"{result.Name} Duration",
                    LongDescription = $"{result.Name} Duration",
                    Format = "n2"
                });

                output.Measurements.Add(new BenchmarkMeasurement
                {
                    Timestamp = DateTime.UtcNow,
                    Name = result.Name,
                    Value = result.Duration,
                });
            }

            // Statistics about publish sizes
            output.Metadata.Add(new BenchmarkMetadata
            {
                Source = "BlazorWasm",
                Name = "Publish size (linked)",
                ShortDescription = "Publish size - linked app (MB)",
                LongDescription = "Publish size - linked app (MB)",
                Format = "n2",
            });

            var testAssembly = typeof(TestApp.Startup).Assembly;
            var testApp = new DirectoryInfo(Path.Combine(
                Path.GetDirectoryName(testAssembly.Location),
                testAssembly.GetName().Name));

            output.Measurements.Add(new BenchmarkMeasurement
            {
                Timestamp = DateTime.UtcNow,
                Name = "Publish size (linked)",
                Value = GetDirectorySize(testApp) / 1024,
            });

            Console.WriteLine("#StartJobStatistics");
            Console.WriteLine(JsonSerializer.Serialize(output));
            Console.WriteLine("#EndJobStatistics");
        }

        private static Task<List<BenchmarkResult>> RunBenchmark(RemoteWebDriver browser)
        {
            var remoteLogs = new RemoteLogs(browser);
            var tcs = new TaskCompletionSource<List<BenchmarkResult>>();

            Task.Run(() =>
            {
                try
                {
                    var results = new List<BenchmarkResult>();
                    var lastSeenCount = 0;
                    new WebDriverWait(browser, TimeSpan.FromSeconds(90)).Until(c =>
                    {
                        var logs = remoteLogs.GetLog("browser");
                        for (var i = lastSeenCount; i < logs.Count; i++)
                        {
                            Console.WriteLine(logs[i].Message);
                            if (logs[i].Message.Contains("Benchmark completed", StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }

                        lastSeenCount = logs.Count;
                        return false;
                    });

                    var js = (string)browser.ExecuteScript("return JSON.stringify(window.benchmarksResults)");
                    tcs.TrySetResult(JsonSerializer.Deserialize<List<BenchmarkResult>>(js, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }

        class BenchmarkResult
        {
            public string Name { get; set; }

            public bool Success { get; set; }

            public int NumExecutions { get; set; }

            public double Duration { get; set; }
        }

        static IHost StartTestApp()
        {
            var args = new[]
            {
                "--urls", "http://127.0.0.1:0",
                "--applicationpath", typeof(TestApp.Startup).Assembly.Location,
            };

            var host = DevHostServerProgram.BuildWebHost(args);
            RunInBackgroundThread(host.Start);
            return host;
        }

        static void RunInBackgroundThread(Action action)
        {
            var isDone = new ManualResetEvent(false);

            ExceptionDispatchInfo edi = null;
            Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    edi = ExceptionDispatchInfo.Capture(ex);
                }

                isDone.Set();
            });

            if (!isDone.WaitOne(TestAppTimeOut))
            {
                throw new TimeoutException("Timed out waiting for: " + action);
            }

            if (edi != null)
            {
                throw edi.SourceException;
            }
        }

        static RemoteWebDriver CreateSeleniumBrowser(Uri uri)
        {
            var options = new ChromeOptions();

            if (RunHeadlessBrowser)
            {
                options.AddArgument("--headless");
            }

            options.SetLoggingPreference(LogType.Browser, LogLevel.All);


            var attempt = 0;
            const int MaxAttempts = 3;
            do
            {
                try
                {
                    // The driver opens the browser window and tries to connect to it on the constructor.
                    // Under heavy load, this can cause issues
                    // To prevent this we let the client attempt several times to connect to the server, increasing
                    // the max allowed timeout for a command on each attempt linearly.
                    var driver = new RemoteWebDriver(
                        uri,
                        options.ToCapabilities(),
                        TimeSpan.FromSeconds(60).Add(TimeSpan.FromSeconds(attempt * 60)));

                    driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1);

                    return driver;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing RemoteWebDriver: {ex.Message}");
                }

                attempt++;

            } while (attempt < MaxAttempts);

            throw new InvalidOperationException("Couldn't create a Selenium remote driver client. The server is irresponsive");
        }

        static long GetDirectorySize(DirectoryInfo directory)
        {
            long size = 0;
            foreach (var item in directory.EnumerateFileSystemInfos())
            {
                if (item is FileInfo fileInfo)
                {
                    size += fileInfo.Length;
                }
                else if (item is DirectoryInfo directoryInfo)
                {
                    size += GetDirectorySize(directoryInfo);
                }
            }

            return size;
        }
    }
}
