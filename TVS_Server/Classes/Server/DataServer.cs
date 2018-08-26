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
using System.Linq;
using Newtonsoft.Json;

namespace TVS_Server
{
    class DataServer
    {
        public int Port { get; set; } = Settings.DataServerPort;
        public string IP { get; set; } = Helper.GetMyIP();
        public bool IsRunning { get; set; } = false;
        private HttpListener Listener { get; set; }

        public void Stop() {
            Listener.Close();
            Log.Write("API Stopped");
        }

        public void Start() {
            if (Listener == null) {
                Listener = new HttpListener(System.Net.IPAddress.Parse(IP), Port);
                Listener.Request += (s, ev) => HandleRequest(ev);
            }
            Listener.Start();
            Log.Write("API Started @ " + IP + ":" + Port);
        }

        private async Task HandleRequest(HttpListenerRequestEventArgs context) {
            await Task.Run(async () => {
                try {
                    Log.Write(context.Request.HttpMethod + " - " + context.Request.RemoteEndpoint.ToString() + " - " + context.Request.Url.PathAndQuery);
                    if (context.Request.Url.LocalPath == "/") {
                        HandleServerInfo(context);
                    } else {
                        switch (context.Request.Url.Segments[1].Replace("/", "").ToLower()) {
                            case "api":
                                HandleApi(context);
                                break;
                            case "file":
                                await HandleFile(context);
                                break;
                            case "image":
                                await HandleImage(context);
                                break;
                            case "register":
                                HandleUser(context, true);
                                break;
                            case "login":
                                HandleUser(context, false);
                                break;
                            default:
                                HandleNotFound(context);
                                break;
                        }
                    }                  
                } catch (Exception e) {
                    Log.Write("Internal server error: " + e.Message + e.StackTrace);
                    HandleInternalError(context);
                }
            });            
        }

        private void HandleApi(HttpListenerRequestEventArgs context) {
            var method = context.Request.HttpMethod.ToLower();
            if (method == "get" || method == "post") {
                if (IsAuthorized(context, out User user)) {
                    var result = method == "get" ? Api.Get.Response(context.Request.Url, user) : "";
                    if (!String.IsNullOrEmpty(result)) {
                        HandleReturn(context, result);
                    } else {
                        HandleNotFound(context);
                    }
                } else {
                    HandleError(context, 401, "Not authorized.");
                }
            } else {
                HandleMethodNotAllowed(context);
            }
        }

        private async Task HandleImage(HttpListenerRequestEventArgs context) {
            if (context.Request.HttpMethod.ToLower() == "get") {
                var str = await Api.Images.GetStream(context.Request.Url);
                if (str != Stream.Null) {
                    await HandleReturn(context, str);
                } else {
                    HandleNotFound(context);
                }
            } else {
                HandleMethodNotAllowed(context);
            }
        }

        private async Task HandleFile(HttpListenerRequestEventArgs context) {
            if (Servers.FileServer.IsRunning) {
                if (context.Request.HttpMethod.ToLower() == "get") {
                    var file = Api.Files.GetFile(context.Request.Url);
                    if (file != default && file.FileType == "Video") {
                        await context.Response.RedirectAsync(Api.Files.GetRedirectUrl(file));
                    }else if (file != default && file.FileType == "Subtitle") {
                        HandleReturn(context, await Api.Files.ReturnSubitile(file));
                    }
                } else {
                    HandleMethodNotAllowed(context);
                }
            } else {
                HandleError(context, 500, "Server for file transfer is not running");
                Log.Write("Error: " + context.Request.RemoteEndpoint.Address + " request a file, but the server is not running");
            }
        }

