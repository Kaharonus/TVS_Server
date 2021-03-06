﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Timers;
using System.Diagnostics;

namespace TVS_Server
{
    class Database {

        #region static variables
        public static string DatabasePath = GetPath();
        public enum SaveType {
            Series, Episodes, Actors, Posters, Files, Background, All
        }
        #endregion

        private static Dictionary<int, Database> Data { get; set; } = new Dictionary<int, Database>();
        private static Dictionary<int, List<DatabaseFile>> Files = new Dictionary<int, List<DatabaseFile>>();

        public Series Series { get; set; }
        public Dictionary<int, Episode> Episodes { get; set; } = new Dictionary<int, Episode>();
        public Dictionary<int, Actor> Actors { get; set; } = new Dictionary<int, Actor>();
        public Dictionary<int, Poster> Posters { get; set; } = new Dictionary<int, Poster>();
        public Poster Background { get; set; } = new Poster();


        #region Get lists

        public static List<Series> GetSeries() => Data.Values.Select(x => x.Series).ToList();

        public static List<Episode> GetEpisodes(int seriesId) => Data.ContainsKey(seriesId) ? Data[seriesId].Episodes.Values.ToList() : new List<Episode>();

        public static List<Actor> GetActors(int seriesId) => Data.ContainsKey(seriesId) ? Data[seriesId].Actors.Values.ToList() : new List<Actor>();

        public static List<Poster> GetPosters(int seriesId) => Data.ContainsKey(seriesId) ? Data[seriesId].Posters.Values.ToList() : new List<Poster>();

        public static List<DatabaseFile> GetFiles(int episodeId) => Files.ContainsKey(episodeId) ? Files[episodeId] : new List<DatabaseFile>();

        public static Dictionary<int, List<DatabaseFile>> GetFiles() => Files;

        #endregion

        #region Get items

        public static Series GetSeries(int seriesId) {
            return Data.ContainsKey(seriesId) ? Data[seriesId].Series : new Series();
        }

        public static Episode GetEpisode(int seriesId,int episodeId, bool fullInfo = false) {
            if (fullInfo) {
                FillData(seriesId,episodeId).Wait();
            }
            return Data.ContainsKey(seriesId) && Data[seriesId].Episodes.ContainsKey(episodeId) ? Data[seriesId].Episodes[episodeId] : new Episode();
        }

        public static Actor GetActor(int actorId) {
            return Data.SelectMany(x => x.Value.Actors).FirstOrDefault(x => x.Value.Id == actorId).Value ?? new Actor();
        }

        public static Poster GetPoster(int posterId) {
            return Data.SelectMany(x => x.Value.Posters).FirstOrDefault(x => x.Value.Id == posterId).Value ?? new Poster();
        }

        public static Poster GetBackground(int seriesId) {
            return Data.ContainsKey(seriesId) ? Data[seriesId].Background : new Poster();
        }

        /*
        * weighted rating (WR) = (v ÷ (v+m)) × R + (m ÷ (v+m)) × C , where:
        * R = average for the poster (mean) = (Rating)
        * v = number of votes for the poster = (votes)
        * m = minimum votes required - average of all votes
        * C = the mean vote across the whole report
        */
        public static int GetDefaultPosterId(int seriesId) {
            Dictionary<Poster, double> weighted = new Dictionary<Poster, double>();
            var posters = GetPosters(seriesId);
            if (posters.Count > 0) {
                double minimum = posters.Select(x => x.ratingsInfo.Count).ToList().Average();
                double totalAverage = posters.Select(x => x.ratingsInfo.Average).ToList().Average();
                foreach (var poster in posters) {
                    int votes = poster.ratingsInfo.Count;
                    weighted.Add(poster, (votes / (votes + minimum)) * poster.ratingsInfo.Average / (minimum / (votes + minimum)) * totalAverage);
                }
                var max = weighted.Max(x => x.Value);
                return weighted.FirstOrDefault(x => x.Value == max).Key.Id;
            } else {
                return 0;
            }
        }

