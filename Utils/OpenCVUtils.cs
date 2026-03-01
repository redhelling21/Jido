using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jido.Models;
using OpenCvSharp;

namespace Jido.Utils
{
    public static class OpenCVUtils
    {
        public static Scalar ToBGRScalar(this Color color)
        {
            return new Scalar(color.RGB[2], color.RGB[1], color.RGB[0]);
        }

        public static (Scalar, Scalar) ToBGRScalarRange(this Color colors, int tolerance)
        {
            var lower = new Scalar(
                Math.Max(0, colors.RGB[2] - tolerance),
                Math.Max(0, colors.RGB[1] - tolerance),
                Math.Max(0, colors.RGB[0] - tolerance)
            );
            var upper = new Scalar(
                Math.Min(255, colors.RGB[2] + tolerance),
                Math.Min(255, colors.RGB[1] + tolerance),
                Math.Min(255, colors.RGB[0] + tolerance)
            );
            return (lower, upper);
        }

        /// <summary>
        /// Ensures the Mat is 3-channel BGR. If it is already 3-channel it is returned as-is;
        /// otherwise a BGRA→BGR conversion is made and the source Mat is disposed.
        /// </summary>
        public static Mat EnsureBgr(Mat src)
        {
            if (src.Channels() == 3)
                return src;

            var dst = new Mat();
            Cv2.CvtColor(src, dst, ColorConversionCodes.BGRA2BGR);
            src.Dispose();
            return dst;
        }
    }
}
