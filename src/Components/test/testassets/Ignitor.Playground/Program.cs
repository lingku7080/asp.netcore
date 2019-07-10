using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Ignitor.Playground
{
    class Program
    {
        static Task Main(string[] args)
        {
            Console.WriteLine("Test starting");
            return DoTheThing();
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
            if(!await client.ConnectAsync(rootUri, prerendered: false))
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
