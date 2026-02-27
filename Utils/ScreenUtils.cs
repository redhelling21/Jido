using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace Jido.Utils
{
    public static class ScreenUtils
    {
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
