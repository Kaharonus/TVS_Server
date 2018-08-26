using System;
using System.Collections.Generic;
using System.Text;

namespace TVS_Server
{
    class DatabaseSearchResult {
        public string Name { get; set; }
        public string Type { get; set; }
        public int SeriesId { get; set; }
        public int EpisodeId { get; set; }
    }
}
