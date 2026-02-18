using System.Drawing.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Point = OpenCvSharp.Point;

namespace OpenCVSandbox.Tests
{
    internal static class SimpleColoredRectangles
    {
        public static void Run()
        {
            // Create a window to display the screen capture
            Cv2.NamedWindow("ScreenCapture");

            // Set up the screen capture
            int counter = 0;
            double averageTime = 0;
            double totalMS = 0;
            Mat lastMask = null;
            while (true)
            {
                counter++;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                using (var screenImage = CaptureScreen())
                {
                    using Mat mask1 = new Mat();
                    using Mat mask2 = new Mat();
                    using Mat mask3 = new Mat();
                    using Mat mask4 = new Mat();
                    using Mat finalMask = new Mat();
                    Scalar lowerBound1 = new Scalar(252, 0, 252); // Adjusted lower bound
                    Scalar upperBound1 = new Scalar(255, 20, 255); // Adjusted upper bound
                    Scalar lowerBound2 = new Scalar(50, 203, 253); // Adjusted lower bound
                    Scalar upperBound2 = new Scalar(51, 205, 255); // Adjusted upper bound
                    Scalar lowerBound3 = new Scalar(0, 203, 253); // Adjusted lower bound
                    Scalar upperBound3 = new Scalar(2, 205, 255); // Adjusted upper bound
                    Scalar lowerBound4 = new Scalar(150, 203, 253); // Adjusted lower bound
                    Scalar upperBound4 = new Scalar(152, 205, 255); // Adjusted upper bound

                    Cv2.InRange(screenImage, lowerBound1, upperBound1, mask1);
                    Cv2.InRange(screenImage, lowerBound2, upperBound2, mask2);
                    Cv2.InRange(screenImage, lowerBound3, upperBound3, mask3);
                    Cv2.InRange(screenImage, lowerBound4, upperBound4, mask4);
                    Cv2.BitwiseOr(mask1, mask2, finalMask);
                    Cv2.BitwiseOr(finalMask, mask3, finalMask);
                    Cv2.BitwiseOr(finalMask, mask4, finalMask);
                    /*if (lastMask != null)
                    {
                        using Mat diff = new Mat();
                        Cv2.Absdiff(thresh, lastMask, diff);
                        var count = Cv2.CountNonZero(diff);
                        if (Cv2.CountNonZero(diff) < 100)
                        {
                            Console.WriteLine("No change : " + count);
                        }
                        else
                        {
                            Console.WriteLine("CHANGED : " + count);
                        }
                    }
                    lastMask = thresh.Clone();*/
                    Point[][] contours;
                    HierarchyIndex[] hierarchy;
                    Cv2.FindContours(
                        finalMask,
                        out contours,
                        out hierarchy,
                        RetrievalModes.List,
                        ContourApproximationModes.ApproxSimple
                    );

                    for (int i = 0; i < contours.Length; i++)
                    {
                        if (Cv2.ContourArea(contours[i]) < 100)
                            continue;
                        /*if (Cv2.ContourArea(contours[i]) > 1000)
                            continue;*/
                        Point[] contour = contours[i];
                        Point[] approxCurve = Cv2.ApproxPolyDP(contours[i], Cv2.ArcLength(contour, true) * 0.02, true);
                        if (approxCurve.Length != 4)
                        {
                            continue;
                        }
                        Cv2.DrawContours(screenImage, contours, i, new Scalar(0, 255, 0), 2);
                        // Calculate centroid of contour to place the label
                        Moments moments = Cv2.Moments(contours[i]);
                        int cx = (int)(moments.M10 / moments.M00);
                        int cy = (int)(moments.M01 / moments.M00);
                        // Draw the label
                        Cv2.PutText(
                            screenImage,
                            i.ToString(),
                            new Point(cx, cy),
                            HersheyFonts.HersheyPlain,
                            1,
                            new Scalar(0, 255, 0),
                            2
                        );
                    }
                    watch.Stop();
                    totalMS += watch.ElapsedMilliseconds;
                    averageTime = totalMS / counter;
                    Console.WriteLine("Average time: " + averageTime);
                    Cv2.ImShow("ScreenCapture", screenImage);
                    // Wait for a key press for a short time (10ms)
                    int key = Cv2.WaitKey(10);
                    if (key == (int)ConsoleKey.Escape) // Exit if the escape key is pressed
                        break;
                }
            }

            // Destroy the window
            Cv2.DestroyAllWindows();
        }

        public static Mat CaptureScreen()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            int width = bounds.Width / 2;
            int height = bounds.Height / 2;
            int x = bounds.X + width / 2;
            int y = bounds.Y + height / 2;
            Rectangle centerBounds = new Rectangle(x, y, width, height);
            // Create a Bitmap object to hold the screen capture
            Bitmap screenshot = new Bitmap(centerBounds.Width, centerBounds.Height, PixelFormat.Format32bppRgb);

            // Capture the screen into the Bitmap object
            using (Graphics graphics = Graphics.FromImage(screenshot))
            {
                graphics.CopyFromScreen(
                    centerBounds.X,
                    centerBounds.Y,
                    0,
                    0,
                    centerBounds.Size,
                    CopyPixelOperation.SourceCopy
                );
            }
            Mat mat = screenshot.ToMat();
            screenshot.Dispose();
            return mat;
        }
    }
}
