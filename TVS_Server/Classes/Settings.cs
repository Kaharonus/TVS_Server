using Avalonia.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace TVS_Server
{
    static class Settings {
        private static string _token;
        private static string _libraryLocation;
        private static string _downloadLocation;
        private static string _scanLocation1;
        private static string _scanLocation2;
        private static string _scanLocation3;
        private static int _dataServerPort;
        private static int _fileServerPort;
        private static DateTime _tokenTimestamp;
        private static DateTime _databaseUpdateTime;
        private static bool _saveRam;

        private static bool _setupComplete = false;

        /// <summary>
        /// Token for logging in into TheTVDB API
        /// </summary>
        public static string Token { get => _token; set { _token = value; SaveSettings(); } }

        /// <summary>
        /// Date time that stores time when token was last retrieved
        /// </summary>
        public static DateTime TokenTimestamp { get => _tokenTimestamp; set { _tokenTimestamp = value; SaveSettings(); } }

        /// <summary>
        /// Date time that stores time when was database updated for the last time
        /// </summary>
        public static DateTime DatabaseUpdateTime { get => _databaseUpdateTime; set { _databaseUpdateTime = value; SaveSettings(); } }

        /// <summary>
        /// Indicates if user has already passed the entire setup process
        /// </summary>
        public static bool SetupComplete { get => _setupComplete; set { _setupComplete = value; SaveSettings(); } }

        /// <summary>
        /// Location of library. All files like episodes and subtitles are stored here
        /// </summary>
        public static string LibraryLocation { get => _libraryLocation; set { _libraryLocation = value; SaveSettings(); } }

        /// <summary>
        /// Location of cache for torrent downloading. While something gets downloaded it gets stored here
        /// </summary>
        public static string DownloadLocation { get => _downloadLocation; set { _downloadLocation = value; SaveSettings(); } }

        /// <summary>
        /// Extra folder that will be scanned for episodes and subtitles
        /// </summary>
        public static string ScanLocation1 { get => _scanLocation1; set { _scanLocation1 = value; SaveSettings(); } }

        /// <summary>
        /// Extra folder that will be scanned for episodes and subtitles
        /// </summary>
        public static string ScanLocation2 { get => _scanLocation2; set { _scanLocation2 = value; SaveSettings(); } }

        /// <summary>
        /// Extra folder that will be scanned for episodes and subtitles
        /// </summary>
        public static string ScanLocation3 { get => _scanLocation3; set { _scanLocation3 = value; SaveSettings(); } }


        /// <summary>
        /// Specifies port that file server will run at. Default value is 5851
        /// </summary>
        public static int FileServerPort { get => _fileServerPort; set { _fileServerPort = value; SaveSettings(); } }

        /// <summary>
        /// Specifies port that data server will run at. Default value is 5850
        /// </summary>
        public static int DataServerPort { get => _dataServerPort; set { _dataServerPort = value; SaveSettings(); } }

        public static bool SaveRam {get => _saveRam; set { _saveRam = value; SaveSettings(); } }


        /// <summary>
        /// Saves Settings. Is called automatically whenever property value is changed
        /// </summary>
        public static void SaveSettings() {
            Type type = typeof(Settings);
            string filename = Database.DatabasePath + "Settings.TVSData";
            if (!File.Exists(filename)) {
                File.Create(filename).Dispose();
            }
            while(true) {
                try {
                    FieldInfo[] properties = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic);
                    object[,] a = new object[properties.Length, 2];
                    int i = 0;
                    foreach (FieldInfo field in properties) {
                        a[i, 0] = field.Name;
                        a[i, 1] = field.GetValue(null);
                        i++;
                    };
                    string json = JsonConvert.SerializeObject(a);
                    StreamWriter sw = new StreamWriter(filename);
                    sw.Write(json);
                    sw.Close();
                    return;
                } catch (IOException e) {
                    Thread.Sleep(15);
                }
            }
        }

        /// <summary>
        /// Loads settings with default value if new settings has been added. In case of enums edit code - might get "crashy" if you dont
        /// </summary>
        public static void LoadSettings() {
            Type type = typeof(Settings);
            string filename = Database.DatabasePath + "Settings.TVSData";
            if (!File.Exists(filename)) {
                File.Create(filename).Dispose();
            }
            while (true) { 
                try {
                    FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic);
                    object[,] a;
                    StreamReader sr = new StreamReader(filename);
                    string json = sr.ReadToEnd();
                    sr.Close();
                    if (!String.IsNullOrEmpty(json)) {
                        JArray ja = JArray.Parse(json);
                        a = ja.ToObject<object[,]>();
                        if (a.GetLength(0) != fields.Length) { }
                        int i = 0;
                        foreach (FieldInfo field in fields) {
                            try {
                                if (field.Name == (a[i, 0] as string)) {
                                    field.SetValue(null, Convert.ChangeType(a[i, 1], field.FieldType));
                                }
                            } catch (IndexOutOfRangeException) {
                                field.SetValue(null, GetDefault(field.FieldType));
                            }
                            i++;
                        };
                    }
                    return;
                } catch (IOException e) {
                    Thread.Sleep(15);
                }
            }
        }

        public static object GetDefault(Type type) {
            if (type.IsValueType) {
                return Activator.CreateInstance(type);
            }
            return null;
        }

    }
}
