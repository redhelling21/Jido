using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Jido.UI.Components.Common;

public class FieldLabel : TemplatedControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<FieldLabel, string>(nameof(Text));

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<FieldLabel, string?>(nameof(Hint));

    // Right by default: labels sit at the end of their row or are short section
    // headings, so the space to their right is empty and the tooltip covers nothing.
    // Override per call site wherever a control sits immediately to the right.
    public static readonly StyledProperty<PlacementMode> HintPlacementProperty =
        AvaloniaProperty.Register<FieldLabel, PlacementMode>(
            nameof(HintPlacement), PlacementMode.Right);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    // Offsets are derived from HintPlacement rather than set per call site, so a label
    // only ever has to declare which side it opens on and the gap follows automatically.
    public static readonly StyledProperty<double> HintHorizontalOffsetProperty =
        AvaloniaProperty.Register<FieldLabel, double>(nameof(HintHorizontalOffset));

    public static readonly StyledProperty<double> HintVerticalOffsetProperty =
        AvaloniaProperty.Register<FieldLabel, double>(nameof(HintVerticalOffset));

    private const double HintGap = 8;

    public FieldLabel()
    {
        ApplyHintGap();
    }

    public PlacementMode HintPlacement
    {
        get => GetValue(HintPlacementProperty);
        set => SetValue(HintPlacementProperty, value);
    }

    public double HintHorizontalOffset
    {
        get => GetValue(HintHorizontalOffsetProperty);
        set => SetValue(HintHorizontalOffsetProperty, value);
    }

    public double HintVerticalOffset
    {
        get => GetValue(HintVerticalOffsetProperty);
        set => SetValue(HintVerticalOffsetProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HintPlacementProperty)
            ApplyHintGap();
    }

    // Push the tooltip away from the icon along whichever axis it opens on, so it
    // never sits flush against the glyph.
    private void ApplyHintGap()
    {
        var (x, y) = HintPlacement switch
        {
            PlacementMode.Left => (-HintGap, 0d),
            PlacementMode.Right => (HintGap, 0d),
            PlacementMode.Top => (0d, -HintGap),
            PlacementMode.Bottom => (0d, HintGap),
            _ => (0d, 0d),
        };

        SetCurrentValue(HintHorizontalOffsetProperty, x);
        SetCurrentValue(HintVerticalOffsetProperty, y);
    }
}
