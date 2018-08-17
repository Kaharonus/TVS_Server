using System;
using System.Collections.Generic;
using System.Text;

namespace TVS_Server
{
    class Servers{
        public static FileServer FileServer { get; set; } = new FileServer();
        public static DataServer DataServer { get; set; } = new DataServer();

        public static void StartFileServer() {
            FileServer = new FileServer();
            FileServer.Start();
        }
        public static void StopFileServer() {
            if (FileServer != null) {
                FileServer.Stop();
            }
        }
        public static void StartDataServer() {
            DataServer = new DataServer();
            DataServer.Start();
        }
        public static void StopDataServer() {
            if (DataServer != null) {
                DataServer.Stop();
            }
        }
    }
}
