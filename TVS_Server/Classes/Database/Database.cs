using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Timers;

namespace TVS_Server
{
    class Database {

        #region static variables
        public static string DatabasePath = GetPath();
        public enum SaveType {
            Series, Episodes, Actors, Posters, All
        }
        #endregion

        private static Dictionary<int, Database> Data { get; set; } = new Dictionary<int, Database>();

        public Series Series { get; set; }
        public Dictionary<int, Episode> Episodes { get; set; } = new Dictionary<int, Episode>();
        public Dictionary<int, Actor> Actors { get; set; } = new Dictionary<int, Actor>();
        public Dictionary<int, Poster> Posters { get; set; } = new Dictionary<int, Poster>();

        #region Get lists

        public static List<Series> GetSeries() => Data.Values.Select(x => x.Series).ToList();

        public static List<Episode> GetEpisodes(int seriesId) => Data.ContainsKey(seriesId) ? Data[seriesId].Episodes.Values.ToList() : new List<Episode>();

        public static List<Actor> GetActors(int seriesId) => Data.ContainsKey(seriesId) ? Data[seriesId].Actors.Values.ToList() : new List<Actor>();

        public static List<Poster> GetPosters(int seriesId) => Data.ContainsKey(seriesId) ? Data[seriesId].Posters.Values.ToList() : new List<Poster>();

        #endregion

        #region Get items

        public static Series GetSeries(int seriesId) {
            return Data.ContainsKey(seriesId) ? Data[seriesId].Series : new Series();
        }

        public static Episode GetEpisode(int seriesId, int episodeId) {
            return Data.ContainsKey(seriesId) && Data[seriesId].Episodes.ContainsKey(episodeId) ? Data[seriesId].Episodes[episodeId] : new Episode();
        }

        public static Actor GetActor(int seriesId, int actorId) {
            return Data.ContainsKey(seriesId) && Data[seriesId].Actors.ContainsKey(actorId) ? Data[seriesId].Actors[actorId] : new Actor();
        }

        public static Poster GetPoster(int seriesId, int posterId) {
            return Data.ContainsKey(seriesId) && Data[seriesId].Posters.ContainsKey(posterId) ? Data[seriesId].Posters[posterId] : new Poster();
        }

        #endregion

        #region Set lists

        public static void SetEpisodes(int seriesId, List<Episode> episodes) {
            if (Data.ContainsKey(seriesId)) {
                Data[seriesId].Episodes = episodes.ToDictionary(x => x.Id, x => x);
                SaveDatabase(seriesId, SaveType.Episodes);
            }
        }

        public static void SetActors(int seriesId, List<Actor> actors) {
            if (Data.ContainsKey(seriesId)) {
                Data[seriesId].Actors = actors.ToDictionary(x => x.Id, x => x);
                SaveDatabase(seriesId, SaveType.Actors);
            }
        }

        public static void SetPosters(int seriesId, List<Poster> posters) {
            if (Data.ContainsKey(seriesId)) {
                Data[seriesId].Posters = posters.ToDictionary(x => x.Id, x => x);
                SaveDatabase(seriesId, SaveType.Posters);
            }
        }

        #endregion

        #region Set items

        public static void SetSeries(int seriesId, Series series) {
            if (Data.ContainsKey(seriesId)) {
                Data[seriesId].Series = series;
                SaveDatabase(seriesId, SaveType.Series);
            }
        }

        public static void SetEpisode(int seriesId, int episodeId, Episode episode) {
            if (Data.ContainsKey(seriesId)) {
                if (Data[seriesId].Episodes.ContainsKey(episodeId)) {
                    Data[seriesId].Episodes[episodeId] = episode;
                } else {
                    Data[seriesId].Episodes.Add(episode.Id, episode);
                }
                SaveDatabase(seriesId, SaveType.Episodes);
            }
        }

        public static void SetActor(int seriesId, int actorId, Actor actor) {
            if (Data.ContainsKey(seriesId)) {
                if (Data[seriesId].Actors.ContainsKey(actorId)) {
                    Data[seriesId].Actors[actorId] = actor;
                } else {
                    Data[seriesId].Actors.Add(actor.Id, actor);
                }
                SaveDatabase(seriesId, SaveType.Actors);
            }
        }

        public static void SetPoster(int seriesId, int posterId, Poster poster) {
            if (Data.ContainsKey(seriesId)) {
                if (Data[seriesId].Posters.ContainsKey(posterId)) {
                    Data[seriesId].Posters[posterId] = poster;
                } else {
                    Data[seriesId].Posters.Add(poster.Id, poster);
                }
                SaveDatabase(seriesId, SaveType.Posters);
            }

        }

