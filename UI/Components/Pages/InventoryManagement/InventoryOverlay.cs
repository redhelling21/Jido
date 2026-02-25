using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Jido.UI.Components.Pages.InventoryManagement
{
    public class InventoryOverlay : Window
    {
        private const int GridWidth = 12;
        private const int GridHeight = 5;

        private bool[][] _cells = new bool[GridWidth][];

        public InventoryOverlay(InventoryOverlayData data)
        {
            Title = "Inventory";
            Background = new SolidColorBrush(Colors.Transparent);
            //TransparencyLevelHint = WindowTransparencyLevel.AcrylicBlur;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
            SystemDecorations = SystemDecorations.None;

            Width = data.InventoryWidth;
            Height = data.InventoryHeight;
            Position = new PixelPoint(data.InventoryPosition[0], data.InventoryPosition[1]);
            _cells = data.InventorySlots;
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", GridWidth))),
                RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("*", GridHeight)))
            };
            for (int col = 0; col < GridWidth; col++)
            {
                if (_cells[col] == null)
                {
                    _cells[col] = new bool[GridHeight];
                }
                for (int row = 0; row < GridHeight; row++)
                {
                    var cell = new Border
                    {
                        Background = new SolidColorBrush(Colors.Transparent),
                        BorderBrush = new SolidColorBrush(Colors.White),
                        BorderThickness = new Thickness(1)
                    };

                    cell.PointerPressed += Cell_PointerPressed;
                    if (_cells[col][row])
                    {
                        cell.Background = new SolidColorBrush(Colors.Red, 0.6);
                    }

                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, col);
                    grid.Children.Add(cell);
                }
            }
            Content = grid;
        }

        public InventoryOverlayData GetInventoryConfig()
        {
            return new InventoryOverlayData
            {
                InventoryWidth = (int)Width,
                InventoryHeight = (int)Height,
                InventorySlots = _cells,
                InventoryPosition = new int[] { Position.X, Position.Y }
            };
        }

        private void Cell_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border cell)
            {
                var x = Grid.GetColumn(cell);
                var y = Grid.GetRow(cell);
                _cells[x][y] = !_cells[x][y];
                if (_cells[x][y])
                {
                    cell.Background = new SolidColorBrush(Colors.Red, 0.6);
                }
                else
                {
                    cell.Background = new SolidColorBrush(Colors.Transparent);
                }
            }
        }
    }

    public class InventoryOverlayData
    {
        public int InventoryWidth { get; set; } = 600;
        public int InventoryHeight { get; set; } = 250;
        public int[] InventoryPosition { get; set; } = { 1000, 1000 };
        public bool[][] InventorySlots { get; set; } = new bool[12][];
    }
}
