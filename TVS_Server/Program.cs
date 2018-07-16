using System;
using System.Diagnostics;
using System.Threading.Tasks;
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
            await Database.LoadDatabase();
            await Database.CreateDatabase(121361);
            await Task.Delay(0);
        }


    }
}