        public static DatabaseFile GetFile(int episodeId, int fileId) {
            if (Files.ContainsKey(episodeId)) {
                var item = Files[episodeId].Where(x => x.Id == fileId).FirstOrDefault();
                if (item != null) {
                    return item;
                }
            }
            return new DatabaseFile();
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

        public static void SetFiles(Dictionary<int,List<DatabaseFile>> files) {
            Files = files.ToDictionary(x=>x.Key, x=>x.Value.ToList());
            SaveDatabase(0, SaveType.Files);
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

        public static void SetFile(int episodeId, int fileId, DatabaseFile file) {
            if (Files.ContainsKey(episodeId)) {
                var item = Files[episodeId].Where(x => x.Id == fileId).FirstOrDefault();
                if (item != null) {
                    Files[episodeId][Files[episodeId].IndexOf(item)] = file;
                } else {
                    Files[episodeId].Add(file);
                }
               
                SaveDatabase(0, SaveType.Files);
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

        public static void RemoveFile(int episodeId, int fileId) {
            if (Files.ContainsKey(episodeId)) {
                var item = Files[episodeId].Where(x => x.Id == fileId).FirstOrDefault();
                if (item != null) {
                    Files[episodeId].Remove(item);
                }
                SaveDatabase(0, SaveType.Files);
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
                    var background = ReadFile(path + id + "\\Background.TVSData");
                    if (series != new JObject() && episodes != new JObject() && actors != new JObject() && posters != new JObject()) {
                        Database database = new Database {
                            Series = series.ToObject<Series>(),
                            Background = background.ToObject<Poster>(),
                            Episodes = episodes.ToObject<Dictionary<int, Episode>>(),
                            Actors = actors.ToObject<Dictionary<int, Actor>>(),
                            Posters = posters.ToObject<Dictionary<int, Poster>>()
                        };
                        if (!Data.ContainsKey(id)) {
                            Data.Add(id, database);
                        }
                    }
                }
            }
            var files = ReadFile(DatabasePath + "\\Files.TVSData");
            Files = (Dictionary<int, List<DatabaseFile>>)files.ToObject(typeof(Dictionary<int, List<DatabaseFile>>));
            StartBackgroundUpdate();
        });

        /// <summary>
        /// Used for saving individual or all files of a TV Show
        /// </summary>
        /// <param name="seriesId">TVDb id of a TV show to be saved</param>
        /// <param name="type">Kind of file to be saved</param>
        /// <returns></returns>
        public static async Task SaveDatabase(int seriesId, SaveType type) => await Task.Run(async () => {

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
                case SaveType.Files:
                    obj = Files;
                    file = DatabasePath + "Files.TVSData";
                    break;
                case SaveType.Background:
                    obj = Data[seriesId].Background;
                    file += "Background.TVSData";
                    break;
                case SaveType.All:
                    await SaveDatabase(seriesId, SaveType.Series);
                    await SaveDatabase(seriesId, SaveType.Episodes);
                    await SaveDatabase(seriesId, SaveType.Actors);
                    await SaveDatabase(seriesId, SaveType.Posters);
                    await SaveDatabase(seriesId, SaveType.Background);
                    await SaveDatabase(0, SaveType.Files);
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
        });

        /// <summary>
        /// Adds new TV Show to database
        /// </summary>
        /// <param name="seriesId">TVDb ID of said TV Show</param>
        /// <returns></returns>
        public static async Task CreateDatabase(int seriesId, string databasePath = null) => await Task.Run(async () => {
            if (!Data.ContainsKey(seriesId)) {
                List<Task> tasks = new List<Task>() {
                    Series.GetSeries(seriesId),
                    Episode.GetEpisodes(seriesId),
                    Actor.GetActors(seriesId),
                    Poster.GetPosters(seriesId),
                    Poster.GetFanArt(seriesId)
                };
                await Task.WhenAll(tasks);
                Database db = new Database {
                    Series = ((Task<Series>)tasks[0]).Result,
                    Episodes = ((Task<List<Episode>>)tasks[1]).Result.ToDictionary(x => x.Id, x => x),
                    Actors = ((Task<List<Actor>>)tasks[2]).Result.ToDictionary(x => x.Id, x => x),
                    Posters = ((Task<List<Poster>>)tasks[3]).Result.ToDictionary(x => x.Id, x => x),
                    Background = ((Task<Poster>)tasks[4]).Result

                };
                if (!String.IsNullOrEmpty(databasePath)) {
                    db.Series.LibraryPath = databasePath;
                } else {
                    db.Series.LibraryPath = Settings.LibraryLocation + "\\" + db.Series.SeriesName;
                }
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

        public static List<DatabaseSearchResult> SearchDatabase(string text) {
            text = text.Replace("%20", " ");
            List<DatabaseSearchResult> results = new List<DatabaseSearchResult>();
            foreach (var item in Data) {
                var eps = item.Value.Episodes.Where(x => Helper.ParseDate(x.Value.FirstAired) <= DateTime.Now && x.Value.EpisodeName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0 && x.Value.AiredSeason > 0).ToList();
                eps = eps.OrderByDescending(x => x.Value.SiteRatingCount).ToList();
                eps.ForEach(x => results.Add(new DatabaseSearchResult { Name = x.Value.EpisodeName, EpisodeId = x.Key, SeriesId = item.Key, Type = "Episode" }));
                if (item.Value.Series.SeriesName.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) >= 0) {
                    results.Add(new DatabaseSearchResult { Name = item.Value.Series.SeriesName, SeriesId = item.Key, Type = "Series" });
                }
            }
            return results;
        }

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
        public static JObject ReadFile(string file) {
            if (File.Exists(file) || File.Exists(file + "Backup")) {
                try {
                    string json = File.ReadAllText(file);
                    var jobject = JObject.Parse(json);
                    return jobject;
                } catch (Exception e) {
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
                ids = ids.Where(x => Data.Keys.Contains(x)).ToList();
                var action = new BackgroundAction("Updating database", ids.Count);
                foreach (var seriesId in ids) {
                    action.Name = "Updating database - " + Data[seriesId].Series.SeriesName;
                    List<Task> tasks = new List<Task>() {
                        Series.GetSeries(seriesId),
                        Episode.GetEpisodes(seriesId),
                        Actor.GetActors(seriesId),
                        Poster.GetPosters(seriesId)
                    };
                    await Task.WhenAll(tasks);
                    UpdateSeries(seriesId, ((Task<Series>)tasks[0]).Result);
                    UpdateEpisodes(seriesId, ((Task<List<Episode>>)tasks[1]).Result);
                    UpdateActors(seriesId, ((Task<List<Actor>>)tasks[2]).Result);
                    UpdatePosters(seriesId, ((Task<List<Poster>>)tasks[3]).Result);
                    action.Value++;
                }
                Settings.DatabaseUpdateTime = DateTime.Now;
            }
            await CheckAllSeries();
            await Renamer.RunRenamer();
        }

        private static void UpdateSeries(int seriesId, Series newData) {
            if (Data.ContainsKey(seriesId)) {
                newData.UpdateObject(Data[seriesId].Series);
                Data[seriesId].Series = newData;
             }
        }

        private static void UpdateEpisodes(int seriesId, List<Episode> newData) {
            if (Data.ContainsKey(seriesId)) {
                for (int i = 0; i < newData.Count; i++) {
                    if (Data.ContainsKey(newData[i].Id)) {
                        newData[i].UpdateObject(Data[seriesId].Episodes[newData[i].Id]);
                    }
                }
                SetEpisodes(seriesId, newData);
            }
        }

        private static void UpdateActors(int seriesId, List<Actor> newData) {
            if (Data.ContainsKey(seriesId)) {
                for (int i = 0; i < newData.Count; i++) {
                    if (Data.ContainsKey(newData[i].Id)) {
                        newData[i].UpdateObject(Data[seriesId].Actors[newData[i].Id]);
                    }
                }
                SetActors(seriesId, newData);
            }
        }

        private static void UpdatePosters(int seriesId, List<Poster> newData) {
            if (Data.ContainsKey(seriesId)) {
                for (int i = 0; i < newData.Count; i++) {
                    if (Data.ContainsKey(newData[i].Id)) {
                        newData[i].UpdateObject(Data[seriesId].Posters[newData[i].Id]);
                    }
                }
                SetPosters(seriesId, newData);
            }
        }

        #endregion

        #region Episode data fill-in

        private static List<(int seriesId, Episode ep)> fillEpisodes = new List<(int, Episode)>();

        private static async Task CheckAllSeries() {
            while (fillEpisodes.Count > 0) {
                await Task.Delay(10_000);
            }
            foreach (var series in GetSeries()) {
                Data[series.Id].Episodes.Values.Where(x => !x.FullInfo).ToList().ForEach(x => fillEpisodes.Add((series.Id, x)));             
            }
            if (fillEpisodes.Count > 0) {
                BackgroundAction action = new BackgroundAction("Filling episode data", fillEpisodes.Count);
                foreach (var item in fillEpisodes) {
                    action.Name = "Filling episode data - " + item.ep.EpisodeName;
                    await FillData(item.seriesId, item.ep.Id);
                    action.Value++;
                }
                fillEpisodes.Clear();
            }

        }

        private static async Task FillData(int seriesId, int episodeId) {
            if (Data.ContainsKey(seriesId) && Data[seriesId].Episodes.ContainsKey(episodeId) && !Data[seriesId].Episodes[episodeId].FullInfo) {
                var ep = await Episode.GetEpisode(episodeId);
                ep.UpdateObject(Data[seriesId].Episodes[episodeId]);
                ep.FullInfo = true;
                SetEpisode(seriesId, episodeId, ep);
            }
        }




        #endregion
        /*private static void SerachByName(string name) {
            name = name.ToLower();
            HashSet<Episode> test = new HashSet<Episode>();
            foreach (var item in Data) {
                test.Union(item.Value.Episodes.Values);
            }
            var ep = test.Where(x => x.EpisodeName.Contains(name)).ToList();
        }*/
    }
}
