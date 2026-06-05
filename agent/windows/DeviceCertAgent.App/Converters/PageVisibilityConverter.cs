using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DeviceCertAgent.App.ViewModels;
using DeviceCertAgent.Core.Models;

namespace DeviceCertAgent.App.Converters;

public sealed class PageVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AppPage page && parameter is string target)
            return Enum.Parse<AppPage>(target) == page ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StepStatusIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ScanStepStatus status)
            return status switch
            {
                ScanStepStatus.Completed => "✓",
                ScanStepStatus.InProgress => "…",
                ScanStepStatus.Warning => "!",
                ScanStepStatus.Failed => "✕",
                _ => "○",
            };
        return "○";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StepStatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is ScanStepStatus status
            ? status switch
            {
                ScanStepStatus.Completed => "SuccessBrush",
                ScanStepStatus.Warning => "WarningBrush",
                ScanStepStatus.Failed => "ErrorBrush",
                ScanStepStatus.InProgress => "AccentBrush",
                _ => "TextSecondaryBrush",
            }
            : "TextSecondaryBrush";

        return System.Windows.Application.Current?.FindResource(key)
            ?? System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
