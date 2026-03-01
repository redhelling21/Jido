using System;
using System.Collections.Generic;
using Jido.Config;
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

        //Ensures the Mat is 3-channel BGR
        public static Mat EnsureBgr(Mat src)
        {
            if (src.Channels() == 3)
                return src;

            var dst = new Mat();
            Cv2.CvtColor(src, dst, ColorConversionCodes.BGRA2BGR);
            src.Dispose();
            return dst;
        }

        // Finds ⌟ corners (bottom-right angle of a square) via template matching
        public static List<(int x, int y)> FindCorners(Mat mat, FillInventoryConfig cfg)
        {
            // Build mask to isolate the color of the corner
            int r = cfg.LineColor[0],
                g = cfg.LineColor[1],
                b = cfg.LineColor[2];
            int tol = cfg.ColorTolerance;
            var lower = new Scalar(Math.Max(0, b - tol), Math.Max(0, g - tol), Math.Max(0, r - tol));
            var upper = new Scalar(Math.Min(255, b + tol), Math.Min(255, g + tol), Math.Min(255, r + tol));

            using var mask = new Mat();
            Cv2.InRange(mat, lower, upper, mask);

            // Build a bottom-right corner shape
            int arm = cfg.ArmLengthPx;
            int thick = cfg.LineThicknessPx;
            using var templ = new Mat(arm, arm, MatType.CV_8UC1, Scalar.All(0));
            // Vertical line
            Cv2.Rectangle(templ, new Point(arm - thick, 0), new Point(arm - 1, arm - 1), Scalar.All(255), -1);
            // Horizontal line
            Cv2.Rectangle(templ, new Point(0, arm - thick), new Point(arm - 1, arm - 1), Scalar.All(255), -1);

            // Match the template with the mask
            using var result = new Mat();
            Cv2.MatchTemplate(mask, templ, result, TemplateMatchModes.CCoeffNormed);

            // Group results by location, pick the best match, remove the others close to it, next
            var targets = new List<(int x, int y)>();
            using var work = result.Clone();
            while (true)
            {
                // Best match
                Cv2.MinMaxLoc(work, out _, out double maxVal, out _, out Point maxLoc);
                if (maxVal < cfg.MatchThreshold)
                    break;

                // Flag the location to be clicked
                targets.Add((maxLoc.X + arm / 2, maxLoc.Y + arm / 2));

                // Remove the others around it
                int sx = Math.Max(0, maxLoc.X - arm / 2);
                int sy = Math.Max(0, maxLoc.Y - arm / 2);
                int ex = Math.Min(work.Cols - 1, maxLoc.X + arm / 2);
                int ey = Math.Min(work.Rows - 1, maxLoc.Y + arm / 2);
                Cv2.Rectangle(work, new Point(sx, sy), new Point(ex, ey), Scalar.All(0.0), -1);
            }

            return targets;
        }
    }
}
