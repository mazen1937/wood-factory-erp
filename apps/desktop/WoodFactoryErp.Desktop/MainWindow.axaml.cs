using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
