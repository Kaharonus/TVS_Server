﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;

namespace TVS_Server {
    class Helper {

        public static string HashString16(string input) {
            const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            SHA256Managed hashstring = new SHA256Managed();
            byte[] hash = hashstring.ComputeHash(bytes);
            char[] hash2 = new char[16];
            for (int i = 0; i < hash2.Length; i++) {
                hash2[i] = chars[hash[i] % chars.Length];
            }
            return new string(hash2);
        }

        public static int GetFreeTcpPort() {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public static string HashString(string input) {
    
            using (SHA512 sha = SHA512.Create()) {
                var hashedBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }

        public static MemoryStream StringToStream(string input) { 
            return new MemoryStream(Encoding.UTF8.GetBytes(input));
        }

        public static string GenerateName(int seriesId, int episodeId) {
            var series = Database.GetSeries(seriesId);
            var episode = Database.GetEpisode(seriesId, episodeId);
            string name = "";
            if (episode.AiredSeason < 10) {
                name = episode.AiredEpisodeNumber < 10 ? series.SeriesName + " - S0" + episode.AiredSeason + "E0" + episode.AiredEpisodeNumber + " - " + episode.EpisodeName : name = series.SeriesName + " - S0" + episode.AiredSeason + "E" + episode.AiredEpisodeNumber + " - " + episode.EpisodeName;
            } else if (episode.AiredSeason >= 10) {
                name = episode.AiredEpisodeNumber < 10 ? series.SeriesName + " - S" + episode.AiredSeason + "E0" + episode.AiredEpisodeNumber + " - " + episode.EpisodeName : series.SeriesName + " - S" + episode.AiredSeason + "E" + episode.AiredEpisodeNumber + " - " + episode.EpisodeName;
            }
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char c in invalid) {
                name = name.Replace(c.ToString(), "");
            }
            return name;
        }

        public static object GetDefaultValue(Type t) {
            if (t.IsValueType) {
                return Activator.CreateInstance(t);
            }
            return null;
        }

        public static double ConvertToUnixTimestamp(DateTime date) {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return Math.Floor(diff.TotalSeconds);
        }

        public static bool ParseDate(string date, out DateTime result) {
            if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime success)) {
                result = success;
                return true;
            } else {
                result = DateTime.MinValue;
                return false;
            }
        }
        public static DateTime ParseDate(string date) {
            if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime success)) {
                return success;
            } else {
                return DateTime.MaxValue;
            }
        }



        public static string DateTimeToString(DateTime dt) {
            return dt.ToString("dd. MM. yyyy");
        }

        public static string GetMyIP() {
            string localIP;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }
            return localIP;
        }

        /// <summary>
        /// Returns your IP
        /// </summary>
        /// <param name="ignoreThis">Does not matter what you enter here. I just had to combat C#s inability to overload functions by return value</param>
        /// <returns></returns>
        public static IPAddress GetMyIP(int ignoreThis = 0) {
            IPAddress localIP;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address;
            }
            return localIP;
        }

        public static string GetMacAddress(string ip) {
            if (ip == GetMyIP()) {
                return NetworkInterface.GetAllNetworkInterfaces().Single(x => x.GetIPProperties().UnicastAddresses.Where(y => y.Address.ToString() == ip).Count() > 0).GetPhysicalAddress().ToString();
            } else {
                Process pProcess = new Process {
                    StartInfo = {
                        FileName = "arp",
                        Arguments = "-a ",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                pProcess.Start();
                string cmdOutput = pProcess.StandardOutput.ReadToEnd();
                string pattern = @"(?<ip>([0-9]{1,3}\.?){4})\s*(?<mac>([a-f0-9]{2}-?){6})";
                var result = Regex.Matches(cmdOutput, pattern, RegexOptions.IgnoreCase).Cast<Match>().FirstOrDefault(x => x.Groups["ip"].Value == ip);
                return result != null ? result.Groups["mac"].Value.ToUpper().Replace("-", "") : "";
            }

        }
    }
    public static class Extentions {

        /// <summary>
        /// Returns IEnumerable withouts its last item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static IEnumerable<T> WithoutLast<T>(this IEnumerable<T> source) {
            using (var e = source.GetEnumerator()) {
                if (e.MoveNext()) {
                    for (var value = e.Current; e.MoveNext(); value = e.Current) {
                        yield return value;
                    }
                }
            }
        }


        /// <summary>
        /// Updates all null or default value properties from old with data from new
        /// </summary>
        public static void UpdateObject(this object oldObj, object newObj) {
            if (oldObj.GetType() == newObj.GetType()) {
                var properties = oldObj.GetType().GetProperties();
                foreach (var property in properties) {
                    if ((property.GetValue(oldObj) == Helper.GetDefaultValue(property.GetType()) || (property.GetType() == typeof(string) && String.IsNullOrEmpty((string)property.GetValue(oldObj)))) && property.Name != "FirstAired") {
                        property.SetValue(oldObj, property.GetValue(newObj));
                    }
                }
            }
        }

        public static async Task WaitAll(this IEnumerable<Task> tasks) {
            await Task.Run(() => Task.WaitAll(tasks.ToArray()));
        }

        public static void StartAll(this IEnumerable<Task> tasks) {
            foreach (var task in tasks) {
                task.Start();
            }
        }

        public static string ChopOffBefore(this string s, string before) {
            int end = s.ToUpper().IndexOf(before.ToUpper());
            return end > -1 ? s.Substring(end + before.Length) : s;
        }

        public static string ChopOffAfter(this string s, string after) {
            int end = s.ToUpper().IndexOf(after.ToUpper());
            return end > -1 ? s.Substring(0, end) : s;
        }

        public static string ReplaceIgnoreCase(this string source, string pattern, string replacement) {// using \\$ in the pattern will screw this regex up
                                                                                                        //return Regex.Replace(Source, Pattern, Replacement, RegexOptions.IgnoreCase);

            if (Regex.IsMatch(source, pattern, RegexOptions.IgnoreCase))
                source = Regex.Replace(source, pattern, replacement, RegexOptions.IgnoreCase);
            return source;
        }

    }

    public class PrivateData : Attribute {
    }

}
