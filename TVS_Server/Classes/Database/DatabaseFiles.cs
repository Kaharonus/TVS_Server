using System;
using System.Collections.Generic;
using System.Text;

namespace TVS_Server
{

    class DatabaseFile {
        public int Id { get; set; }
        [PrivateData]
        public string OldName { get; set; }
        [PrivateData]
        public string NewName { get; set; }
        public string URL { get; set; }
        public string Extension { get; set; }
        public string FileType { get; set; }
        public string SubtitleLanguage { get; set; }
        public string TimeStamp { get; set; }
        public int Resolution { get; set; } = 0;
        public int SeriesId { get; set; }
        public int EpisodeId { get; set; }
        [PrivateData]
        public List<(int userId, double time)> UserData { get; set; } = new List<(int userId, double time)>();
    }
}
