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

namespace SignalRChat.Hubs
{
    public class VoiceHub : Hub
    {
        private static IConfiguration _config;
        private static IHubContext<VoiceHub> _hubContext;
        private static Dictionary<string, SpeechAPIConnection> _connections;
        private static Dictionary<string, AttendeeInfo> _attendeeInfo;
        public VoiceHub(IConfiguration configuration, IHubContext<VoiceHub> ctx)
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
        public async void RegisterAttendeeAsync(string name, string myLanguage, string preferredLanguage)
        {
            Debug.WriteLine($"User {name}, Language: {myLanguage}, Connection {Context.ConnectionId} starting audio.");
            var config = _config.GetSection("SpeechAPI").Get<AppSettings>();

            bool exists = await InitializeAttendeeInfo(name, myLanguage, preferredLanguage);

            var audioStream = new VoiceAudioStream();
            var audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
            var audioConfig = AudioConfig.FromStreamInput(audioStream, audioFormat);

            var speechKey = config.SubscriptionKey;
            var speechRegion = config.Region;
            var url = config.EndpointUri;

            Debug.WriteLine($"Key:{speechKey} | Region:{speechRegion}");

            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            speechConfig.SpeechRecognitionLanguage = preferredLanguage;
            speechConfig.OutputFormat = OutputFormat.Simple;

            var speechClient = new SpeechRecognizer(speechConfig, audioConfig);
            speechClient.Recognized += _speechClient_Recognized;
            speechClient.Recognizing += _speechClient_Recognizing;
            speechClient.Canceled += _speechClient_Canceled;
            speechClient.SessionStarted += _speechClient_SessionStarted;  
            string sessionId = speechClient.Properties.GetProperty(PropertyId.Speech_SessionId);

            //Maintains only one API connection per language
            SpeechAPIConnection conn = null;
            
            if (_connections.ContainsKey(preferredLanguage))
            {
                conn = _connections[preferredLanguage];
                conn.SessionId = sessionId;
            }
            else
            {
                conn = new SpeechAPIConnection()
                {
                    SessionId = sessionId,
                    AudioStream = audioStream,
                    Recognizer = speechClient,
                    Language = preferredLanguage
                };
                _connections[preferredLanguage] = conn;
            }

            Debug.WriteLine($"Connection for {preferredLanguage} added | SessionId:{sessionId}");

            await SendToAttendeeAsync(_attendeeInfo.GetAttendeeByConnectionID(Context.ConnectionId), $"Welcome:{name}");
            await speechClient.StartContinuousRecognitionAsync();

            Debug.WriteLine("Audio start message.");
        }

        

        //  Java's uses signed byte. so we use sbyte here to receive incoming data from Java client
        public Task ReceiveAudioJavaAsync(string name, sbyte[] audio)
        {
            var attendee = _attendeeInfo[name];
            byte[] buffer = (byte[])(Array)audio;
            //WriteToFile(name, audio);
            _connections[attendee.PreferredLanguage].AudioStream.Write(buffer, 0, buffer.Length);
            Debug.WriteLine("Translating.............................");
            return Task.CompletedTask;
        }
        //  Other clients use this method to send audio data
        public Task ReceiveAudioAsync(string name, byte[] audio)
        {
            var attendee = _attendeeInfo[name];
            //WriteToFile(name, audio);
            _connections[attendee.PreferredLanguage].AudioStream.Write(audio, 0, audio.Length);

            return Task.CompletedTask;
        }
        #endregion

        #region SignalR Helper Functions
        public async override Task OnDisconnectedAsync(Exception exception)
        {
            Debug.WriteLine("======== Disconnecting =========");
            var attendee = _attendeeInfo.GetAttendeeByConnectionID(Context.ConnectionId);
            //  For testing purpose, do not remove attendee reference when client disconnected, the client will try to reconnect.
            //  To futher complete this, we should implement a closing handshake to ensure disconnect process before removing attendee reference
            /*
            if (attendee != null)
            {
                _attendeeInfo.Remove(attendee.ID);

                //Close API connections that has no attendees associated with
                var conns = _attendeeInfo.GetAttendeesByLanguage(attendee.PreferredLanguage);
                if (conns != null)
                {
                    var con = _connections[attendee.PreferredLanguage];
                    await con.Recognizer.StopContinuousRecognitionAsync();
                    con.Recognizer.Dispose();
                    con.AudioStream.Dispose();
                }
            }
            */
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

        private Task SendToAttendeeAsync(AttendeeInfo attendee, string message)
        {
            try
            {
                var connectionId = _attendeeInfo[attendee.ID].ConnectionID;
                return _hubContext.Clients.Client(connectionId).SendCoreAsync(Commands.SEND_TO_ATTENDEE,new object[] { message });
                //return _hubContext.Clients.Client(connectionId).SendAsync(Commands.SEND_TO_ATTENDEE, message);
            }
            catch
            {
                Debug.WriteLine(String.Join(',', _attendeeInfo.Keys.ToArray()));
                throw;
            }
        }

        private Task[] SendToAttendeesByLanguageAsync(string language, string message)
        {
            var attendees = _attendeeInfo.GetAttendeesByLanguage(language);

            if (attendees != null)
            {
                List<Task> tasks = new List<Task>();
                foreach (var attendee in attendees)
                {
                    Console.WriteLine($"Send to attendee {attendee.ConnectionID}");
                    tasks.Add(SendToAttendeeAsync(attendee, message));
                }
                //Task.WaitAll(tasks.ToArray(), System.Threading.CancellationToken.None);
                return tasks.ToArray();
            }
            return null;
        }
        private void SendTranscript(string language, string message)
        {
            Debug.WriteLine($"Sending Transcripts:{message} | {language}");
            var tasks = SendToAttendeesByLanguageAsync(language, message);
            Task.WaitAll(tasks);
        }
        #endregion

        #region Speech events
        private void _speechClient_Canceled(object sender, SpeechRecognitionCanceledEventArgs e)
        {
            //WriteToFile("result.txt", System.Text.Encoding.Default.GetBytes($"[{DateTime.Now}][Canceled]{e.Reason}\r\n"));
            Debug.WriteLine("Recognition was cancelled.");
        }

        private void _speechClient_Recognizing(object sender, SpeechRecognitionEventArgs e)
        {
            //WriteToFile("result.txt", System.Text.Encoding.Default.GetBytes($"[{DateTime.Now}][Recognizing]{e.Result.Text}\r\n"));
            Debug.WriteLine($"{e.SessionId} > Intermediate result: {e.Result.Text}");
            Debug.WriteLine(JsonConvert.SerializeObject(e));
            Debug.WriteLine(JsonConvert.SerializeObject(sender));
            var conn = _connections.GetAPIConnection(e.SessionId);
            SendTranscript(conn.Language, e.Result.Text);
        }
        private void _speechClient_SessionStarted(object sender, SessionEventArgs e)
        {
            //WriteToFile("result.txt", System.Text.Encoding.Default.GetBytes($"[{DateTime.Now}][SessionStarted]\r\n"));
        }
        private void _speechClient_Recognized(object sender, SpeechRecognitionEventArgs e)
        {
            Debug.WriteLine($"{e.SessionId} > Final result: {e.Result.Text}");
            SpeechRecognizer r = (SpeechRecognizer)sender;
            SendTranscript(r.SpeechRecognitionLanguage, e.Result.Text);
        }
        #endregion
    }
}