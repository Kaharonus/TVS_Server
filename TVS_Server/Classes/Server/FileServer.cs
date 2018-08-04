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
        public List<string> Clients { get; set; } = new List<string>();
        public int LoopCount { get; set; }
        public int ClientCount { get; set; }
        public bool IsRunning { get; set; } = false;//Flag set to true when running and false to kill the service
        public string IP { get; set; }//The ip of this service we will listen on for DLNA requests
        public int Port { get; set; }//The post we will listen on for incoming DLNA requests 
        public string RootMovies { get; set; } //Points towards the root path of your media collection

        private long LastFileLength = 0;
        private string LastFileName = "";//Sometimes we keep the file-stream open so need to know the name of the last file served up
        private FileStream FS = null;
        private Socket SocServer = null;
        private Thread TH = null;
        private long TempRange = 0;        //Past to the client thread ready to service the request
        private Socket TempClient = null;  //Past to the client thread ready to service the request
        private string TempFileName = "";  //Past to the client thread ready to service the request

        public FileServer(int port, string rootMovies) {
            this.IP = Helper.GetMyIP();
            this.Port = port;
            this.RootMovies = rootMovies;
            if (!this.RootMovies.EndsWith("\\")) this.RootMovies += "\\";
        }

        public void Start() {//Starts our DLNA service
            if (!Directory.Exists(this.RootMovies) || this.IsRunning) return;
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

        private string SafeReadClient(Socket Soc) {//Safe way to read incoming requests for DLNA stream data or HTTP HEAD requests
            try {
                byte[] Buf = new byte[Soc.Available];
                Soc.Receive(Buf);
                return UTF8Encoding.UTF8.GetString(Buf);
            } catch {; }
            return "";
        }

        private string ContentString(long Range, string ContentType, long FileLength) {//Builds up our HTTP reply string for byte-range requests
            string Reply = "";
            Reply = "HTTP/1.1 206 Partial Content" + Environment.NewLine + "Server: VLC" + Environment.NewLine + "Content-Type: " + ContentType + Environment.NewLine;
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

        private bool IsMusicOrImage(string FileName) {//We don't want to use byte-ranges for music or image data so we test the filename here
            if (FileName.ToLower().EndsWith(".jpg") || FileName.ToLower().EndsWith(".png") || FileName.ToLower().EndsWith(".gif") || FileName.ToLower().EndsWith(".mp3"))
                return true;
            return false;
        }

        private string GMTTime(DateTime Time) {//Covert date to GMT time/date
            string GMT = Time.ToString("ddd, dd MMM yyyy HH':'mm':'ss 'GMT'");
            return GMT;//Example "Sat, 25 Jan 2014 12:03:19 GMT";
        }

        public string MakeBaseUrl(string DirectoryName) {//Helper function to make the base url thats past to the DNLA device so that it can talk to this media service
            DirectoryName = DirectoryName.ChopOffBefore(this.RootMovies);
            DirectoryName = EncodeUrl(DirectoryName);
            string Url = "http://" + this.IP + ":" + this.Port + "/" + DirectoryName + "/";
            if (Url.EndsWith("//")) return Url.Substring(0, Url.Length - 1);
            return Url;
        }//Returns something like http://192.168.0.10:9090/Action%20Films/

        private string EncodeUrl(string Value) {//Encode requests sent to the DLNA device
            if (Value == null) return null;
            return Value.Replace(" ", "%20").Replace("&", "%26").Replace("'", "%27").Replace("\\", "/");
        }

        private string DecodeUrl(string Value) {//Decode request from the DLNA device
            if (Value == null) return null;
            return Value.Replace("%20", " ").Replace("%26", "&").Replace("%27", "'").Replace("/", "\\");
        }

        private void SendHeadData(Socket Soc, string FileName) {//This runs in the same thread as the service since it should be nice and fast
            FileInfo FInfo = new FileInfo(FileName);
            if (!FInfo.Exists) {//We cannot find the file so just dump the connection
                Soc.Close();
                this.TempClient = null;//Flag also so the next request can be serviced
                return;
            }
            string ContentType = GetContentType(FileName);
            string Reply = "HTTP/1.1 200 OK" + Environment.NewLine + "Content-Type: " + ContentType + Environment.NewLine;
            Reply += "Date: " + GMTTime(DateTime.Now) + Environment.NewLine;
            if (!IsMusicOrImage(FileName)) Reply += "Accept-Ranges: bytes" + Environment.NewLine;//We only do ranges for movies
            Reply += "Content-Length: " + FInfo.Length + Environment.NewLine;
            Reply += "Connection: close" + Environment.NewLine + Environment.NewLine;
            Soc.Send(UTF8Encoding.UTF8.GetBytes(Reply), SocketFlags.None);
            Soc.Close();
            this.TempClient = null;
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
                    if (Request.ToUpper().StartsWith("HEAD /") && Request.ToUpper().IndexOf("HTTP/1.") > -1) {//Samsung TV
                        string HeadFileName = RootMovies + Request.ChopOffBefore("HEAD /").ChopOffAfter("HTTP/1.").Trim().Replace("/", "\\");
                        SendHeadData(TempClient, HeadFileName);
                    } else if (Request.ToUpper().StartsWith("GET /") && Request.ToUpper().IndexOf("HTTP/1.") > -1) {
                        bool HasRange = false;
                        TempFileName = this.RootMovies + Request.ChopOffBefore("GET /").ChopOffAfter("HTTP/1.").Trim();
                        TempFileName = DecodeUrl(TempFileName);
                        if (Request.ToLower().IndexOf("range: ") > -1) {
                            HasRange = true;//We can stream this if it's a movie using ranges
                            string Range = Request.ToLower().ChopOffBefore("range: ").ChopOffAfter("-").ChopOffAfter(Environment.NewLine).Replace("bytes=", "");
                            long.TryParse(Range, out TempRange);
                        } else
                            TempRange = 0;
                        if (!HasRange || TempFileName.ToLower().EndsWith(".jpg") || TempFileName.ToLower().EndsWith(".png") || TempFileName.ToLower().EndsWith(".gif") || TempFileName.ToLower().EndsWith(".mp3")) {
                            Thread THSend = new Thread(SendFile);
                            THSend.Start();
                        } else {//Movies need to be streamed for best results if they use a byte-range
                            Thread THStream = new Thread(StreamMovie);
                            THStream.Start();
                        }
                    } else
                        TempClient.Close();
                } catch { }
            }
        }

        private void SendFile() {//Here we just send the file without using ranges and this function runs in it's own thread
            FileStream FSFile = null;
            long ChunkSize = 50000;
            long BytesSent = 0;
            string FileName = TempFileName.ToLower();
            string ContentType = GetContentType(FileName);
            Socket Client = this.TempClient;
            this.TempClient = null;//Server is ready to recive more requests now
            if (!File.Exists(FileName)) { Client.Close(); return; }
            ClientCount++;
            FileInfo FInfo = new FileInfo(FileName);
            if (FInfo.Length > 8000000)
                ChunkSize = 500000;//Looks big like a movie so increase the chunk size
            string Reply = "HTTP/1.1 200 OK" + Environment.NewLine + "Content-Type: " + ContentType + Environment.NewLine;
            Reply += "Connection: close" + Environment.NewLine;
            Reply += "Content-Length: " + FInfo.Length + Environment.NewLine + Environment.NewLine;
            if (LastFileName != FileName || FS == null)//Should use a lock here but i will risk it
            {
                FSFile = new FileStream(FileName, FileMode.Open);//needs work incase open by another request and locked
                LastFileName = FileName;
            } else {
                FSFile = FS;
                FS.Seek(0, SeekOrigin.Begin);
            }
            Client.Send(UTF8Encoding.UTF8.GetBytes(Reply), SocketFlags.None);
            while (this.IsRunning && Client.Connected && ChunkSize > 0) {//Keep looping untill all the data is sent or the connection is dropped by the client
                LoopCount++;
                if (BytesSent + ChunkSize > FInfo.Length)
                    ChunkSize = FInfo.Length - BytesSent;
                byte[] Buf = new byte[ChunkSize];

                if (Client.Connected) {
                    try { FSFile.Read(Buf, 0, Buf.Length); Client.Send(Buf); } catch { ChunkSize = 0; }//Will force exit of the loop
                }
                BytesSent += Buf.Length;
                if (ChunkSize > 0) Thread.Sleep(100);
            }
            ClientCount--;
            FSFile.Close();
            Client.Close();
        }

        private string GetContentType(string FileName) {//Based on the file type we create our content type for the reply to the TV/DLNA device
            string ContentType = "audio/mpeg";
            if (FileName.ToLower().EndsWith(".jpg")) ContentType = "image/jpg";
            else if (FileName.ToLower().EndsWith(".png")) ContentType = "image/png";
            else if (FileName.ToLower().EndsWith(".gif")) ContentType = "image/gif";
            else if (FileName.ToLower().EndsWith(".avi")) ContentType = "video/avi";
            if (FileName.ToLower().EndsWith(".mp4")) ContentType = "video/mp4";
            return ContentType;
        }

        private void StreamMovie() {//Streams a movie using ranges and runs on it's own thread
            ClientCount++;
            long ChunkSize = 500000;
            long Range = TempRange;
            long BytesSent = 0;
            long ByteToSend = 1;
            string FileName = TempFileName.ToLower();
            string ContentType = GetContentType(FileName);
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
    }
}
