using System.Globalization;
using System.Windows.Data;

namespace HomeWorkJudge.UI.Converters;

/// <summary>Trả về true nếu value == parameter. Dùng cho RadioButton IsChecked binding.</summary>
public class EqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (bool)value ? parameter : Binding.DoNothing;
}

/// <summary>Submission status string → color brush.</summary>
public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? "";
        return status switch
        {
            "Pending"   => System.Windows.Application.Current.FindResource("StatusPendingBrush"),
            "Grading"   => System.Windows.Application.Current.FindResource("StatusGradingBrush"),
            "AIGraded"  => System.Windows.Application.Current.FindResource("StatusAIGradedBrush"),
            "Reviewed"  => System.Windows.Application.Current.FindResource("StatusReviewedBrush"),
            "Error"     => System.Windows.Application.Current.FindResource("StatusErrorBrush"),
            _ => System.Windows.Application.Current.FindResource("StatusPendingBrush")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Collapsed khi value là null hoặc empty string.</summary>
public class NullableToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is null || (value is string s && string.IsNullOrEmpty(s))
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
