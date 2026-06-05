using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DeviceCertAgent.App.ViewModels;

namespace DeviceCertAgent.App.Converters;

public sealed class FunctionalStepVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FunctionalTestStep step && parameter is string target
            && Enum.TryParse<FunctionalTestStep>(target, out var expected))
            return step == expected ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
