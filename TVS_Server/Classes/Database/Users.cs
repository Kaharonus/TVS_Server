using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TVS_Server
{
    class Users {
        private static Dictionary<short, User> Data = new Dictionary<short, User>();

        public static string CreateUser(string userName, string password, string ipAddress) {
            User u = new User(userName, password);
            var device = u.AddDevice(ipAddress);
            u.LastLoginIP = ipAddress;
            SetUser(u.Id, u);
            return device.Token;
        }

        public static void SetUser(short id, User user) {
            if (Data.ContainsKey(id)) {
                Data[id] = user;
            } else {
                Data.Add(user.Id, user);
            }
            SaveUsers();
        }

        public static Dictionary<short, User> GetUsers() {
            return Data;
        }

        public static User GetUser(string userName) {
            var res = Data.Values.Where(x => x.UserName == userName).FirstOrDefault();
            return res ?? new User();
        }

        public static User GetUser(short id) {
            return Data.ContainsKey(id) ? Data[id] : new User();
        }

        public static void RemoveUser(short id) {
            Data.Remove(id);
            SaveUsers();
        }

        public static async Task SaveUsers() => await Task.Run(async () => {
            string file = Database.DatabasePath + "Users.TVSData";
            string json = JsonConvert.SerializeObject(Data);
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
        
        public static async Task LoadUsers() => await Task.Run(async () => {
            var users = Database.ReadFile(Database.DatabasePath + "Users.TVSData");
            if (users != new JObject()) {
                Data = (Dictionary<short, User>)users.ToObject(typeof(Dictionary<short, User>));
            }
        });

    }


    class User {
        public short Id { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public DateTime LastLogin { get; set; }
        public string LastLoginIP { get; set; }
        public List<UserDevice> Devices { get; set; } = new List<UserDevice>();
        public Dictionary<int, int> SelectedPoster { get; set; } = new Dictionary<int, int>();

        [JsonConstructor]
        public User() { }

        public User(string userName, string password) {        
            UserName = userName;
            SetPassword(password);
            Id = GenerateId();
            LastLogin = DateTime.Now;
        }

        public short GenerateId() {
            var rnd = RandomNumberGenerator.Create();
            short id = 0;
            do {
                byte[] data = new byte[4];
                rnd.GetBytes(data);
                id = Math.Abs(BitConverter.ToInt16(data, 0));
            } while (Users.GetUsers().Keys.Contains(id));
            return id;      
        }

        public void SetPassword(string password) {
            using (SHA512 sha = SHA512.Create()) {
                var hashedBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                var hash = BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
                Password = hash;
            }
        }

        public UserDevice AddDevice(string ipAddress) {
            var mac = Helper.GetMacAddress(ipAddress);
            var existingDev = Devices.Where(x => x.MacAddress == mac).FirstOrDefault();
            if (existingDev == null) {
                var dev = UserDevice.Create(this, ipAddress, mac);
                Devices.Add(dev);
                
                return dev;
            } else {
                return existingDev;
            }

        }

        public UserDevice GetDevice(string macAddress) {
            return Devices.Where(x => x.MacAddress == macAddress).FirstOrDefault();
        }


        public class UserDevice {
            public string MacAddress { get; set; }
            public string Token { get; set; }

            public static UserDevice Create(User user, string ip) {
                return new UserDevice {
                    Token = GenerateToken(user.Id),
                    MacAddress = Helper.GetMacAddress(ip)
                };
            }

            public static UserDevice Create(User user, string ip, string mac) {
                return new UserDevice {
                    Token = GenerateToken(user.Id),
                    MacAddress = mac
                };
            }

            private static string GenerateToken(short id) {
                var rnd = RandomNumberGenerator.Create();
                string password = "";
                char[] randomChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@$?_-".ToCharArray();
                for (int i = 0; i < 124; i++) {
                    byte[] data = new byte[4];
                    rnd.GetBytes(data);
                    int generatedValue = Math.Abs(BitConverter.ToInt32(data, 0));
                    password += randomChars[generatedValue % randomChars.Length];
                }
                password += id.ToString("X4");
                return password;
            }
        }
    }
}
