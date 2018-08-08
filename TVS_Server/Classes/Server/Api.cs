using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Linq;

namespace TVS_Server {
    class Api {
        public static string GetResponse(string request, User user) {
            var filtered = FilterPrivateData(Database.GetSeries(121361));
            return "";
        }

        private static Dictionary<string,object> FilterPrivateData(object obj) {
            var result = new Dictionary<string, object>();
            var properties = obj.GetType().GetProperties();         
            var list = properties.Where(x => x.GetCustomAttributes().Count() == 0).ToList();     
            foreach (var property in list) {
                result.Add(property.Name,property.GetValue(obj));
            }
            return result;
        }
        class Series{

        }
        class Episodes {

        }
        class Posters {

        }
        class Actors {

        }
    }
}