        #endregion

        #region Remove items

        public static void RemoveEpisode(int seriesId, int episodeId) {
            if (Data.ContainsKey(seriesId) && Data[seriesId].Episodes.ContainsKey(episodeId)) {
                Data[seriesId].Episodes.Remove(episodeId);
                SaveDatabase(seriesId, SaveType.Episodes);
            }
        }

        public static void RemoveActor(int seriesId, int actorId) {
            if (Data.ContainsKey(seriesId) && Data[seriesId].Actors.ContainsKey(actorId)) {
                Data[seriesId].Actors.Remove(actorId);
                SaveDatabase(seriesId, SaveType.Actors);
            }
        }

        public static void RemovePoster(int seriesId, int posterId) {
            if (Data.ContainsKey(seriesId) && Data[seriesId].Posters.ContainsKey(posterId)) {
                Data[seriesId].Posters.Remove(posterId);
                SaveDatabase(seriesId, SaveType.Posters);
            }
        }

        #endregion

        #region Database operations

        /// <summary>
        /// Used to load database, start things like DB Update 
        /// </summary>
        /// <returns></returns>
        public static async Task LoadDatabase() => await Task.Run(async () => {
            var ids = await LoadIDs();
            string path = DatabasePath + "Data\\";
            foreach (int id in ids.Keys) {
                if (Directory.Exists(path + id)) {
                    var series = ReadFile(path + id + "\\Series.TVSData");
                    var episodes = ReadFile(path + id + "\\Episodes.TVSData");
                    var actors = ReadFile(path + id + "\\Actors.TVSData");
                    var posters = ReadFile(path + id + "\\Posters.TVSData");
                    if (series != new JObject() && episodes != new JObject() && actors != new JObject() && posters != new JObject()) {
                        Database database = new Database();
                        database.Series = (Series)series.ToObject(typeof(Series));
                        database.Episodes = (Dictionary<int, Episode>)episodes.ToObject(typeof(Dictionary<int, Episode>));
                        database.Actors = (Dictionary<int, Actor>)actors.ToObject(typeof(Dictionary<int, Actor>));
                        database.Posters = (Dictionary<int, Poster>)posters.ToObject(typeof(Dictionary<int, Poster>));
                        if (!Data.ContainsKey(id)) {
                            Data.Add(id, database);
                        }
                    }
                }
            }
            StartBackgroundUpdate();
        });

        /// <summary>
        /// Used for saving individual or all files of a TV Show
        /// </summary>
        /// <param name="seriesId">TVDb id of a TV show to be saved</param>
        /// <param name="type">Kind of file to be saved</param>
        /// <returns></returns>
        public static async Task SaveDatabase(int seriesId, SaveType type) => await Task.Run(async () => {
            if (Data.ContainsKey(seriesId)) {
                object obj = new object();
                string file = DatabasePath + "Data\\" + seriesId + "\\";
                if (!Directory.Exists(file)) {
                    Directory.CreateDirectory(file);
                }
                switch (type) {
                    case SaveType.Series:
                        obj = Data[seriesId].Series;
                        file += "Series.TVSData";
                        break;
                    case SaveType.Episodes:
                        obj = Data[seriesId].Episodes;
                        file += "Episodes.TVSData";
                        break;
                    case SaveType.Actors:
                        obj = Data[seriesId].Actors;
                        file += "Actors.TVSData";
                        break;
                    case SaveType.Posters:
                        obj = Data[seriesId].Posters;
                        file += "Posters.TVSData";
                        break;
                    case SaveType.All:
                        await SaveDatabase(seriesId, SaveType.Series);
                        await SaveDatabase(seriesId, SaveType.Episodes);
                        await SaveDatabase(seriesId, SaveType.Actors);
                        await SaveDatabase(seriesId, SaveType.Posters);
                        return;
                }
                string json = JsonConvert.SerializeObject(obj);
                while (true) { 
                    try {
                        if (File.Exists(file)) {
                            if (File.Exists(file + "Backup")) {
                                File.Delete(file + "Backup");
                            }
                            File.Move(file, file + "Backup");
                        }
                        File.WriteAllText(file, json);
                        return;
                    } catch (IOException e) {
                        await Task.Delay(10);
                    }
                }
            }
        });

