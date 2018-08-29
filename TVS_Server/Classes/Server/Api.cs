using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using NReco.VideoConverter;

namespace TVS_Server {
    class Api {

        /// <summary>
        /// Filters properties with PrivateData attribute from object and returns it as dictioanry
        /// </summary>
        private static Dictionary<string, object> FilterPrivateData(object obj) {
            var result = new Dictionary<string, object>();
            var properties = obj.GetType().GetProperties();
            var list = properties.Where(x => x.GetCustomAttributes().Count() == 0).ToList();
            foreach (var property in list) {
                result.Add(property.Name, property.GetValue(obj));
            }

            return result;
        }

        /// <summary>
        /// Loads public static properties of any type into List of Tupples.
        /// </summary>
        private static List<(string name, MethodInfo method, Dictionary<string, Type> args)> LoadMethods(Type type) {
            var result = new List<(string name, MethodInfo method, Dictionary<string, Type> args)>();
            foreach (var item in type.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                (string name, MethodInfo method, Dictionary<string, Type> args) method = (item.Name.ToLower(), item,
                    new Dictionary<string, Type>());
                foreach (var par in item.GetParameters()) {
                    method.args.Add(par.Name.ToLower(), par.ParameterType);
                }

                result.Add(method);
            }

            return result;
        }

        public class Get {

            public static List<(string name, MethodInfo method, Dictionary<string, Type> args)> Methods =
                LoadMethods(typeof(GetOptions));

            public static string Response(Uri request, User user) {
                if (request.Segments.Count() == 3) {
                    var requestedMethod = request.Segments[2].ToLower();
                    Dictionary<string, object> paramQuery = new Dictionary<string, object>();
                    if (request.Query.Contains("?")) {
                        foreach (var item in request.Query.Replace("?", "").Split("&")) {
                            var parts = item.Split("=");
                            paramQuery.Add(parts[0].ToLower(), parts[1]);
                        }
                    }

                    var methods = Methods.Where(x => x.name == requestedMethod).ToList();
                    foreach (var method in methods) {
                        var args = method.args.WithoutLast().ToDictionary(x => x.Key, x => x.Value);
                        if (args.Keys.All(x => paramQuery.Keys.Contains(x)) &&
                            args.Keys.Count == paramQuery.Keys.Count && args.All(x =>
                                x.Value == typeof(int)
                                    ? Int32.TryParse((string) paramQuery[x.Key], out int r)
                                    : true)) {
                            paramQuery.Add("user", user);
                            List<object> obj = new List<object>();
                            foreach (var item in method.args) {
                                obj.Add(item.Value == typeof(int)
                                    ? Int32.Parse((string) paramQuery[item.Key.ToLower()])
                                    : paramQuery[item.Key.ToLower()]);
                            }

                            var result = method.method.Invoke(null, obj.ToArray());
                            if (result != null) {
                                return JsonConvert.SerializeObject(result);
                            }
                        }
                    }

                }

                return null;
            }

            public class GetOptions {

                #region Series

                public static object GetSeries(User user) {
                    List<object> result = new List<object>();
                    foreach (var series in Database.GetSeries()) {
                        result.Add(EditSeries(series, user));
                    }

                    return result;
                }

                public static object GetSeries(int seriesId, User user) =>
                    EditSeries(Database.GetSeries(seriesId), user);

                private static Dictionary<string, object> EditSeries(Series series, User user) {
                    if (series.Id != 0) {
                        if (user.SelectedPoster.ContainsKey(series.Id)) {
                            series.URL = "/image/poster/" + user.SelectedPoster[series.Id];
                        }
                        else {
                            user.SelectedPoster.Add(series.Id, Database.GetDefaultPosterId(series.Id));
                            Users.SetUser(user.Id, user);
                            series.URL = "/image/poster/" + user.SelectedPoster[series.Id];
                        }

                        return FilterPrivateData(series);
                    }
                    else {
                        return null;
                    }
                }

                public static object SearchSeries(string query, User user) {
                    List<object> result = new List<object>();
                    foreach (var series in Series.Search(query).GetAwaiter().GetResult()) {
                        result.Add(FilterPrivateData(series));
                    }

