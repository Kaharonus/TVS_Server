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
            if (Settings.DatabaseUpdateTime == default(DateTime)) Settings.DatabaseUpdateTime = DateTime.Now;
            await Database.LoadDatabase();
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
            //var ids = File.ReadAllLines(@"C:\Users\tomas\Desktop\test.txt");
            //await Database.RemoveDatabase(121361);
            //await Database.CreateDatabase(121361);
        }


    }
}
