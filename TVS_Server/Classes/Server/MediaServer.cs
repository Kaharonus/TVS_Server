﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;

namespace TVS_Server {
    public class MediaServer {

        public static Dictionary<string, string> FileCodeDictionary { get; set; } = new Dictionary<string, string>();
        public int LoopCount = 0;//Counts the number of chunks of data that we have sent to the client DLNA device
        public int ClientCount = 0;//Count of client sockets we are serving at the current time
        public bool Running = false;//Flag set to true when running and false to kill the service
        public string IP;//The ip of this service we will listen on for DLNA requests
        public int Port;//The post we will listen on for incoming DLNA requests 
        private long LastFileLength = 0;
        private string LastFileName = "";//Sometimes we keep the file-stream open so need to know the name of the last file served up
        private FileStream FS = null;
        private Socket SocServer = null;
        private Thread TH = null;
        private long TempRange = 0;        //Past to the client thread ready to service the request
        private Socket TempClient = null;  //Past to the client thread ready to service the request
        private string TempFileName = "";  //Past to the client thread ready to service the request

        public void Start() {//Starts our DLNA service
            IP = Helper.GetMyIP();
            Port = Settings.MediaServerPort;
            this.Running = true;
            LoopCount = 0; ClientCount = 0;
            this.TH = new Thread(Listen);
            TH.Start();
        }

        public void Stop() {//Stops our DLNA service
            this.Running = false;
            Thread.Sleep(100);
            if (this.FS != null) { try { FS.Close(); } catch {; } }
            if (SocServer != null && SocServer.Connected) SocServer.Shutdown(SocketShutdown.Both);
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

        private string EncodeUrl(string Value) {//Encode requests sent to the DLNA device
            if (Value == null) return null;
            return Value.Replace(" ", "%20").Replace("&", "%26").Replace("'", "%27").Replace("\\", "/");
        }

        private void SendHeadData(Socket Soc, string FileName) {//This runs in the same thread as the service since it should be nice and fast
            FileInfo FInfo = new FileInfo(FileName);
            if (!FInfo.Exists) {//We cannot find the file so just dump the connection
                Soc.Close();
                this.TempClient = null;//Flag also so the next request can be serviced
                return;
            }
            string ContentType = GetContentType(FileName);
            string Reply = "HTTP/1.1 200 OK" + Environment.NewLine + "Server: VLC" + Environment.NewLine + "Content-Type: " + ContentType + Environment.NewLine;
            Reply += "Last-Modified: " + GMTTime(DateTime.Now.AddYears(-1).AddDays(-7)) + Environment.NewLine;//Just dream up a date
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
            while (this.Running) {
                SocServer.Listen(0);
                if (this.TempClient != null) Thread.Sleep(500);//Wait so last thread can get started first
                TempClient = SocServer.Accept();
                Thread.Sleep(250);
                byte[] Buf = new byte[3000];
                int Size = TempClient.Receive(Buf, SocketFlags.None);
                MemoryStream MS = new MemoryStream();
                MS.Write(Buf, 0, Size);
                string Request = UTF8Encoding.UTF8.GetString(MS.ToArray());
                var requestUrl = Request.ChopOffBefore("GET /").ChopOffAfter("HTTP/1.").Trim();
                if (FileCodeDictionary.ContainsKey(requestUrl)) {
                    if (Request.ToUpper().StartsWith("HEAD /") && Request.ToUpper().IndexOf("HTTP/1.") > -1) {//Samsung TV
                        string HeadFileName = FileCodeDictionary[requestUrl];
                        SendHeadData(TempClient, HeadFileName);
                    } else if (Request.ToUpper().StartsWith("GET /") && Request.ToUpper().IndexOf("HTTP/1.") > -1) {
                        bool HasRange = false;
                        TempFileName = FileCodeDictionary[requestUrl];
                        if (Request.ToLower().IndexOf("range: ") > -1) {
                            HasRange = true;//We can stream this if it's a movie using ranges
                            string Range = Request.ToLower().ChopOffBefore("range: ").ChopOffAfter("-").ChopOffAfter(Environment.NewLine).Replace("bytes=", "");
                            long.TryParse(Range, out TempRange);
                        } else
                            TempRange = 0;
                        if (HasRange) {
                            Thread THStream = new Thread(StreamMovie);
                            THStream.Start();
                        }
                    } else {
                        TempClient.Close();
                    }
                } else {
                    TempClient.Close();
                }

            }
        }

      



        private string GetContentType(string FileName) {//Based on the file type we create our content type for the reply to the TV/DLNA device
            string ContentType = "audio/mpeg";
            if (FileName.ToLower().EndsWith(".mkv")) ContentType = "video/mkv";
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
            this.TempClient = null;//Server is ready to recive more requests now
            if (LastFileName != FileName) //Should use a lock here but i will risk it
            {
                if (!File.Exists(FileName)) { ClientCount--; Client.Close(); return; }
                if (FS != null) FS.Close();
                FileInfo FInfo = new FileInfo(FileName);
                LastFileLength = FInfo.Length;
                FS = new FileStream(FileName, FileMode.Open);
                LastFileName = FileName;
            }
            string Reply = ContentString(Range, ContentType, LastFileLength);
            Client.Send(UTF8Encoding.UTF8.GetBytes(Reply), SocketFlags.None);
            byte[] Buf = new byte[ChunkSize];
            if (FS.CanSeek)
                FS.Seek(Range, SeekOrigin.Begin);
            BytesSent = Range;
            while (this.Running && Client.Connected && ByteToSend > 0) {//Keep looping untill all the data is sent or the connection is dropped by the client
                LoopCount++;
                ByteToSend = LastFileLength - BytesSent;
                if (ByteToSend > ChunkSize) ByteToSend = ChunkSize;
                long BytesLeftToSend = LastFileLength - BytesSent;
                if (BytesSent + ChunkSize > LastFileLength)
                    ChunkSize = LastFileLength - BytesSent;
                Buf = new byte[ByteToSend];
                FS.Read(Buf, 0, Buf.Length);
                BytesSent += Buf.Length;
                if (Client.Connected) {
                    try { Client.Send(Buf); Thread.Sleep(100); } catch { ByteToSend = 0; }//Force an exit}
                }
            }
            if (!this.Running) { try { FS.Close(); FS = null; } catch {; } }
            Client.Close();
            ClientCount--;
        }
    }
}
