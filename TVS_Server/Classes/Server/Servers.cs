using System;
using System.Collections.Generic;
using System.Text;

namespace TVS_Server
{
    class Servers{
        public static MediaServer MediaServer { get; set; }
        public static DataServer DataServer { get; set; } = new DataServer();

        public static void StartFileServer() {
            MediaServer = new MediaServer();
            MediaServer.Start();
        }
        public static void StopFileServer() {

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
