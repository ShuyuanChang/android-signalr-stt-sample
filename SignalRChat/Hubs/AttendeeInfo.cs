using Microsoft.AspNetCore.SignalR;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SignalRChat.Hubs
{
    public class AttendeeInfo{
        public string ID {get;set;}
        public string ConnectionID{get;set;}
        public string SpeakingLanguage{get;set;}

        public string PreferredLanguage{get;set;}

    }
}