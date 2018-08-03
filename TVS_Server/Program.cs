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
            if (Settings.DatabaseUpdateTime == default) Settings.DatabaseUpdateTime = DateTime.Now;
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
            Settings.ScanLocation1 = @"D:\TVSTests\ImportFolder\";
            Renamer.RunRenamer();

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
