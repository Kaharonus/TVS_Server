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

namespace TVS_Server {
    class Helper {

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
