using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using Avalonia;
using Avalonia.Logging.Serilog;
using System.IO;

namespace TVS_Server
{
    class Program
    {
        public static bool GUIEnabeled { get; set; } = false;
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToDebug();

        static void Main(string[] args) {
            ParseParameters(args);
            LoadApplication().Wait();
            StartApplication();
            Console.ReadLine();
        }

        private static void ParseParameters(string[] args) {

        }

        private static async Task LoadApplication() {
            Settings.LoadSettings();
            if (Settings.DataServerPort == default) {
                Settings.DataServerPort = 5850;
                Settings.FileServerPort = 5851;
            }
            if (Settings.DatabaseUpdateTime == default) Settings.DatabaseUpdateTime = DateTime.Now;
            await Database.LoadDatabase();
            await Users.LoadUsers();
        }

        private static void StartApplication() {
            if (GUIEnabeled) {
                BuildAvaloniaApp().Start<MainWindow>();
            } else {
                TestMethod();
            }
        }


        private static async void TestMethod() {
            Log.Write(DateTime.Now.ToShortDateString()+ ", " + DateTime.Now.ToLongTimeString()+", " + Helper.GetMyIP());
            Servers.StartDataServer();
            Servers.StartFileServer();
            
            
            
            /*foreach (var item in File.ReadAllLines(@"D:\TVSTests\test.txt")) {
                await Database.CreateDatabase(Int32.Parse(item));
            }*/
            //Users.CreateUser("test", "test", Helper.GetMyIP());
            /*            while (true) {
                            var list = BackgroundAction.GetActions();
                            foreach (var action in list) {
                                Log.Write(action.Name + ", " + action.Value + "/" + action.MaxValue + ", " + action.TimeRemaining);
                            }
                            await Task.Delay(1000);
                        }*/

        }


    }
}
