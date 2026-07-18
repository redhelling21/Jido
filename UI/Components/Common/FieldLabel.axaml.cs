using Avalonia;
using Avalonia.Controls.Primitives;

namespace Jido.UI.Components.Common;

public class FieldLabel : TemplatedControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<FieldLabel, string>(nameof(Text));

    public static readonly StyledProperty<string?> TooltipProperty =
        AvaloniaProperty.Register<FieldLabel, string?>(nameof(Tooltip));

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Tooltip
    {
        get => GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }
}
