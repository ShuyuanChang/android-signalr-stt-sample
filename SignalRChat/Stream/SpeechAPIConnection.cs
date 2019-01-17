using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;

namespace SignalRChat.Stream
{            
    public class SpeechAPIConnection
    {
        public string SessionId { get; set; }
        public VoiceAudioStream AudioStream { get; set; }
        public SpeechRecognizer Recognizer { get; set; }
        public string Language { get; set; }
    }
}
