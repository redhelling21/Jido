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
    }
}
