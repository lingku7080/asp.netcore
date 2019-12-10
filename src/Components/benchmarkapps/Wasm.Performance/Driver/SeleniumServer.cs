// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Wasm.Performance.Driver
{
    class SeleniumServer : IDisposable
    {
        private SeleniumServer(Process process)
        {
            SeleniumProcess = process;
        }

        private Process SeleniumProcess { get; }

        public static SeleniumServer Start(int port = 4444)
        {
            var outputLock = new object();

            var psi = new ProcessStartInfo
            {
                FileName = "/opt/bin/start-selenium-standalone.sh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = Process.Start(psi);
            var output = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                lock (outputLock)
                {
                    Console.WriteLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                lock (outputLock)
                {
                    Console.Error.WriteLine(e.Data);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return new SeleniumServer(process);
        }

        public static async ValueTask<Uri> WaitForLaunchAsync(int port)
        {
            var uri = new UriBuilder("http", "localhost", port, "/wd/hub/").Uri;
            var httpClient = new HttpClient
            {
                BaseAddress = uri,
                Timeout = TimeSpan.FromSeconds(1),
            };

            Console.WriteLine($"Attempting to connect to Selenium Server running at {uri}");

            const int MaxRetries = 30;
            var retries = 0;

            while (retries < MaxRetries)
            {
                retries++;
                await Task.Delay(1000);
                try
                {
                    var response = await httpClient.GetAsync("status");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Console.WriteLine("Connected to Selenium");
                        return uri;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);

                    if (retries == MaxRetries)
                    {
                        throw;
                    }
                }
            }

            throw new Exception("This shouldn't happen");
        }

        public void Dispose()
        {
            SeleniumProcess.Dispose();
        }
    }
}
