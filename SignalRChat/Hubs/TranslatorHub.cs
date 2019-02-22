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
using SignalRChat.Stream;
using SignalRChat.Helpers;
using Newtonsoft.Json;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.CognitiveServices.Speech.Translation;

namespace SignalRChat.Hubs
{
    public class TranslatorHub : Hub
    {
        private static IConfiguration _config;
        private static IHubContext<TranslatorHub> _hubContext;
        private static Dictionary<string, SpeechAPIConnection> _connections;
        private static Dictionary<string, AttendeeInfo> _attendeeInfo;
        
        public TranslatorHub(IConfiguration configuration, IHubContext<TranslatorHub> ctx)
        {
            if (_config == null)
                _config = configuration;

            if (_connections == null)
                _connections = new Dictionary<string, SpeechAPIConnection>();

            if (_hubContext == null)
                _hubContext = ctx;

            if (_attendeeInfo == null)
                _attendeeInfo = new Dictionary<string, AttendeeInfo>();
        }

        #region SignalR public methods
        public async void RegisterAttendeeAsync(string name, string myLanguage, string targetLanguage)
        {
            Console.WriteLine($"User {name}, Language: {myLanguage}, Connection {Context.ConnectionId} starting audio.");
            var config = _config.GetSection("SpeechAPI").Get<AppSettings>();

            bool exists = await InitializeAttendeeInfo(name, myLanguage, targetLanguage);

            if (exists)
                return;

            var audioStream = new VoiceAudioStream();
            var audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
            var audioConfig = AudioConfig.FromStreamInput(audioStream, audioFormat);

            var speechKey = config.SubscriptionKey;
            var speechRegion = config.Region;
            var url = config.EndpointUri;

            Console.WriteLine($"Key:{speechKey} | Region:{speechRegion}");
            var speechConfig = SpeechTranslationConfig.FromSubscription(speechKey, speechRegion);
            speechConfig.SpeechRecognitionLanguage = myLanguage;

            if (!speechConfig.TargetLanguages.Contains(targetLanguage))
            {
                speechConfig.AddTargetLanguage(targetLanguage);
            }

            Console.WriteLine($"My Language:{myLanguage} | Target Language:{targetLanguage}");

            speechConfig.OutputFormat = OutputFormat.Simple;

            //TODO: switch back
            Console.WriteLine($"Adding {Context.ConnectionId} to ALL");
#if false
            await _hubContext.Groups.AddToGroupAsync(Context.ConnectionId, targetLanguage);
#else
        
            await _hubContext.Groups.AddToGroupAsync(Context.ConnectionId, "ALL");
#endif
            //Maintains only one API connection per language
            SpeechAPIConnection conn = null;
            TranslationRecognizer _recognizer = null;
            if (_connections.ContainsKey(name))
            {
                conn = _connections[name];
                conn.AudioStream.Dispose();
                var c = conn.Recognizer as TranslationRecognizer;

                if(c != null){
                    await c.StopContinuousRecognitionAsync();
                }
            }
            else
            {
                conn = new SpeechAPIConnection();
                _connections[name] = conn;
            }
            _recognizer = new TranslationRecognizer(speechConfig, audioConfig);
            exists = false;
            _recognizer.Recognized += _speechClient_Recognized;
            _recognizer.Recognizing += _speechClient_Recognizing;
            _recognizer.Canceled += _speechClient_Canceled;
            _recognizer.SessionStarted += _speechClient_SessionStarted;
            string sessionId = _recognizer.Properties.GetProperty(PropertyId.Speech_SessionId);
            conn.SessionId = sessionId;
            conn.AudioStream = audioStream;
            conn.Recognizer = _recognizer;
            conn.Language = myLanguage;
            conn.TargetLanguage = targetLanguage;
            Console.WriteLine($"Connection for {name} added | SessionId:{sessionId}");
            await Broadcast(myLanguage, $"Welcome {name}");
            if (!exists)
            {
                await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false) ;
            }
            Debug.WriteLine("Audio start message.");
        }
        
        
        public Task ReceiveAudioJavaAsync(string name, sbyte[] audio)
        {
            var attendee = _attendeeInfo[name];
            byte[] buffer = (byte[])(Array)audio;
            _connections[name].AudioStream.Write(buffer, 0, buffer.Length);
            return Task.CompletedTask;
        }
        public Task ReceiveAudioAsync(string name, byte[] audio)
        {
            var attendee = _attendeeInfo[name];
            _connections[name].AudioStream.Write(audio, 0, audio.Length);

            return Task.CompletedTask;
        }
#endregion

#region SignalR Helper Functions
        public async override Task OnDisconnectedAsync(Exception exception)
        {
            Debug.WriteLine("======== Disconnecting =========");
            var attendee = _attendeeInfo.GetAttendeeByConnectionID(Context.ConnectionId);
            
            await base.OnDisconnectedAsync(exception);
        }
        //  Attendee registeration. provides name and preferred language and speaking language
        private Task<bool> InitializeAttendeeInfo(string name, string myLanguage, string preferredLanguage)
        {
            bool exists = false;
            AttendeeInfo attendee = null;

            if (!_attendeeInfo.ContainsKey(name))
            {
                attendee = new AttendeeInfo();
            }
            else
            {
                attendee = _attendeeInfo[name];
                exists = true;
            }
            attendee.ID = name;
            attendee.ConnectionID = Context.ConnectionId;
            attendee.SpeakingLanguage = myLanguage;
            attendee.PreferredLanguage = preferredLanguage;

            _attendeeInfo[name] = attendee;

            return Task.FromResult<bool>(exists);
        }

        private Task Broadcast(string lang, string message)
        {
            try
            {
                return _hubContext.Clients.All.SendCoreAsync(Commands.SEND_TO_ATTENDEE,new object[] { message });
            }
            catch
            {
                Console.WriteLine("Exception while sending to attendees:" + String.Join(',', _attendeeInfo.Keys.ToArray()));
                throw;
                return Task.CompletedTask;
            }
        }


#endregion

#region Speech events
        private void _speechClient_Canceled(object sender, TranslationRecognitionCanceledEventArgs e)
        {
            Console.WriteLine("Recognition was cancelled.");
        }

        private void _speechClient_Recognizing(object sender, TranslationRecognitionEventArgs e)
        {
            Console.WriteLine($"{e.SessionId} > Intermediate result: {e.Result.Text}");
            Console.WriteLine($"Sending transcripts :{e?.Result?.Text}");
            foreach (var result in e.Result.Translations)
            {
                Task.Run( async () => {await Broadcast(result.Key, $"{result.Value}:{e.Result.Text}");});
            }
        }
        private void _speechClient_SessionStarted(object sender, SessionEventArgs e)
        {
            //TODOL
        }
        private void _speechClient_Recognized(object sender, TranslationRecognitionEventArgs e)
        {
        }
#endregion
    }
}