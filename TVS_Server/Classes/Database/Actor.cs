using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TVS_Server
{
    class Actor{
        public int Id { get; set; }
        public int? SeriesId { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public int? SortOrder { get; set; }
        public string Image { get; set; }
        public string ImageAdded { get; set; }

        /// <summary>
        /// Requests all information about actors in TV Show from TVDb API
        /// </summary>
        /// <param name="id">TVDb id of Series</param>
        /// <returns>List of Actor objects with information or null when error occurs</returns>
        public static async Task<List<Actor>> GetActors(int id) {
            return await Task.Run(() => {
                HttpWebRequest request = TVDbBase.GetRequest("https://api.thetvdb.com/series/" + id + "/actors");
                try {
                    var response = request.GetResponse();
                    using (var sr = new StreamReader(response.GetResponseStream())) {
                        List<Actor> list = new List<Actor>();
                        JObject jObject = JObject.Parse(sr.ReadToEnd());
                        foreach (JToken jt in jObject["data"]) {
                            list.Add(jt.ToObject<Actor>());
                        }
                        return list.OrderBy(x => x.SortOrder).ToList<Actor>();
                    }
                } catch (WebException e) {
                    return null;
                }
            });
          
        }

    }
}

