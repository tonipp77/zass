using System.Reflection;
using System.Windows;
using Zass.App.Resources;

namespace Zass.App.Views;

/// <summary>
/// The "About" dialog (RF-18): app name, version, and a short description. The
/// version is read from the assembly so it tracks the build automatically.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        Title = Strings.AboutTitle;
        DescriptionText.Text = Strings.AboutDescription;
        CloseButton.Content = Strings.AboutClose;
        VersionText.Text = $"{Strings.AboutVersionLabel} {ResolveVersion()}";
    }

    private static string ResolveVersion()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip the build metadata suffix (e.g. "+<commit hash>") if present.
            int plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        Version version = assembly.GetName().Version ?? new Version(1, 0, 0, 0);
        return version.ToString(3);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
