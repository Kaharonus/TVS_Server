using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;

namespace TVS_Server
{
    class TVDbBase
    {
        /// <summary>
        /// Returns valid token. In case of errors returns null
        /// </summary>
        /// <returns>TVDB API Token</returns>
        private static string GetToken() {
            if (Settings.TokenTimestamp == null) {
                Settings.TokenTimestamp = new DateTime(1900, 1, 1);
            }
            if (Settings.TokenTimestamp.AddDays(1) < DateTime.Now) {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.thetvdb.com/login");
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                try {
                    using (var streamWriter = new StreamWriter(request.GetRequestStream())) {
                        string data = "{\"apikey\": \"0152EADCC5783DF5\",\"username\": \"TVS\",\"userkey\": \"529945F8E3C41A4A\"}";
                        streamWriter.Write(data);
                    }
                } catch (WebException) { return null; }
                string text;
                try {
                    var response = request.GetResponse();
                    using (var sr = new StreamReader(response.GetResponseStream())) {
                        text = sr.ReadToEnd();
                        text = text.Remove(text.IndexOf("\"token\""), "\"token\"".Length);
                        text = text.Split('\"', '\"')[1];
                        Settings.Token = text;
                        Settings.TokenTimestamp = DateTime.Now;
                        return text;
                    }
                } catch (Exception) { return null; }
            } else {
                return Settings.Token;
            }

        }

        public static HttpWebRequest GetRequest(string link) {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(link);
            request.Method = "GET";
            request.Accept = "application/json";
            request.Headers.Add("Accept-Language", "en");
            request.Headers.Add("Authorization", "Bearer " + GetToken());
            return request;
        }
    }
}

