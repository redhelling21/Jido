using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Jido.UI.Components.Common.Sidebar;

namespace Jido.UI.Components.Common;

public class PageWithHeader : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<PageWithHeader, string>(
        nameof(Title)
    );

    public static readonly StyledProperty<string> SubtitleProperty = AvaloniaProperty.Register<PageWithHeader, string>(
        nameof(Subtitle)
    );

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }
}
