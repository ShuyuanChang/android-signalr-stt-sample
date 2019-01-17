using System;
using System.Collections.Generic;
using System.Text;
using CommandLine.Text;
using CommandLine;
namespace SignalRConsoleClient
{
    public class ConsoleOptions
    {
        [Option(
           longName: "url",
           HelpText = "SignalR Server URL",
           Required = false,
           Default = "http://localhost:31047/voice"
           )]
        public string URL { get; set; }

        [Option(
            shortName:'u',
            longName:"user",
            HelpText ="Unique User Name",
            Required = false,
            Default = "michael"
            )]
        public string UserName { get; set; }

        [Option(
            shortName: 'l',
            longName: "lang",
            HelpText = "Preferred Language",
            Required = false,
            Default = "en-us"
            )]
        public string Language { get; set; }
    }
}