        /// <summary>
        /// Adds new TV Show to database
        /// </summary>
        /// <param name="seriesId">TVDb ID of said TV Show</param>
        /// <returns></returns>
        public static async Task CreateDatabase(int seriesId) => await Task.Run(async () => {
            if (!Data.ContainsKey(seriesId)) {
                List<Task> tasks = new List<Task>() {
                Series.GetSeries(seriesId),
                Episode.GetEpisodes(seriesId),
                Actor.GetActors(seriesId),
                Poster.GetPosters(seriesId)
            };
                await Task.WhenAll(tasks);
                Database db = new Database {
                    Series = ((Task<Series>)tasks[0]).Result,
                    Episodes = ((Task<List<Episode>>)tasks[1]).Result.ToDictionary(x => x.Id, x => x),
                    Actors = ((Task<List<Actor>>)tasks[2]).Result.ToDictionary(x => x.Id, x => x),
                    Posters = ((Task<List<Poster>>)tasks[3]).Result.ToDictionary(x => x.Id, x => x)
                };
                Data.Add(seriesId, db);
                await SaveDatabase(seriesId, SaveType.All);
                var ids = await LoadIDs();
                ids.Add(seriesId, db.Series.SeriesName);
                await SaveIDs(ids);
            }
        });

        /// <summary>
        /// Removes TV Show from database and deletes its data
        /// </summary>
        /// <param name="seriesId"></param>
        /// <returns></returns>
        public static async Task RemoveDatabase(int seriesId) => await Task.Run(async () => {
            if (Data.ContainsKey(seriesId)) {
                Data.Remove(seriesId);
                var ids = await LoadIDs();
                ids.Remove(seriesId);
                await SaveIDs(ids);
                Directory.Delete(DatabasePath + "Data\\" + seriesId, true);
            }
        });

        /// <summary>
        /// Loads IDs of all TV Shows in database.
        /// </summary>
        /// <returns></returns>
        private static async Task<Dictionary<int, string>> LoadIDs() {
            string file = DatabasePath + "Base.TVSData";
            var data = ReadFile(file);
            if (data != new JObject()) {
                return (Dictionary<int,string>)data.ToObject(typeof(Dictionary<int, string>));
            }
            return new Dictionary<int, string>();
        }

        /// <summary>
        /// Saves IDs of TV Shows and their names to a seperate file for better loading and faster orientation when debugging
        /// </summary>
        private static async Task SaveIDs(Dictionary<int, string> data) {
            string file = DatabasePath + "Base.TVSData";
            string json = JsonConvert.SerializeObject(data);
            while (true) {
                try {
                    if (File.Exists(file)) {
                        if (File.Exists(file + "Backup")) {
                            File.Delete(file + "Backup");
                        }
                        File.Move(file, file + "Backup");
                    }
                    File.WriteAllText(file, json);
                    return;
                } catch (IOException e) {
                    await Task.Delay(10);
                }
            }
        }

        #endregion

        #region Support methods

        /// <summary>
        /// Returns path to library on whatever platform
        /// TODO: Create path for OSX
        /// </summary>
        /// <returns></returns>
        private static string GetPath() {
            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + "\\TVS_Server\\";
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                path = "/etc/TVS_Server/";
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                Log.Write("OSX/Apple machines are not supported right now. Sorry.");
                Environment.Exit(0);
            }
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        /// <summary>
        /// Reads specified file, returns it as JObject for further parsing, takes care of backup file recovery
        /// </summary>
        private static JObject ReadFile(string file) {
            if (File.Exists(file)) {
                string json = File.ReadAllText(file);
                try {
                    var jobject = JObject.Parse(json);
                    return jobject;
                } catch (JsonReaderException e) {
                    if (File.Exists(file + "Backup")) {
                        File.Delete(file);
                        File.Move(file + "Backup", file);
                        return ReadFile(file);
                    } else {
                        throw e;
                    }
                }
            }
            return new JObject();
        }

        #endregion

        #region DatabaseUpdater

        private static void StartBackgroundUpdate() {
            CheckForUpdates();
            Timer timer = new Timer(1800000);
            timer.Elapsed += async (s, ev) => await CheckForUpdates();
            timer.Start();
        }

        private static async Task CheckForUpdates() {
            if (Settings.DatabaseUpdateTime.AddDays(1) < DateTime.Now) {
                var ids = await Series.GetUpdates(Settings.DatabaseUpdateTime);

            }
        }

        #endregion

    }
}
