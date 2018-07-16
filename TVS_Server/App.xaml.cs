using Avalonia;
using Avalonia.Markup.Xaml;

namespace TVS_Server
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
