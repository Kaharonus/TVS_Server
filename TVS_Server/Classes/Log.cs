using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Text;

namespace TVS_Server{
    class Log {
        public static void Write(string text, ISolidColorBrush fontColor = null) {
            if (Program.GUIEnabeled) {

            }
            Console.WriteLine(text);
        }
    }
}
