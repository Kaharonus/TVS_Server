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
                Listener.Request += (s, ev) => HandleRequest(ev);
            }
            Listener.Start();
            Log.Write("API Started @ " + IP + ":" + Port);
        }

        private async Task HandleRequest(HttpListenerRequestEventArgs ctx) {
            await Task.Run(async () => {
                var context = ctx;
                Log.Write(ctx.Request.HttpMethod + " - " + ctx.Request.RemoteEndpoint.ToString() + " - " + ctx.Request.Url.PathAndQuery);
                switch (context.Request.Url.Segments[1].Replace("/", "").ToLower()) {
                    case "api":
                        HandleApi(context);
                        break;
                    case "file":
                    case "image":
                        HandleData(context);
                        break;
                    case "register":
                        HandleUser(context, true);
                        break;
                    case "login":
                        HandleUser(context,false);
                        break;
                    default:
                        HandleNotFound(context);
                        break;
                }
            });            
        }

        private void HandleApi(HttpListenerRequestEventArgs context) {
            if (context.Request.HttpMethod.ToLower() == "get") {
                if (IsAuthorized(context, out User user)) {
                    Api.GetResponse(context.Request.Url.PathAndQuery, user);
                    context.Response.NotImplemented();
                    context.Response.Close();
                } else {
                    HandleError(context, 401, "Not authorized.");
                }
            } else {
                HandleMethodNotAllowed(context);
            }

        }

        private void HandleData(HttpListenerRequestEventArgs context) {

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
                    var databaseUser = Users.GetUsers().Values.Where(x => x.UserName.ToLower() == user.Username.ToLower()).FirstOrDefault();
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

        class UserRequest {
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}
