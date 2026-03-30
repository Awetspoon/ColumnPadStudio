using ColumnPadStudio.App.Styling;
using ColumnPadStudio.Domain.Enums;

namespace ColumnPadStudio.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeManager.ApplyTheme(ThemePreset.Default);
    }
}
