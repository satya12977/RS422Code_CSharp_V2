using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ANVESHA_TCRX_HEALTH_STATUS_GUI_V2.Converters
{
    // ── Bool → Connected/Disconnected text ──────────────────────────────────
    public class BoolToConnectionTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            return (value is bool && (bool)value) ? "CONNECTED" : "DISCONNECTED";
        }
        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        { return null; }
    }

    // ── Bool → Green/Red brush ───────────────────────────────────────────────
    public class BoolToStatusBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            return (value is bool && (bool)value)
                ? new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88))
                : new SolidColorBrush(Color.FromRgb(0xE9, 0x45, 0x60));
        }
        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        { return null; }
    }

    // ── Bool → Connect/Disconnect button text ───────────────────────────────
    public class BoolToButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            return (value is bool && (bool)value) ? "DISCONNECT" : "CONNECT";
        }
        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        { return null; }
    }

    // ── Bool → Inverted bool ─────────────────────────────────────────────────
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            return !(value is bool && (bool)value);
        }
        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        { return null; }

    }

   
}