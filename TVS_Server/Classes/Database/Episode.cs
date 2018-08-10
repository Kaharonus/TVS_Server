using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TVS_Server {
    class Episode {
        public int Id { get; set; }
        public int? AiredSeason { get; set; }
        public int? AiredEpisodeNumber { get; set; }
        public string EpisodeName { get; set; }
        public string FirstAired { get; set; }
        public List<string> GuestStars { get; set; } = new List<string>();
        public string Director { get; set; }
        public List<string> Directors { get; set; } = new List<string>();
        public List<string> Writers { get; set; } = new List<string>();
        public string Overview { get; set; }
        public string ShowUrl { get; set; }
        public int? AbsoluteNumber { get; set; }
        [PrivateData]
        public string Filename { get; set; }
        public int? SeriesId { get; set; }
        public int? AirsAfterSeason { get; set; }
        public int? AirsBeforeSeason { get; set; }
        public int? AirsBeforeEpisode { get; set; }
        public string ImdbId { get; set; }
        public double SiteRating { get; set; }
        public int? SiteRatingCount { get; set; }

        [PrivateData]
        public bool FullInfo { get; set; } = false;
        public string URL { get; set; }
        /// <summary>
        /// Requests basic information about all episodes from TVDb API
        /// </summary>
        /// <param name="id">TVDb id of Series</param>
        /// <returns>List of Episodes with only basic information or null when error occurs</returns>
        public static async Task<List<Episode>> GetEpisodes(int id) {
            return await Task.Run(() => {
                List<Episode> list = new List<Episode>();
                int page = 1;
                while (true) {
                    try {
                        HttpWebRequest request = TVDbBase.GetRequest("https://api.thetvdb.com/series/" + id + "/episodes?page=" + page);
                        var response = request.GetResponse();
                        using (var sr = new StreamReader(response.GetResponseStream())) {
                            JObject jObject = JObject.Parse(sr.ReadToEnd());
                            foreach (JToken jt in jObject["data"]) {
                                list.Add(jt.ToObject<Episode>());
                            }
                            page++;
                        }
                    } catch (WebException ex) {
                        if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null) {
                            var resp = (HttpWebResponse)ex.Response;
                            if (resp.StatusCode == HttpStatusCode.NotFound) { return list; }
                        } else {
                            return new List<Episode>();
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Requests full information about Episode from TVDb API
        /// </summary>
        /// <param name="id">TVDb ID of Episode</param>
        /// <returns>Episode object or null when error occurs</returns>
        public static async Task<Episode> GetEpisode(int id) {
            return await Task.Run(() => {
                HttpWebRequest request = TVDbBase.GetRequest("https://api.thetvdb.com/episodes/" + id);
                try {
                    var response = request.GetResponse();
                    using (var sr = new StreamReader(response.GetResponseStream())) {
                        JObject jObject = JObject.Parse(sr.ReadToEnd());
                        return jObject["data"].ToObject<Episode>();
                    }
                } catch (WebException e) {
                    return new Episode();
                }
            });
        }
    }
}

