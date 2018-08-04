using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HttpListener = System.Net.Http.HttpListener;
using NETStandard.HttpListener;
using System.Text.RegularExpressions;
using System.Diagnostics;
namespace TVS_Server
{
    class DataServer
    {
        public int Port { get; set; }
        public string IP { get; set; }
        public bool IsRunning { get; set; }
        private HttpListener Listener { get; set; }

        public DataServer(int port) {
            Port = port;
            IsRunning = false;
            IP = Helper.GetMyIP();

        }


        public void Stop() {
            Listener.Close();
            Log.Write("API Stopped");
        }

        public void Start() {
            if (Listener == null) {
                Listener = new HttpListener(System.Net.IPAddress.Parse(IP), Port);
                Listener.Request += (s, ev) => Listen(ev);
            }
            Listener.Start();
            Log.Write("API Started @ " + IP + ":" + Port);
        }

        public void Listen(HttpListenerRequestEventArgs context) {
            Task.Run(async () => {
                await context.Response.RedirectAsync(new Uri("http://192.168.1.83:8081/test.mkv"));
                //context.Response.Close();
            });
        }      
    }
}