        private void HandleUser(HttpListenerRequestEventArgs context, bool register) {
            if (context.Request.HttpMethod.ToLower() != "post") {
                HandleMethodNotAllowed(context);
            } else {
                try {
                    UserRequest user = (UserRequest)JsonConvert.DeserializeObject(new StreamReader(context.Request.InputStream).ReadToEnd(), typeof(UserRequest));
                    if (String.IsNullOrEmpty(user.Username) || String.IsNullOrEmpty(user.Password)) {
                        HandleWrongJson(context);
                        return;
                    }
                    var databaseUser = Users.GetUsers().Values.FirstOrDefault(x => x.UserName.ToLower() == user.Username.ToLower());
                    if (databaseUser != null) {
                        if (register) {
                            HandleError(context, 401, "Username is already in use.");
                        } else if(databaseUser.Password != Helper.HashString(user.Password)) {
                            HandleError(context, 401, "Wrong username or password.");
                        } else {
                            //Successful login reqeust
                            var device = databaseUser.AddDevice(context.Request.RemoteEndpoint.Address.ToString());
                            Users.SetUser(databaseUser.Id, databaseUser);
                            HandleReturn(context, device.Token);
                        }
                    } else {
                        if (register) {
                            //Successful register request
                            var token = Users.CreateUser(user.Username, user.Password, context.Request.RemoteEndpoint.Address.ToString());
                            HandleReturn(context, token);
                        } else {
                            HandleError(context, 401, "Wrong username or password.");
                        }
                    }
                } catch (JsonException e) {
                    HandleWrongJson(context);
                }
            }
        }

        private void HandleServerInfo(HttpListenerRequestEventArgs context) {
            string response = "{ " +
                "\"Name\":\"TVS_Server\",\n" +
                "\"GitHub\":\"https://github.com/Kaharonus/TVS_Server\",\n" +
                "\"Discription\":\"TVS_Server is a server for a client called TVSPlayer. It's use is to manage library of TV series/shows. Written in .Net Core with Avalonia UI. \",\n" +
                "\"ServerTime\":\"" + DateTime.UtcNow.ToString("o") + "\",\n" +
                "\"Version\":\"Develop\"\n" +
                "}";
            HandleReturn(context, response);
        }

        private bool IsAuthorized(HttpListenerRequestEventArgs context, out User user) {
            var request = context.Request;
            user = new User();
            if (request.Headers.Keys.Contains("AuthToken") && request.Headers["AuthToken"].Length == 128) {
                var id = Int16.Parse(request.Headers["AuthToken"].Substring(request.Headers["AuthToken"].Length - 4), System.Globalization.NumberStyles.HexNumber);
                var us = Users.GetUser(id);
                if (us.Devices.Where(x => x.Token == request.Headers["AuthToken"]).Count() > 0) {
                    user = us;
                    return true;
                }
            }
            return false;
        }

        private async Task HandleReturn(HttpListenerRequestEventArgs context, Stream input) {
            context.Response.OutputStream.Position = 0;
            await input.CopyToAsync(context.Response.OutputStream);
            context.Response.StatusCode = 200;
            input.Close();
            context.Response.Close();
        }

        private void HandleReturn(HttpListenerRequestEventArgs context,string input) {
            context.Response.OutputStream.Position = 0;
            StreamWriter sr = new StreamWriter(context.Response.OutputStream);
            sr.Write(input);
            sr.Flush();
            context.Response.StatusCode = 200;
            context.Response.Close();
        }

        private void HandleNotFound(HttpListenerRequestEventArgs context) {
            context.Response.NotFound();
            context.Response.Close();
        }

        private void HandleWrongJson(HttpListenerRequestEventArgs context) {
            context.Response.ReasonPhrase = "Wrong input format. Ex.: { \"username\":\"\",\"password\":\"\"}";
            context.Response.StatusCode = 401;
            context.Response.Close();
        }
        private void HandleMethodNotAllowed(HttpListenerRequestEventArgs context) {
            context.Response.MethodNotAllowed();
            context.Response.Close();
        }

        private void HandleError(HttpListenerRequestEventArgs context, int statuscode, string phrase) {
            context.Response.ReasonPhrase = phrase;
            context.Response.StatusCode = statuscode;
            context.Response.Close();
        }

        private void HandleInternalError(HttpListenerRequestEventArgs context) {
            context.Response.InternalServerError();
            try {
                context.Response.Close();
            } catch { }
        }

        class UserRequest {
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}
