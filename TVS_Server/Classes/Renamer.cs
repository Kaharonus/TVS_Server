using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace TVS_Server{
    class Renamer{
        public static async Task RunRenamer() {
            await Task.Run(() => {
                //Search and filter files from library and other scan dirs
                var libFiles = GetFilesInLibrary();
                var scanFiles = GetFilesInScanDirectories();
                //Merge Dictionaries
                foreach (var item in scanFiles) {
                    if (libFiles.ContainsKey(item.Key)) {
                        libFiles[item.Key].UnionWith(item.Value);
                    } else {
                        libFiles.Add(item.Key, item.Value);
                    }
                }
                //Filter files that are already in library and select episode for the rest
                libFiles = FilterExisting(libFiles);
                libFiles = FillEpisodeData(libFiles);
                //Generate right proper name for ep, rename, add to DB
                var files = Database.GetFiles();
                foreach (var series in libFiles) {
                    foreach (var file in series.Value) {
                        file.NewName = GenerateNewName(file);
                        Rename(file);
                        //Create id for file
                        if (files.ContainsKey(file.EpisodeId)) {
                            int id = 1;
                            var ids = files[file.EpisodeId].Select(x => x.Id);
                            while (ids.Contains(id)) {
                                id++;
                            }
                            file.Id = id;
                            file.URL = "/files/" + file.EpisodeId + "/" + file.Id;
                            files[file.EpisodeId].Add(file);
                        } else {
                            file.Id = 1;
                            file.URL = "/files/" + file.EpisodeId + "/" + file.Id;
                            files.Add(file.EpisodeId, new List<DatabaseFile> { file });
                        }
                    }
                }
                Database.SetFiles(files);
            });

        }

        private static string GenerateNewName(DatabaseFile file) {
            var series = Database.GetSeries(file.SeriesId);
            var episode = Database.GetEpisode(file.SeriesId, file.EpisodeId);
            string name = Helper.GenerateName(file.SeriesId,file.EpisodeId);
            string dir = episode.AiredSeason < 10 ? series.LibraryPath + @"\Season 0" + episode.AiredSeason +"\\" : series.LibraryPath + @"\Season " + episode.AiredSeason + "\\";
            Directory.CreateDirectory(dir);
            if (file.OldName == dir + name + file.Extension) {
                return file.OldName;
            }
            string old = Path.GetFileNameWithoutExtension(file.OldName);
            Match match = new Regex(name + "_[0-9]?[0-9]").Match(old);
            if (match.Success) {
                return file.OldName;
            }
            int filenumber = 1;
            string result = dir + name + file.Extension;
            while (File.Exists(result)) {
                result = dir + name + "_" + filenumber + file.Extension;
                filenumber++;
            }
            return result;
        }

        private static void Rename(DatabaseFile file) {
            if (file.NewName != file.OldName) {
                try {
                    File.Move(file.OldName, file.NewName);
                } catch (IOException) {
                    Log.Write("File: " + file.OldName + " is probably in use. There is no option right now to try again since there is no UI for it. Try again later");
                }
            }
        }

        private static Dictionary<int, HashSet<DatabaseFile>> FilterExisting(Dictionary<int, HashSet<DatabaseFile>> data) {
            Dictionary<int, HashSet<DatabaseFile>> result = new Dictionary<int, HashSet<DatabaseFile>>();
            var files = Database.GetFiles().SelectMany(x=>x.Value);
            foreach (var file in data) {
                result.Add(file.Key, new HashSet<DatabaseFile>());
                foreach (var item in file.Value) {
                    if(files.Where(x=>x.NewName == item.OldName).Count() == 0) {
                        result[file.Key].Add(item);
                    }
                }
            }
            return result;
        }
            private static Dictionary<int, HashSet<DatabaseFile>> FillEpisodeData(Dictionary<int, HashSet<DatabaseFile>> data) {
            foreach (var item in data) {
                foreach (var file in item.Value) {
                    if (GetEpisodeId(file, out int epId)) {
                        file.EpisodeId = epId;
                    }
                }
            }
            return data;
        }

        private static bool GetEpisodeId(DatabaseFile file, out int episodeId) {
            Match season = new Regex("[s][0-9][0-9]", RegexOptions.IgnoreCase).Match(file.OldName);
            Match episode = new Regex("[e][0-9][0-9]", RegexOptions.IgnoreCase).Match(file.OldName);
            Match special = new Regex("[0-9][0-9][x][0-9][0-9]", RegexOptions.IgnoreCase).Match(file.OldName);
            (int season, int episode) result = (-1, -1);
            if (season.Success && episode.Success) {
                result = (Int32.Parse(season.Value.Remove(0, 1)), Int32.Parse(episode.Value.Remove(0, 1)));
            } else if (special.Success) {
                result = (Int32.Parse(special.Value.Substring(0, 2)), Int32.Parse(special.Value.Substring(3, 2)));
            }
            if (result != (-1, -1)) {
                var ep = Database.GetEpisodes(file.SeriesId).Where(x=>x.AiredEpisodeNumber == result.episode && x.AiredSeason == result.season).FirstOrDefault();
                if (ep != null) {
                    episodeId = ep.Id;
                    return true;
                }
            }
            episodeId = -1;
            return false;
        }


        private static Dictionary<int, HashSet<DatabaseFile>> GetFilesInLibrary() {
            Dictionary<int, HashSet<DatabaseFile>> result = new Dictionary<int, HashSet<DatabaseFile>>();
            var allSeries = Database.GetSeries();
            var allFiles = Directory.GetFiles(Settings.LibraryLocation, "*.*", SearchOption.AllDirectories).ToHashSet();
            foreach (var series in allSeries) {
                series.LibraryPath = series.LibraryPath.Replace("\\\\", "\\");
                if (Directory.Exists(series.LibraryPath)) {
                    HashSet<DatabaseFile> files = new HashSet<DatabaseFile>();
                    var tempFiles = allFiles.Where(x => x.Contains(series.LibraryPath)).ToList();
                    foreach (var file in tempFiles) {
                        allFiles.Remove(file);
                        DatabaseFile databaseFile = new DatabaseFile();
                        databaseFile.OldName = file;
                        databaseFile.SeriesId = series.Id;
                        files.Add(databaseFile);
                    }
                    if (files.Count() > 0) {
                        result.Add(series.Id, FilterExtensions(files));
                    }
                }
            }
            foreach (var key in result.Keys.ToList()) {
                result[key] = CheckAliases(result[key], Database.GetSeries(key));
            }
            return result;
        }

        private static Dictionary<int, HashSet<DatabaseFile>> GetFilesInScanDirectories() {
            Dictionary<int, HashSet<DatabaseFile>> result = new Dictionary<int, HashSet<DatabaseFile>>();
            //Load all files into List
            List<string> tempFiles = new List<string>();
            if (Directory.Exists(Settings.ScanLocation1)) tempFiles.AddRange(Directory.GetFiles(Settings.ScanLocation1, "*", SearchOption.AllDirectories));
            if (Directory.Exists(Settings.ScanLocation2)) tempFiles.AddRange(Directory.GetFiles(Settings.ScanLocation2, "*", SearchOption.AllDirectories));
            if (Directory.Exists(Settings.ScanLocation3)) tempFiles.AddRange(Directory.GetFiles(Settings.ScanLocation3, "*", SearchOption.AllDirectories));
            HashSet<DatabaseFile> files = new HashSet<DatabaseFile>();
            //Switch strings with path to DatabaseFile objects
            tempFiles.ForEach(x => files.Add(new DatabaseFile() { OldName = x }));
            files = FilterExtensions(files);
            //Filter series
            foreach (var series in Database.GetSeries()) {
                foreach (var file in files) {
                    if (CheckAliases(file, series)) {
                        file.SeriesId = series.Id;
                        if (result.ContainsKey(series.Id)) {
                            result[series.Id].Add(file);
                        } else {
                            result.Add(series.Id, new HashSet<DatabaseFile>() { file });
                        }
                    }
                }
            }
            return result;
        }

        private static HashSet<DatabaseFile> CheckAliases(HashSet<DatabaseFile> files, Series series) {
            HashSet<DatabaseFile> result = new HashSet<DatabaseFile>();
            foreach (var file in files) {
                if (CheckAliases(file, series)) {
                    result.Add(file);
                }
            }
            return result;
        }

        private static bool CheckAliases(DatabaseFile file, Series series) {
            foreach (string alias in series.aliases) {
                string temp = alias.ToUpper();
                if (
                    Path.GetFileName(file.OldName.ToUpper()).StartsWith(temp) ||
                    Path.GetFileName(Path.GetDirectoryName(file.OldName.ToUpper())).StartsWith(temp) && ContainsEpisodeInfo(file.OldName)
                    ) {
                    return true;
                }
            }
            return false;
        }

        public static bool ContainsEpisodeInfo(string file) {
            Match season = new Regex("[s][0-9][0-9]", RegexOptions.IgnoreCase).Match(file);
            Match episode = new Regex("[e][0-9][0-9]", RegexOptions.IgnoreCase).Match(file);
            Match special = new Regex("[0-5][0-9][x][0-5][0-9]", RegexOptions.IgnoreCase).Match(file);
            if ((season.Success && episode.Success && (episode.Index - season.Index) < 5) || special.Success) {
                return true;
            }
            return false;
        }

        private static HashSet<DatabaseFile> FilterExtensions(HashSet<DatabaseFile> files) {
            string[] subExtension = new string[] { ".srt", ".sub" };
            string[] videoExtensions = new string[] { ".mkv", ".avi", ".mp4", ".m4v", ".mov", ".wmv", ".flv" };
            HashSet<DatabaseFile> filtered = new HashSet<DatabaseFile>();
            foreach (var file in files) {
                var ext = Path.GetExtension(file.OldName);
                if (videoExtensions.Any(x => x == ext)) {
                    file.FileType = DatabaseFile.Filetype.Video;
                    file.Extension = ext;
                    filtered.Add(file);
                } else if (subExtension.Any(x => x == ext)) {
                    file.FileType = DatabaseFile.Filetype.Subtitle;
                    file.Extension = ext;
                    file.SubtitleLanguage = "Unknown";
                    filtered.Add(file);
                }
            }
            return filtered;
        }

    }
}
