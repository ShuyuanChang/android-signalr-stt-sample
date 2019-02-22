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
using Microsoft.Extensions.Logging;

namespace SignalRChat.Hubs
{
    public class VoiceHub : Hub
    {
        private static IConfiguration _config;
        private static IHubContext<VoiceHub> _hubContext;
        private static Dictionary<string, SpeechAPIConnection> _connections;
        private static Dictionary<string, AttendeeInfo> _attendeeInfo;
        private static ILogger<VoiceHub> _logger;

        public VoiceHub(IConfiguration configuration, IHubContext<VoiceHub> ctx, ILogger<VoiceHub> logger)
        {
            if (_logger == null)
                _logger = logger;

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
            _logger.LogDebug($"User {name}, Language: {myLanguage}, Connection {Context.ConnectionId} starting audio.");
            var config = _config.GetSection("SpeechAPI").Get<AppSettings>();

            bool exists = await InitializeAttendeeInfo(name, myLanguage, preferredLanguage);

            var audioStream = new VoiceAudioStream();
            var audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
            var audioConfig = AudioConfig.FromStreamInput(audioStream, audioFormat);

            var speechKey = config.SubscriptionKey;
            var speechRegion = config.Region;
            var url = config.EndpointUri;

            _logger.LogDebug($"Key:{speechKey} | Region:{speechRegion}");

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

            _logger.LogDebug($"Connection for {preferredLanguage} added | SessionId:{sessionId}");

            await SendToAttendeeAsync(_attendeeInfo.GetAttendeeByConnectionID(Context.ConnectionId), $"Welcome:{name}");
            await speechClient.StartContinuousRecognitionAsync().ConfigureAwait(false);

            _logger.LogDebug("Audio start message.");
        }

        private void WriteToFile(string name, sbyte[] data)
        {
            var path = Path.Combine(Startup.RootPath, $"APP_DATA\\{name}");
            FileStream stream = null;
            if (!File.Exists(path))
            {
                stream = File.Create(path);
            }
            else
            {
                stream = File.Open(path, FileMode.Append);
            }
            FileInfo info = new FileInfo(path);
            using (var sw = new BinaryWriter(stream))
            {
                sw.Seek((int)info.Length, SeekOrigin.Begin);
                var buffer = (byte[])(Array)data;
                sw.Write(buffer, 0, (int)buffer.Length);
                sw.Close();
            }
        }
        private void WriteToFile(string name, byte[] data)
        {
            var path = Path.Combine(Startup.RootPath, $"APP_DATA\\{name}.wav");
            FileStream stream = null;
            if (!File.Exists(path))
            {
                stream = File.Create(path);
            }
            else
            {
                stream = File.Open(path, FileMode.Append);
            }
            FileInfo info = new FileInfo(path);
            using (var sw = new BinaryWriter(stream))
            {
                sw.Seek((int)info.Length, SeekOrigin.Begin);
                sw.Write(data, 0, (int)data.Length);
                sw.Close();
            }
        }
        public Task ReceiveAudioJavaAsync(string name, sbyte[] audio)
        {
            var attendee = _attendeeInfo[name];
            byte[] buffer = (byte[])(Array)audio;
            //WriteToFile($"{name}.wav", audio);
            _connections[attendee.PreferredLanguage].AudioStream.Write(buffer, 0, buffer.Length);
            buffer = null;
            //Console.WriteLine("Translating.............................");
            //Console.WriteLine("Translating.............................");
            //Trace.WriteLine("Translating.............................");
            return Task.CompletedTask;
        }
        public void Disconnect(string name)
        {
            _logger.LogDebug("======== Disconnecting =========");
            var attendee = _attendeeInfo.GetAttendeeByConnectionID(Context.ConnectionId);
            if (attendee != null)
            {
                _attendeeInfo.Remove(attendee.ID);
            }
        }
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
            _logger.LogInformation("======== Disconnecting =========");
            var attendee = _attendeeInfo.GetAttendeeByConnectionID(Context.ConnectionId);
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
                _logger.LogInformation("Exception while sending to attendees:" + String.Join(',', _attendeeInfo.Keys.ToArray()));
                throw;
                return Task.CompletedTask;
            }
        }

        private Task[] SendToAttendeesByLanguageAsync(string language, string message)
        {
            var attendees = _attendeeInfo.GetAttendeeByTargetLanguage(language);

            if (attendees != null)
            {
                List<Task> tasks = new List<Task>();
                foreach (var attendee in attendees)
                {
                    _logger.LogDebug($"Send to attendee {attendee.ConnectionID}");
                    tasks.Add(SendToAttendeeAsync(attendee, message));
                }
                //Task.WaitAll(tasks.ToArray(), System.Threading.CancellationToken.None);
                return tasks.ToArray();
            }
            return null;
        }
        private void SendTranscript(string language, string message)
        {
            _logger.LogDebug($"Sending Transcripts:{message} | {language}");
            var tasks = SendToAttendeesByLanguageAsync(language, message);
            Task.WaitAll(tasks);
        }
        #endregion

        #region Speech events
        private void _speechClient_Canceled(object sender, SpeechRecognitionCanceledEventArgs e)
        {
            WriteToFile("result.txt", System.Text.Encoding.Default.GetBytes($"[{DateTime.Now}][Canceled]{e.ErrorDetails}\r\n"));
            _logger.LogDebug("Recognition was cancelled.");
        }

        private void _speechClient_Recognizing(object sender, SpeechRecognitionEventArgs e)
        {
            WriteToFile("result.txt", System.Text.Encoding.Default.GetBytes($"[{DateTime.Now}][Recognizing]{e.Result.Text}\r\n"));
            _logger.LogDebug($"{e.SessionId} > Intermediate result: {e.Result.Text}");
            _logger.LogDebug(JsonConvert.SerializeObject(e));
            _logger.LogDebug(JsonConvert.SerializeObject(sender));
            var conn = _connections.GetAPIConnectionByLanguage(((SpeechRecognizer)sender).SpeechRecognitionLanguage);
            _logger.LogDebug($"Sending transcripts of {conn?.Language}:{e?.Result?.Text}");
            
            SendTranscript(conn.Language, e.Result.Text);
        }
        private void _speechClient_SessionStarted(object sender, SessionEventArgs e)
        {
            WriteToFile("result.txt", System.Text.Encoding.Default.GetBytes($"[{DateTime.Now}][SessionStarted]{e.SessionId}\r\n"));
        }
        private void _speechClient_Recognized(object sender, SpeechRecognitionEventArgs e)
        {
            _logger.LogDebug($"{e.SessionId} > Final result: {e.Result.Best().FirstOrDefault()?.Text}");
            SpeechRecognizer r = (SpeechRecognizer)sender;
            SendTranscript(r.SpeechRecognitionLanguage, e.Result.Best().FirstOrDefault()?.Text);
        }
        #endregion
    }
}