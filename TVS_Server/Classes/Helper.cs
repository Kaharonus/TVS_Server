using System;
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
                Process pProcess = new Process();
                pProcess.StartInfo.FileName = "arp";
                pProcess.StartInfo.Arguments = "-a ";
                pProcess.StartInfo.UseShellExecute = false;
                pProcess.StartInfo.RedirectStandardOutput = true;
                pProcess.StartInfo.CreateNoWindow = true;
                pProcess.Start();
                string cmdOutput = pProcess.StandardOutput.ReadToEnd();
                string pattern = @"(?<ip>([0-9]{1,3}\.?){4})\s*(?<mac>([a-f0-9]{2}-?){6})";
                var result = Regex.Matches(cmdOutput, pattern, RegexOptions.IgnoreCase).Cast<Match>().Where(x => x.Groups["ip"].Value == ip).FirstOrDefault();
                if (result != null) {
                    return result.Groups["mac"].Value.ToUpper().Replace("-", "");
                } else {
                    return "";
                }
            }

        }
    }
    public static class Extentions {


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

        public async static Task WaitAll(this IEnumerable<Task> tasks) {
            await Task.Run(() => Task.WaitAll(tasks.ToArray()));
        }

        public static void StartAll(this IEnumerable<Task> tasks) {
            foreach (var task in tasks) {
                task.Start();
            }
        }

        public static string ChopOffBefore(this string s, string Before) {
            int End = s.ToUpper().IndexOf(Before.ToUpper());
            if (End > -1) {
                return s.Substring(End + Before.Length);
            }
            return s;
        }

        public static string ChopOffAfter(this string s, string After) {
            int End = s.ToUpper().IndexOf(After.ToUpper());
            if (End > -1) {
                return s.Substring(0, End);
            }
            return s;
        }

        public static string ReplaceIgnoreCase(this string Source, string Pattern, string Replacement) {// using \\$ in the pattern will screw this regex up
                                                                                                        //return Regex.Replace(Source, Pattern, Replacement, RegexOptions.IgnoreCase);

            if (Regex.IsMatch(Source, Pattern, RegexOptions.IgnoreCase))
                Source = Regex.Replace(Source, Pattern, Replacement, RegexOptions.IgnoreCase);
            return Source;
        }

    }

    public class PrivateData : System.Attribute {

    }

}
