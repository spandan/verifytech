using System.Windows;
using System.Windows.Input;
using DeviceCertAgent.App.ViewModels;

namespace DeviceCertAgent.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is ShellViewModel shell)
            shell.FunctionalTests.HandleKeyPress(e.Key);
    }
}
