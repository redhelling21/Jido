using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Jido.Utils;
using SharpHook.Data;

namespace Jido.UI.Converters
{
    public class KeyCodeConverter : IValueConverter
    {
        public static readonly KeyCodeConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is KeyCombo combo)
                return combo.ToString();

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
                if (targetType == typeof(KeyCode))
                {
                    var trimmed = input.Trim();
                    if (Enum.TryParse<KeyCode>(trimmed, ignoreCase: true, out var direct))
                        return direct;
                    var candidate = "Vc" + char.ToUpper(trimmed[0]) + trimmed[1..];
                    if (Enum.TryParse<KeyCode>(candidate, ignoreCase: true, out var prefixed))
                        return prefixed;
                    return KeyCode.VcUndefined;
                }
                return KeyCombo.Parse(input);
            }
            return targetType == typeof(KeyCode) ? KeyCode.VcUndefined : new KeyCombo(KeyCode.VcUndefined);
        }
    }
}
