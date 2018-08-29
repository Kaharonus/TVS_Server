using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TVS_Server {
    public class Poster {
        public int Id { get; set; }
        public string KeyType { get; set; }
        public string SubKey { get; set; }
        [PrivateData]
        public string FileName { get; set; }
        public string Resolution { get; set; }
        public RatingsInfo ratingsInfo { get; set; }
        public string Thumbnail { get; set; }
        public class RatingsInfo {
            public double Average { get; set; }
            public int Count { get; set; }
        }

        public string URL { get; set; }

        /// <summary>
        /// Request information about posters
        /// </summary>
        /// <param name="id">TVDb id of Series</param>
        /// <returns>List of Poster objects or null when error occurs</returns>
        public static async Task<List<Poster>> GetPosters(int id) {
            return await Task.Run(() => {
                HttpWebRequest request = TVDbBase.GetRequest("https://api.thetvdb.com/series/" + id + "/images/query?keyType=poster");
                try {
                    var response = request.GetResponse();
                    using (var sr = new StreamReader(response.GetResponseStream())) {
                        List<Poster> list = new List<Poster>();
                        JObject jObject = JObject.Parse(sr.ReadToEnd());
                        foreach (JToken jt in jObject["data"]) {
                            list.Add(jt.ToObject<Poster>());
                        }
                        return list.OrderByDescending(y => y.ratingsInfo.Count).ToList();
                    }
                } catch (WebException e) {
                    return new List<Poster>();
                }
            });
           
        }

        /// <summary>
        /// Request information about posters in a season
        /// </summary>
        /// <param name="id">TVDb id of Series</param>
        /// <param name="season">Any season that the Series has</param>
        /// <returns>List of Poster objects or null when error occurs</returns>
        public static async Task<List<Poster>> GetPostersForSeason(int id, int season) {
            return await Task.Run(() => {
                HttpWebRequest request = TVDbBase.GetRequest("https://api.thetvdb.com/series/" + id + "/images/query?keyType=season&subKey=" + season);
                try {
                    var response = request.GetResponse();
                    using (var sr = new StreamReader(response.GetResponseStream())) {
                        List<Poster> list = new List<Poster>();
                        JObject jObject = JObject.Parse(sr.ReadToEnd());
                        foreach (JToken jt in jObject["data"]) {
                            list.Add(jt.ToObject<Poster>());
                        }
                        return list.OrderByDescending(y => y.ratingsInfo.Count).ToList();
                    }
                } catch (WebException e) {
                    return new List<Poster>();
                }
            });
        }

        /// <summary>
        /// Request information about default fanart
        /// </summary>
        /// <param name="id">TVDb id of Series</param>
        /// <returns></returns>
        public static async Task<Poster> GetFanArt(int id) {
            return await Task.Run(() => {
                HttpWebRequest request = TVDbBase.GetRequest("https://api.thetvdb.com/series/" + id + "/images/query?keyType=fanart");
                try {
                    var response = request.GetResponse();
                    using (var sr = new StreamReader(response.GetResponseStream())) {
                        List<Poster> list = new List<Poster>();
                        JObject jObject = JObject.Parse(sr.ReadToEnd());
                        foreach (JToken jt in jObject["data"]) {
                            list.Add(jt.ToObject<Poster>());
                        }
                        return SelectFanArt(list);
                    }
                } catch (WebException e) {
                    return new Poster();
                }
            });
        }

        private static Poster SelectFanArt(List<Poster> posters) {
            Dictionary<Poster, double> weighted = new Dictionary<Poster, double>();
            if (posters.Count > 0) {
                double minimum = posters.Select(x => x.ratingsInfo.Count).ToList().Average();
                double totalAverage = posters.Select(x => x.ratingsInfo.Average).ToList().Average();
                foreach (var poster in posters) {
                    int votes = poster.ratingsInfo.Count;
                    weighted.Add(poster, (votes / (votes + minimum)) * poster.ratingsInfo.Average / (minimum / (votes + minimum)) * totalAverage);
                }
                var max = weighted.Max(x => x.Value);
                return weighted.FirstOrDefault(x => x.Value == max).Key;
            } else {
                return new Poster();
            }
        }
    }
}
