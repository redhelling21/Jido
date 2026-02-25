using System.Drawing.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace OpenCVSandbox.Tests
{
    // AR 100 MinArea 2000 Threshold 90 Epsilon 2
    internal static class ColorlessRectangleDetection
    {
        public static void Run()
        {
            Cv2.NamedWindow("ScreenCapture");
            Cv2.NamedWindow("Edges");
            Cv2.MoveWindow("ScreenCapture", 0, 0);
            Cv2.MoveWindow("Edges", 700, 0);

            int minArea = 2000;
            int epsilon = 3;
            int threshold = 100;
            int maxAR = 20;

            Cv2.CreateTrackbar("Min Area", "Edges", ref minArea, 50000);
            Cv2.CreateTrackbar("Epsilon px", "Edges", ref epsilon, 20);
            Cv2.CreateTrackbar("Threshold", "Edges", ref threshold, 255);
            Cv2.CreateTrackbar("Max AR x10", "Edges", ref maxAR, 100);

            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));

            // Pre-allocate all Mats once — reused every frame, no GC pressure
            using var dilated = new Mat();
            using var eroded = new Mat();
            using var gradient = new Mat(); // 3-channel BGR gradient
            using var combined = new Mat(); // single channel max across BGR
            using var channels0 = new Mat();
            using var channels1 = new Mat();
            using var channels2 = new Mat();

            int counter = 0;
            double totalMS = 0;

            while (true)
            {
                counter++;
                var watch = System.Diagnostics.Stopwatch.StartNew();

                using var screenImage = CaptureScreen();

                // Single dilate/erode on the full BGR image instead of 3 separate passes — OpenCV
                // processes each channel independently under the hood, so the result is identical
                // but with 1/3 the morphological op overhead
                Cv2.Dilate(screenImage, dilated, kernel);
                Cv2.Erode(screenImage, eroded, kernel);
                Cv2.Subtract(dilated, eroded, gradient); // 3-channel gradient

                // Reduce 3-channel gradient to 1 channel by taking the per-pixel max — a border
                // visible in any channel will survive
                Cv2.Split(gradient, out Mat[] ch);
                Cv2.Max(ch[0], ch[1], combined);
                Cv2.Max(combined, ch[2], combined);
                ch[0].Dispose();
                ch[1].Dispose();
                ch[2].Dispose();

                Cv2.Threshold(combined, combined, threshold, 255, ThresholdTypes.Binary);

                Cv2.ImShow("Edges", combined);

                Cv2.FindContours(
                    combined,
                    out Point[][] contours,
                    out _,
                    RetrievalModes.List,
                    ContourApproximationModes.ApproxSimple
                );

                var rects = new List<Point[]>();
                foreach (var contour in contours)
                {
                    if (Cv2.ContourArea(contour) < minArea)
                        continue;

                    Point[] approx = Cv2.ApproxPolyDP(contour, epsilon, true);
                    if (approx.Length != 4)
                        continue;

                    if (!Cv2.IsContourConvex(approx))
                        continue;

                    Rect br = Cv2.BoundingRect(approx);
                    double ar = (double)br.Width / br.Height;
                    double maxAspect = maxAR / 10.0;
                    if (ar < 1.0 || ar > maxAspect)
                        continue;

                    rects.Add(approx);
                }

                foreach (var rect in rects)
                {
                    Rect br = Cv2.BoundingRect(rect);
                    Cv2.Rectangle(screenImage, br, new Scalar(0, 255, 0), -1);
                    Cv2.Rectangle(screenImage, br, new Scalar(0, 0, 255), 2);
                    Cv2.PutText(
                        screenImage,
                        $"{br.Width}x{br.Height}",
                        new Point(br.X, br.Y - 5),
                        HersheyFonts.HersheyPlain,
                        1.2,
                        new Scalar(255, 255, 255),
                        2
                    );
                }

                watch.Stop();
                totalMS += watch.ElapsedMilliseconds;
                Console.WriteLine(
                    $"Avg: {totalMS / counter:F1}ms | Rects: {rects.Count} | Total contours: {contours.Length}"
                );

                Cv2.ImShow("ScreenCapture", screenImage);

                if (Cv2.WaitKey(10) == (int)ConsoleKey.Escape)
                    break;
            }

            Cv2.DestroyAllWindows();
        }

        public static Mat CaptureScreen()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            int width = bounds.Width / 2;
            int height = bounds.Height / 2;
            int x = bounds.X + width / 2;
            int y = bounds.Y + height / 2;

            using var screenshot = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            using (var g = Graphics.FromImage(screenshot))
            {
                g.CopyFromScreen(x, y, 0, 0, screenshot.Size, CopyPixelOperation.SourceCopy);
            }

            return screenshot.ToMat();
        }
    }
}
