using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Domain.Common
{
    public enum MessageDirection
    {
        In,
        Out,
        None
    }
    public static class LogUtils
    {
        public static void LogHttpRequest(string requestMethod, string queryOrCommand, long timeEllapsed = 0, string tenant = null, string user = null)
        {
            var _sb = BuildMessage("Request", null, new List<Tuple<string, string>>()
{
                Tuple.Create("HTTP", requestMethod),
                Tuple.Create(IsQuery(queryOrCommand) ? "Query" : "Command", queryOrCommand),
                Tuple.Create("ElapsedMilliseconds", timeEllapsed + "")
            }, tenant, user, MessageDirection.None);

            Log.Information(_sb.ToString());
        }

        private static bool IsQuery(string queryOrCommand)
        {
            return queryOrCommand.Contains("Query");
        }

        public static void LogWebsocketEvent(string channel, string ev, MessageDirection dir, string message = null, string tenant = null, string user = null)
        {
            var _sb = BuildMessage("Websocket", null, new List<Tuple<string, string>>()
            {
                Tuple.Create("Channel",channel),
                Tuple.Create("Event",ev),
                Tuple.Create("Message",message)
            }, tenant, user, dir);

            Log.Information(_sb.ToString());
        }

        public static void LogError(string title, string errorMessage, string className, string exception = null, string tenant = null, string user = null)
        {
            var _sb = BuildMessage("Error", title, new List<Tuple<string, string>>()
            {
                Tuple.Create("Message",errorMessage),
                Tuple.Create("ClassName",className),
                Tuple.Create("Exception",exception),
            }, tenant, user, MessageDirection.None);

            Log.Error(_sb.ToString());
        }

        public static void LogWarning(string title, string warningMessage, string className, string exception = null, string tenant = null, string user = null)
        {
            var _sb = BuildMessage("Warning", title, new List<Tuple<string, string>>()
            {
                Tuple.Create("Message",warningMessage),
                Tuple.Create("ClassName",className),
                Tuple.Create("Exception",exception),
            }, tenant, user, MessageDirection.None);

            Log.Warning(_sb.ToString());
        }

        public static void LogEvent(string title, string eventMessage, string tenant = null, string user = null)
        {
            var _sb = BuildMessage("Event", title, new List<Tuple<string, string>>()
            {
                Tuple.Create("Message",eventMessage)
            }, tenant, user, MessageDirection.None);

            Log.Information(_sb.ToString());
        }

        public static void LogRetry(int count, int countMax, string message)
        {
            var _sb = BuildMessage("RetryPolicy", null, new List<Tuple<string, string>>()
            {
                Tuple.Create("Retry", count + " of " + countMax),
                Tuple.Create("Message",message)
            }, null, null, MessageDirection.None);

            Log.Information(_sb.ToString());
        }

        public static void LogBrokerEvent(string message, string messageType, string from, string to, string payload, MessageDirection dir = MessageDirection.None, string tenant = null, string user = null)
        {
            var _sb = BuildMessage("Broker", null, new List<Tuple<string, string>>()
            {
                Tuple.Create("MessageType", messageType),
                Tuple.Create("Message",message),
                Tuple.Create("From",from),
                Tuple.Create("To",to),
                Tuple.Create("Payload", payload)
            }, tenant, user, dir);

            Log.Information(_sb.ToString());
        }

        public static void LogReminderTick(string reminderName, long tickSequence, DateTime currentTickTime, TimeSpan intervalPeriod, string reminderAsJson, string tenant = null, string user = null)
        {
            var _sb = BuildMessage("Reminder", "Tick", new List<Tuple<string, string>>()
            {
                Tuple.Create("Name", reminderName),
                Tuple.Create("Count", tickSequence + ""),
                Tuple.Create("Current", currentTickTime.ToString("dd/MM/yyyy H:mm:ss")),
                Tuple.Create("Interval", intervalPeriod.TotalSeconds + " seconds"),
                Tuple.Create("Payload", reminderAsJson),
            }, tenant, user);

            Log.Information(_sb.ToString());
        }

        /// <summary>
        /// Build a custom structured log message <br></br>
        /// <i>[title][subtitle][dir][key "value][key2 "value2"][tenant "value"][user "value"]</i>
        /// </summary>
        /// <param name="title"></param>
        /// <param name="subtitle"></param>
        /// <param name="keyValues"></param>
        /// <param name="tenant"></param>
        /// <param name="user"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static StringBuilder BuildMessage(
            string title,
            string subtitle,
            List<Tuple<string, string>> keyValues,
            string tenant = null,
            string user = null,
            MessageDirection dir = MessageDirection.None)
        {

            var _sb = new StringBuilder();

            _sb.Append(GetSection(title));

            if (!string.IsNullOrEmpty(subtitle))
            {
                _sb.Append(GetSection(subtitle));
            }

            if (dir != MessageDirection.None)
            {
                _sb.Append(GetSection(dir.ToString()));
            }

            foreach (var keyValue in keyValues)
            {
                if (!string.IsNullOrEmpty(keyValue.Item2))
                {
                    _sb.Append(GetSectionWithValue(keyValue.Item1, keyValue.Item2));
                }
            }

            if (!string.IsNullOrEmpty(tenant))
            {
                _sb.Append(GetSectionWithValue("Tenant", tenant));
            }
            if (!string.IsNullOrEmpty(user))
            {
                _sb.Append(GetSectionWithValue("User", user));
            }

            return _sb;
        }

        private static string GetSection(string key)
        {
            return $"[{key}]";
        }

        private static string GetSectionWithValue(string key, string value)
        {
            return $@"[{key} ""{value}""]";
        }
    }
}
