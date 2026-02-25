using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using SharpHook.Native;

namespace Jido.UI.Converters
{
    public class KeyCodeConverter : IValueConverter
    {
        public static readonly KeyCodeConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is KeyCode keyCode)
            {
                var name = keyCode.ToString();
                // Strip "Vc" prefix, VcB -> B, VcF1 -> F1, VcSpace -> Space
                return name.StartsWith("Vc") ? name[2..] : name;
            }
            return value?.ToString();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string input)
            {
                var candidate = input.StartsWith("Vc", StringComparison.OrdinalIgnoreCase)
                    ? input
                    : "Vc" + char.ToUpper(input[0]) + input[1..]; // "b" -> "VcB"

                if (Enum.TryParse<KeyCode>(candidate, ignoreCase: true, out var keyCode))
                    return keyCode;
            }
            return KeyCode.VcUndefined;
        }
    }
}
