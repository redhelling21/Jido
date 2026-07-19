using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Jido.Config;

namespace Jido.UI.Components.Pages.InventoryManagement
{
    public class InventoryOverlay : Window
    {
        private const int GridWidth = InventoryManagementConfig.GridWidth;
        private const int GridHeight = InventoryManagementConfig.GridHeight;
        public const int TitleBarHeight = 28;
        private const int ResizeHandleSize = 14;

        private readonly bool[][] _cells = new bool[GridWidth][];

        private readonly int _physicalGridWidth;
        private readonly int _physicalGridHeight;

        public InventoryOverlay(InventoryOverlayData data)
        {
            Title = "Inventory Layout";
            Background = new SolidColorBrush(Colors.Transparent);
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
            SystemDecorations = SystemDecorations.None;
            CanResize = true;
            Topmost = true;

            // Initialize cell state arrays
            for (int col = 0; col < GridWidth; col++)
            {
                _cells[col] =
                    data.InventorySlots != null && col < data.InventorySlots.Length && data.InventorySlots[col] != null
                        ? data.InventorySlots[col]
                        : new bool[GridHeight];
            }

            _physicalGridWidth = data.InventoryWidth;
            _physicalGridHeight = data.InventoryHeight;

            // Assume no scaling for now
            Width = _physicalGridWidth;
            Height = _physicalGridHeight + TitleBarHeight;
            Position = new PixelPoint(data.InventoryPosition[0], data.InventoryPosition[1] - TitleBarHeight);

            Content = BuildContent();
        }

        
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var scaling = DesktopScaling;
            if (scaling <= 0)
                return;
            // Avalonia Width/Height are logical (DIPs) while Position is physical
            // So we have to convert between the two
            Width = _physicalGridWidth / scaling;
            Height = (_physicalGridHeight / scaling) + TitleBarHeight;
            // Adapt the position after eventual resizing
            Position = new PixelPoint(Position.X, Position.Y + TitleBarHeight - (int)(TitleBarHeight * scaling));
        }

        public InventoryOverlayData GetInventoryConfig()
        {
            var scaling = DesktopScaling > 0 ? DesktopScaling : 1.0;
            return new InventoryOverlayData
            {
                InventoryWidth = (int)(ClientSize.Width * scaling),
                InventoryHeight = (int)((ClientSize.Height - TitleBarHeight) * scaling),
                InventorySlots = _cells,
                InventoryPosition = [Position.X, Position.Y + (int)(TitleBarHeight * scaling)],
            };
        }

        private Panel BuildContent()
        {
            var root = new Panel();

            // Title bar on top, cell grid below
            var mainGrid = new Grid { RowDefinitions = new RowDefinitions($"{TitleBarHeight},*"), };

            var titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(210, 20, 20, 50)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 70, 70, 150)),
                BorderThickness = new Thickness(1, 1, 1, 0),
                Cursor = new Cursor(StandardCursorType.SizeAll),
            };

            var titleContent = new Grid { ColumnDefinitions = new ColumnDefinitions("*, Auto") };

            var titleLabel = new TextBlock
            {
                Text = "Inventory Layout  ·  click cells to protect them",
                Foreground = new SolidColorBrush(Color.FromArgb(200, 220, 220, 255)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };
            Grid.SetColumn(titleLabel, 0);
            titleContent.Children.Add(titleLabel);

            var closeButton = new Button
            {
                Content = "✕",
                Width = TitleBarHeight,
                Height = TitleBarHeight,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromArgb(180, 220, 220, 255)),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Cursor = new Cursor(StandardCursorType.Arrow),
            };
            closeButton.Click += (_, _) => Close();
            Grid.SetColumn(closeButton, 1);
            titleContent.Children.Add(closeButton);

            titleBar.Child = titleContent;
            titleBar.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginMoveDrag(e);
            };

            Grid.SetRow(titleBar, 0);
            mainGrid.Children.Add(titleBar);

            // Cell grid
            var gridBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 70, 70, 150)),
                BorderThickness = new Thickness(1, 0, 1, 1),
            };

            var cellGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", GridWidth))),
                RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("*", GridHeight))),
            };
            // Create each cell
            for (int col = 0; col < GridWidth; col++)
            {
                for (int row = 0; row < GridHeight; row++)
                {
                    var cell = new Border
                    {
                        Background = _cells[col][row]
                            ? new SolidColorBrush(Color.FromArgb(160, 200, 50, 50))
                            : new SolidColorBrush(Colors.Transparent),
                        BorderBrush = new SolidColorBrush(Colors.White),
                        BorderThickness = new Thickness(1),
                    };

                    var c = col;
                    var r = row;
                    // React to the cell being clicked
                    cell.PointerPressed += (sender, e) => CellPointerPressed(sender, c, r);

                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, col);
                    cellGrid.Children.Add(cell);
                }
            }

            gridBorder.Child = cellGrid;
            Grid.SetRow(gridBorder, 1);
            mainGrid.Children.Add(gridBorder);

            root.Children.Add(mainGrid);

            // Resize handle (bottom-right corner)
            var resizeHandle = new Border
            {
                Width = ResizeHandleSize,
                Height = ResizeHandleSize,
                Background = new SolidColorBrush(Color.FromArgb(180, 100, 100, 210)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Cursor = new Cursor(StandardCursorType.BottomRightCorner),
            };
            resizeHandle.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    BeginResizeDrag(WindowEdge.SouthEast, e);
            };

            root.Children.Add(resizeHandle);

            return root;
        }

        private void CellPointerPressed(object? sender, int col, int row)
        {
            if (sender is not Border cell)
                return;
            // Toggle this inventory slot protection
            _cells[col][row] = !_cells[col][row];
            cell.Background = _cells[col][row]
                ? new SolidColorBrush(Color.FromArgb(160, 200, 50, 50))
                : new SolidColorBrush(Colors.Transparent);
        }
    }

    public class InventoryOverlayData
    {
        public int InventoryWidth { get; set; } = 600;
        public int InventoryHeight { get; set; } = 250;
        public int[] InventoryPosition { get; set; } = [1000, 1000];
        public bool[][] InventorySlots { get; set; } = new bool[InventoryManagementConfig.GridWidth][];
    }
}
