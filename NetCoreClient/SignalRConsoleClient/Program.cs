using System;
using System.IO;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Microsoft.AspNetCore.SignalR.Client;

namespace SignalRConsoleClient
{
    class Program
    {
        static ConsoleOptions options = null;
        static HubConnection connection = null;
        static void Main(string[] args)
        {
            options = new ConsoleOptions();
            var result = Parser.Default.ParseArguments<ConsoleOptions>(args)
                .WithParsed<ConsoleOptions>( async (o) =>
                    {
                        options = o;
                        Task.WaitAll(StartConversation());
                    }
                );
        }

        static async Task StartConversation()
        {
            connection = new HubConnectionBuilder()
                                    .WithUrl(options.URL)
                                    .Build();
            connection.Closed += async (error) =>
            {
                //  Retry
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };
            connection.On<string>("ServerMessages", (message) =>
            {
                Console.WriteLine(message);
            });

            await connection.StartAsync();
            Console.WriteLine($"user {options.UserName} with language {options.Language} registerred");
            await connection.InvokeAsync("RegisterAttendeeAsync",
                                    options.UserName,
                                    options.Language,
                                    options.Language);

            var data = File.ReadAllBytes(@".\whatstheweatherlike.wav");
            const int size = 100 * 20;
            byte[] buffer = new byte[size];

            int index = 0;
            while(index < data.Length)
            {
                int targetSize = data.Length - index;

                if (targetSize >= size)
                    targetSize = size;
                
                Array.Copy(data, index, buffer, 0, targetSize);
                index += size;

                await connection.InvokeAsync("ReceiveAudioAsync",
                                    options.UserName, buffer);
            }
            
            Console.ReadLine();
            
        }

    }
}
