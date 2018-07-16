using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TVS_Server
{
    class Users {
        private static Dictionary<short, User> Data = new Dictionary<short, User>();

        public static void CreateUser(string userName, string password) {

        }

        public static void SetUser(short id, User user) {

        }

        public static User GetUser(string userName) {
            return null;
        }

        public static User GetUser(short id) {
            return null;
        }

        public static void RemoveUser(short id) {

        }

        public static async Task SaveUsers() {

        }

        public static async Task LoadUsers() {

        }
    }
    class User {
        public short Id { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public DateTime LastLogin { get; set; }
        public string LastLoginIP { get; set; }
        public List<UserDevice> Devices { get; set; } = new List<UserDevice>();

        [JsonConstructor]
        public User() { }

        public User(string userName, string password) {
            
            UserName = userName;
            SetPassword(password);
        }

        public void SetPassword(string password) {
            using (SHA512 sha = SHA512.Create()) {
                var hashedBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                var hash = BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
                Password = hash;
            }
        }

        public void AddDevice(string ipAddress) {
            Devices.Add(UserDevice.Create(this, ipAddress));
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

            private static string GenerateToken(short id) {
                var rnd = RandomNumberGenerator.Create();
                string password = "";
                char[] randomChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@$?_-".ToCharArray();
                for (int i = 0; i < 60; i++) {
                    byte[] data = new byte[4];
                    rnd.GetBytes(data);
                    int generatedValue = Math.Abs(BitConverter.ToInt32(data, 0));
                    password += randomChars[generatedValue % randomChars.Length];
                }
                password += Convert.ToBase64String(BitConverter.GetBytes(id));
                return password;
            }
        }
    }
}
