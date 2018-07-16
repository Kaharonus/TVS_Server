using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using Avalonia;
using Avalonia.Logging.Serilog;

namespace TVS_Server
{
    class Program
    {
        public static bool GUIEnabeled { get; set; } = false;
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToDebug();

        static void Main(string[] args) {
            ParseParameters(args);
            LoadApplication();
            StartApplication();
            Console.ReadLine();
        }

        private static void ParseParameters(string[] args) {

        }

        private static void LoadApplication() {
            if (Settings.DatabaseUpdateTime == default(DateTime)) Settings.DatabaseUpdateTime = DateTime.Now;
        }

        private static void StartApplication() {
            if (GUIEnabeled) {
                BuildAvaloniaApp().Start<MainWindow>();
            } else {
                TestMethod();
            }
        }

        private static async void TestMethod() {
            Settings.LoadSettings();
            Log.Write(DateTime.Now.ToShortDateString()+ ", " + DateTime.Now.ToLongTimeString()+", " + Helper.GetMyIP());
            await Database.LoadDatabase();
            //await Database.RemoveDatabase(121361);
            //await Database.CreateDatabase(121361);
        }


    }
}
