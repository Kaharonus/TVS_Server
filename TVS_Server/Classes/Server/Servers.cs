using System;
using System.Collections.Generic;
using System.Text;

namespace TVS_Server
{
    class Servers{
        public static FileServer FileServer { get; set; }
        public static DataServer DataServer { get; set; }

        public static void StartFileServer() {
            FileServer = new FileServer(Settings.FileServerPort,@"C:\");
            FileServer.Start();
        }
        public static void StopFileServer() {
            if (FileServer != null) {
                FileServer.Stop();
            }
        }
        public static void StartDataServer() {
            DataServer = new DataServer(Settings.DataServerPort);
            DataServer.Start();
        }
        public static void StopDataServer() {
            if (DataServer != null) {
                DataServer.Stop();
            }
        }
    }
}
