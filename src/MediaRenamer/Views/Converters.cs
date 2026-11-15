using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using MediaRenamer.ViewModels;

namespace MediaRenamer.Views;

/// <summary>Converts image data (e.g. the poster) into a bitmap; invalid data results in no image.</summary>
public sealed class BytesToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] data)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(data);
            return new Bitmap(stream);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException
                                      or IOException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converters from <see cref="NewNameState"/> to style classes.</summary>
public static class StateConverters
{
    public static readonly IValueConverter IsNotFound =
        new FuncValueConverter<NewNameState, bool>(state => state == NewNameState.NotFound);

    public static readonly IValueConverter IsCaseOnly =
        new FuncValueConverter<NewNameState, bool>(state => state == NewNameState.CaseOnly);

    public static readonly IValueConverter IsChanged =
        new FuncValueConverter<NewNameState, bool>(state => state == NewNameState.Changed);
}