                    return result;
                }

                #endregion

                #region Episodes

                public static object GetEpisodes(int seriesId, User user) {
                    List<object> result = new List<object>();
                    foreach (var episode in Database.GetEpisodes(seriesId)) {
                        result.Add(EditEpisode(episode, seriesId, user));
                    }

                    return result;
                }

                public static object GetEpisode(int seriesId, int episodeId, User user) =>
                    EditEpisode(Database.GetEpisode(seriesId, episodeId, true), seriesId, user);

                private static Dictionary<string, object> EditEpisode(Episode episode, int seriesId, User user) {
                    if (episode.Id != 0) {
                        episode.URL = "/image/episode/" + seriesId + "/" + episode.Id;
                        return FilterPrivateData(episode);
                    }
                    else {
                        return null;
                    }
                }

                #endregion

                #region Actor

                public static object GetActors(int seriesId, User user) {
                    List<object> result = new List<object>();
                    foreach (var actor in Database.GetActors(seriesId)) {
                        result.Add(EditActor(actor, user));
                    }

                    return result;
                }

                public static object GetActor(int actorId, User user) => EditActor(Database.GetActor(actorId), user);

                private static Dictionary<string, object> EditActor(Actor actor, User user) {
                    if (actor.Id != 0) {
                        actor.URL = "/image/actor/" + actor.Id;
                        return FilterPrivateData(actor);
                    }
                    else {
                        return null;
                    }
                }

                #endregion

                #region Posters

                public static object GetPosters(int seriesId, User user) {
                    List<object> result = new List<object>();
                    foreach (var poster in Database.GetPosters(seriesId)) {
                        result.Add(EditPoster(poster, user));
                    }

                    return result;
                }

                public static object GetPoster(int posterId, User user) =>
                    EditPoster(Database.GetPoster(posterId), user);

                private static Dictionary<string, object> EditPoster(Poster poster, User user) {
                    if (poster.Id != 0) {
                        poster.URL = "/image/poster/" + poster.Id;
                        return FilterPrivateData(poster);
                    }
                    else {
                        return null;
                    }
                }

                #endregion

                #region Files

                public static object GetFiles(int episodeId, User user) {
                    List<object> result = new List<object>();
                    foreach (var file in Database.GetFiles(episodeId)) {
                        result.Add(EditFile(file, user));
                    }

                    return result;
                }

                private static Dictionary<string, object> EditFile(DatabaseFile file, User user) {
                    if (file.Id != 0) {
                        var timestamp = file.UserData.FirstOrDefault(x => x.userId == user.Id);
                        if (timestamp != default) {
                            file.TimeStamp = timestamp.ToString();
                        }
                        var fileName = file.NewName;
                        string hash = Helper.HashString16(fileName);
                        if (!FileServer.FileDictionary.ContainsKey(hash)) {
                            FileServer.FileDictionary.Add(hash, fileName);
                        }

                        file.URL = "http://" + Servers.FileServer.IP + ":" + Servers.FileServer.Port + "/" + hash;
                        return FilterPrivateData(file);
                    }
                    else {
                        return null;
                    }
                }

                #endregion

                #region Background 

                public static object GetBackground(int seriesId, User user) => EditBackground(Database.GetBackground(seriesId),seriesId, user);

                private static Dictionary<string, object> EditBackground(Poster poster, int seriesId, User user) {
                    if (poster.Id != 0) {
                        poster.URL = "/image/background/" + seriesId;
                        return FilterPrivateData(poster);
                    } else {
                        return null;
                    }
                }

                #endregion

                public static object Search(string query, User user) {
                    return Database.SearchDatabase(query.Replace("+", " "));
                }
            }

        }

        public class Post { }

        public class Files {

            public static DatabaseFile GetFile(Uri request) {
                List<string> segments = request.Segments.Select(item => item.Replace("/", ""))
                    .Where(temp => temp.Length > 0).ToList();
                if (segments.Count == 3 && int.TryParse(segments[1], out int episodeId) &&
                    int.TryParse(segments[2], out int id)) {
                    return Database.GetFile(episodeId, id);
                }

                return default;
            }

