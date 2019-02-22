using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SignalRChat.Hubs;
using System.Collections.Generic;
using SignalRChat.Stream;

namespace SignalRChat.Helpers
{
    public static class DictionaryExtension
    {
        public static SpeechAPIConnection GetAPIConnectionByLanguage(this Dictionary<string, SpeechAPIConnection> conns, string language)
        {
            var items = from item in conns.Values
                        where item.Language.Equals(language,StringComparison.OrdinalIgnoreCase)
                        select item;
            return items.Count() == 1 ? items.SingleOrDefault() : null;
        }
        public static SpeechAPIConnection GetAPIConnection(this Dictionary<string, SpeechAPIConnection> conns, string sessionId)
        {
            var items = from item in conns.Values
                            where item.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase)
                            select item;
            return items.Count() == 1 ? items.SingleOrDefault() : null;
        }
        public static AttendeeInfo[] GetAttendeeByMyLanguage(this Dictionary<string, AttendeeInfo> items, string language)
        {
            var attendees = from item in items.Values
                            where item.SpeakingLanguage.CompareTo(language) == 0
                            select item;
            return attendees.Count() > 0 ? attendees.ToArray() : null;
        }
        public static AttendeeInfo[] GetAttendeeByTargetLanguage(this Dictionary<string, AttendeeInfo> items,string language)
        {
            var attendees = from item in items.Values
                            where item.PreferredLanguage.CompareTo(language) == 0
                            select item;
            return attendees.Count() > 0 ? attendees.ToArray() : null;
        }
        public static AttendeeInfo GetAttendeeByConnectionID(this Dictionary<string, AttendeeInfo> items,string connectionId)
        {
            var attendees = from item in items.Values
                            where item.ConnectionID.Equals(connectionId, StringComparison.OrdinalIgnoreCase)
                            select item;
            return attendees.SingleOrDefault();
        }
    }
}
