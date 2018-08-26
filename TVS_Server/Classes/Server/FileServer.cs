using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TVS_Server
{
    public class FileServer {
        public static Dictionary<string, string> FileDictionary = new Dictionary<string, string>();

        public List<string> Clients { get; set; } = new List<string>();
        public int LoopCount { get; set; }
        public int ClientCount { get; set; }
        public bool IsRunning { get; set; } = false; //Flag set to true when running and false to kill the service
        public string IP { get; set; } = Helper.GetMyIP();
        public int Port { get; set; } = Settings.FileServerPort;
        private long LastFileLength = 0;
        private string LastFileName = "";//Sometimes we keep the file-stream open so need to know the name of the last file served up
        private FileStream FS = null;
        private Socket SocServer = null;
        private Thread TH = null;
        private long TempRange = 0;        //Past to the client thread ready to service the request
        private Socket TempClient = null;  //Past to the client thread ready to service the request
        private string TempFileName = "";  //Past to the client thread ready to service the request

        public void Start() {//Starts our DLNA service
            if (this.IsRunning) return;
            this.IsRunning = true;
            LoopCount = 0; ClientCount = 0;
            this.TH = new Thread(Listen);
            TH.Start();
            Log.Write("Media server started @ " + IP + ":" + Port);
        }

        public void Stop() {//Stops our DLNA service
            this.IsRunning = false;
            Thread.Sleep(100);
            if (this.FS != null) { try { FS.Close(); } catch {; } }
            if (SocServer != null && SocServer.Connected) SocServer.Shutdown(SocketShutdown.Both);
            SocServer.Dispose();
            Log.Write("Media server stopped");
        }

        private void Listen() {//This is the main service that waits for bew incoming request and then service the requests on another thread in most cases
            SocServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint IPE = new IPEndPoint(IPAddress.Parse(this.IP), this.Port);
            SocServer.Bind(IPE);
            while (this.IsRunning) {
                try {
                    SocServer.Listen(0);
                    if (this.TempClient != null) Thread.Sleep(500);//Wait so last thread can get started first
                    TempClient = SocServer.Accept();
                    Thread.Sleep(250);
                    byte[] Buf = new byte[3000];
                    int Size = TempClient.Receive(Buf, SocketFlags.None);
                    MemoryStream MS = new MemoryStream();
                    MS.Write(Buf, 0, Size);
                    string Request = UTF8Encoding.UTF8.GetString(MS.ToArray());
                    if (Request.ToUpper().StartsWith("GET /") && Request.ToUpper().IndexOf("HTTP/1.") > -1) {
                        TempFileName = "";
                        var requestedFile = Request.ChopOffBefore("GET /").ChopOffAfter("HTTP/1.").Trim();
                        if (FileDictionary.ContainsKey(requestedFile)) {
                            TempFileName = FileDictionary[requestedFile];
                        }
                        TempFileName = DecodeUrl(TempFileName);
                        Thread THStream = new Thread(StreamMovie);
                        THStream.Start();
                    } else {
                        TempClient.Close();
                    }
                } catch { }
            }
        }

        private void StreamMovie() {//Streams a movie using ranges and runs on it's own thread
            ClientCount++;
            long ChunkSize = 500000;
            long Range = TempRange;
            long BytesSent = 0;
            long ByteToSend = 1;
            string FileName = TempFileName.ToLower();
            string ContentType = "video/" + Path.GetExtension(FileName).Replace(".", "");
            Socket Client = this.TempClient;
            var client = ((IPEndPoint)Client.RemoteEndPoint).Address.ToString() + ":" + ((IPEndPoint)Client.RemoteEndPoint).Port;
            Clients.Add(client);
            this.TempClient = null;//Server is ready to recive more requests now
            if (!File.Exists(FileName)) { ClientCount--; Client.Close(); return; }
            if (FS != null) FS.Close();
            FileInfo FInfo = new FileInfo(FileName);
            LastFileLength = FInfo.Length;
            FS = new FileStream(FileName, FileMode.Open);
            LastFileName = FileName;
            string Reply = ContentString(Range, ContentType, LastFileLength);
            Client.Send(UTF8Encoding.UTF8.GetBytes(Reply), SocketFlags.None);
            byte[] Buf = new byte[ChunkSize];
            if (FS.CanSeek)
                FS.Seek(Range, SeekOrigin.Begin);
            BytesSent = Range;
            while (this.IsRunning && Client.Connected && ByteToSend > 0) {//Keep looping untill all the data is sent or the connection is dropped by the client
                LoopCount++;
                ByteToSend = LastFileLength - BytesSent;
                if (ByteToSend > ChunkSize) ByteToSend = ChunkSize;
                long BytesLeftToSend = LastFileLength - BytesSent;
                if (BytesSent + ChunkSize > LastFileLength)
                    ChunkSize = LastFileLength - BytesSent;
                Buf = new byte[ByteToSend];
                try {
                    FS.Read(Buf, 0, Buf.Length);
                } catch (Exception e) {
                    Log.Write(e.Message);
                }
                BytesSent += Buf.Length;
                if (Client.Connected) {
                    try { Client.Send(Buf); Thread.Sleep(100); } catch { ByteToSend = 0; }//Force an exit}
                }
            }
            if (!this.IsRunning) { try { FS.Close(); FS = null; } catch {; } }
            Client.Close();
            Clients.Remove(client);
            ClientCount--;
        }


        private string ContentString(long Range, string ContentType, long FileLength) {//Builds up our HTTP reply string for byte-range requests
            string Reply = "";
            Reply = "HTTP/1.1 206 Partial Content" + Environment.NewLine + "Server: TVS_Player" + Environment.NewLine + "Content-Type: " + ContentType + Environment.NewLine;
            Reply += "Accept-Ranges: bytes" + Environment.NewLine;
            Reply += "Date: " + GMTTime(DateTime.Now) + Environment.NewLine;
            if (Range == 0) {
                Reply += "Content-Length: " + FileLength + Environment.NewLine;
                Reply += "Content-Range: bytes 0-" + (FileLength - 1) + "/" + FileLength + Environment.NewLine;
            } else {
                Reply += "Content-Length: " + (FileLength - Range) + Environment.NewLine;
                Reply += "Content-Range: bytes " + Range + "-" + (FileLength - 1) + "/" + FileLength + Environment.NewLine;
            }
            return Reply + Environment.NewLine;
        }

        private string GMTTime(DateTime Time) {//Covert date to GMT time/date
            string GMT = Time.ToString("ddd, dd MMM yyyy HH':'mm':'ss 'GMT'");
            return GMT;//Example "Sat, 25 Jan 2014 12:03:19 GMT";
        }

        private string DecodeUrl(string Value) {//Decode request from the DLNA device
            if (Value == null) return null;
            return Value.Replace("%20", " ").Replace("%26", "&").Replace("%27", "'").Replace("/", "\\");
        }

    
    }
}
