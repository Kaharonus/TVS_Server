using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Diagnostics;

namespace TVS_Server {
    class Api {

        private static Dictionary<string, object> FilterPrivateData(object obj) {
            var result = new Dictionary<string, object>();
            var properties = obj.GetType().GetProperties();         
            var list = properties.Where(x => x.GetCustomAttributes().Count() == 0).ToList();     
            foreach (var property in list) {
                result.Add(property.Name,property.GetValue(obj));
            }
            return result;
        }

        public class Get {

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
                    var methods = typeof(GetOptions).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name.ToLower() == requestedMethod).ToList();
                    foreach (var method in methods) {
                        var par = method.GetParameters().WithoutLast().ToDictionary(x => x.Name.ToLower(), x => x);
                        if (par.Keys.All(x => paramQuery.Keys.Contains(x)) && par.Keys.Count == paramQuery.Keys.Count && par.All(x => x.Value.ParameterType == typeof(int) ? Int32.TryParse((string)paramQuery[x.Key], out int r) : true)) {
                            paramQuery.Add("user", user);
                            par = method.GetParameters().ToDictionary(x => x.Name, x => x);
                            List<object> obj = new List<object>();
                            foreach (var item in par) {
                                obj.Add(item.Value.ParameterType == typeof(int) ? Int32.Parse((string)paramQuery[item.Key.ToLower()]) : paramQuery[item.Key.ToLower()]);
                            }
                            var result = method.Invoke(null, obj.ToArray());
                            if (result != null) {
                                return JsonConvert.SerializeObject(result);
                            }
                        }
                    }
                    
                }
                return null;
            }
            public class GetOptions {
                public static object GetSeries(User user) {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    List<object> result = new List<object>();
                    foreach (var series in Database.GetSeries()) {
                        result.Add(EditSeries(series, user));
                    }
                    sw.Stop();
                    return result;
                }

                public static object GetSeries(int seriesId, User user) => EditSeries(Database.GetSeries(seriesId), user);

                public static object SearchSeries(string query, User user) {
                    List<object> result = new List<object>();
                    foreach (var series in Series.Search(query).GetAwaiter().GetResult()) {
                        result.Add(FilterPrivateData(series));
                    }
                    return result;
                }

                private static Dictionary<string, object> EditSeries(Series series, User user) {
                    if (series.Id != 0) {
                        if (user.SelectedPoster.ContainsKey(series.Id)) {
                            series.URL = "/data/image/" + user.SelectedPoster[series.Id];
                        } else {
                            user.SelectedPoster.Add(series.Id, Database.GetDefaultPosterId(series.Id));
                            Users.SetUser(user.Id, user);
                            series.URL = "/data/image/" + user.SelectedPoster[series.Id];
                        }
                        return FilterPrivateData(series);
                    } else {
                        return null;
                    }
                }

                public static object Search(string text, User user) {
                    return new object();
                }
            }
            
        }
        

        class Post {

        }

    }
}