            public static async Task<string> ReturnSubitile(DatabaseFile file) {
                return File.Exists(file.NewName) ? await File.ReadAllTextAsync(file.NewName) : "";
            }

        }

        public class Images {

            private static readonly string DataPath = Database.DatabasePath + "\\Data\\Images\\";

            public static async Task<Stream> GetStream(Uri request) {
                List<string> segments = request.Segments.Select(item => item.Replace("/", ""))
                    .Where(temp => temp.Length > 0).ToList();
                if (segments.Count != 3 && segments.Count != 4) return Stream.Null;
                switch (segments[1]) {
                    case "poster":
                        if (int.TryParse(segments[2], out int posterId)) {
                            var poster = Database.GetPoster(posterId);
                            if (poster != default) {
                                return await LoadPosterImage(poster);
                            }
                        }
                        break;
                    case "actor":
                        if (int.TryParse(segments[2], out int actorId)) {
                            var poster = Database.GetActor(actorId);
                            if (poster != default) {
                                return await LoadActorImage(poster);
                            }
                        }
                        break;
                    case "episode":
                        if (int.TryParse(segments[2], out int seriesId) && int.TryParse(segments[3], out int episodeId)) {
                            var poster = Database.GetEpisode(seriesId, episodeId, true);
                            if (poster != default) {
                                return await LoadEpisodeImage(poster);
                            }
                        }
                        break;
                    case "background":
                        if (int.TryParse(segments[2], out int sId)) {
                            var poster = Database.GetBackground(sId);
                            if (poster != default) {
                                return await LoadPosterImage(poster);
                            }
                        }
                        break;
                    default:
                        return Stream.Null;
                }
                return Stream.Null;
            }

            private static async Task<Stream> LoadPosterImage(Poster poster) {
                var fileName = DataPath + "Posters\\" + poster.Id + ".png";
                if (File.Exists(fileName)) {
                    return File.OpenRead(fileName);
                }
                var stream = await GetStreamResponse(poster.FileName);
                await WriteToFile(stream, fileName);
                return stream;
            }

            private static async Task<Stream> LoadActorImage(Actor actor) {
                var fileName = DataPath + "Actors\\" +  actor.Id + ".png";
                if (File.Exists(fileName)) {
                    return File.OpenRead(fileName);
                }
                var stream = await GetStreamResponse(actor.Image);
                await WriteToFile(stream, fileName);
                return stream;
            }
            private static async Task<Stream> LoadEpisodeImage(Episode episode) {
                var fileName = DataPath + "Episodes\\" + episode.Id + ".png";
                if (File.Exists(fileName)) {

                    return File.OpenRead(fileName);
                }
                if (!string.IsNullOrEmpty(episode.Filename)) {
                    var stream = await GetStreamResponse(episode.Filename);
                    await WriteToFile(stream, fileName);
                    return stream;
                }
                var result = Database.GetFiles(episode.Id).FirstOrDefault(x => x.FileType == "Video");
                if (result != default) {
                    MemoryStream ms = new MemoryStream();
                    var ffmpeg = new FFMpegConverter();
                    ffmpeg.GetVideoThumbnail(fileName,ms);
                    await WriteToFile(ms,fileName);
                    return ms;
                }
                return Stream.Null;
            }

            private static async Task WriteToFile(Stream stream, string fileName) {
                if (!Directory.Exists(Path.GetDirectoryName(fileName))) {
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                }
                using (var fileStream = File.OpenWrite(fileName)) {
                    await stream.CopyToAsync(fileStream);
                    stream.Position = 0;

                }
            }

            private static async Task<Stream> GetStreamResponse(string url) {
                WebRequest request = WebRequest.CreateHttp("https://www.thetvdb.com/banners/" + url);
                using (var response = await request.GetResponseAsync()) {
                    Stream memoryStream = new MemoryStream();
                    await (response.GetResponseStream()).CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    return memoryStream;
                }

            }
        }
    }
}
