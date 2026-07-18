using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace Jido.Utils
{
    public static class ScreenUtils
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        // Only used if the OS somehow reports a nonsensical size — a zero-sized Bitmap throws.
        private const int FallbackWidth = 1920;
        private const int FallbackHeight = 1080;

        // Primary monitor size in physical pixels
        public static int PrimaryWidth
        {
            get
            {
                var width = GetSystemMetrics(SM_CXSCREEN);
                return width > 0 ? width : FallbackWidth;
            }
        }

        public static int PrimaryHeight
        {
            get
            {
                var height = GetSystemMetrics(SM_CYSCREEN);
                return height > 0 ? height : FallbackHeight;
            }
        }

        public static Rectangle PrimaryBounds => new(0, 0, PrimaryWidth, PrimaryHeight);

        public static Mat CaptureScreen(Rectangle bounds, Bitmap? reusedBitmap = null)
        {
            var screenshot = reusedBitmap ?? new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppRgb);
            try
            {
                using var graphics = Graphics.FromImage(screenshot);
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                return screenshot.ToMat();
            }
            finally
            {
                // Only dispose if we allocated it
                if (reusedBitmap is null)
                    screenshot.Dispose();
            }
        }
    }
}
