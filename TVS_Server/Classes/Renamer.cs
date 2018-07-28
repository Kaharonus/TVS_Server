using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace TVS_Server{
    class Renamer{
        public static async void RunRenamer() {
            await Task.Run(() => {

            });
            var files = GetFilesInLibrary();
        }

        public static async Task RunRenamer(int seriesId) {
            await Task.Run(() => { });
            var files = GetFilesInLibrary().Where(x => x.Key == seriesId).FirstOrDefault();
        }

        private static Dictionary<int, HashSet<DatabaseFile>> GetFilesInLibrary() {
            Dictionary<int, HashSet<DatabaseFile>> result = new Dictionary<int, HashSet<DatabaseFile>>();
            var allSeries = Database.GetSeries();
            var allFiles = Directory.GetFiles(Settings.LibraryLocation, "*.*", SearchOption.AllDirectories).ToHashSet();
            foreach (var series in allSeries) {
                series.LibraryPath = series.LibraryPath.Replace("\\\\", "\\");
                if (Directory.Exists(series.LibraryPath)) {
                    HashSet<DatabaseFile> files = new HashSet<DatabaseFile>();
                    foreach (var file in allFiles.Where(x => x.Contains(series.LibraryPath))) {
                        allFiles.Remove(file);
                        DatabaseFile databaseFile = new DatabaseFile();
                        databaseFile.OldName = file;
                        databaseFile.SeriesId = series.Id;
                    }
                    if (files.Count() > 0) {
                        result.Add(series.Id, FilterExtensions(files));
                    }
                }
            }
            return result;
        }

        private static Dictionary<int, HashSet<DatabaseFile>> GetFilesInScanDirectories() {
            Dictionary<int, HashSet<DatabaseFile>> result = new Dictionary<int, HashSet<DatabaseFile>>();
            HashSet<string> files = new HashSet<string>();
            return result;
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


        /*
         
        private static bool CheckAliases(ScannedFileInfo file, Series series) {
            foreach (string alias in series.aliases) {
                if (Path.GetFileName(file.origFile.ToUpper()).StartsWith(alias.ToUpper())) {
                    return true;
                }
            }
            return false;
        }

        
        
         */
    }
}
