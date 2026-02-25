using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Jido.Config;
using Jido.Utils;
using OpenCvSharp;
using SharpHook;
using SharpHook.Native;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace Jido.Services
{
    public class AutolootService : BaseToggleableService, IAutolootService
    {
        private CancellationTokenSource _cancellationTokenSource;

        public AutolootService(
            IHooksManager keyHooksManager,
            JidoConfig config,
            IMacroService macroService,
            IServiceHub serviceHub
        )
            : base(
                keyHooksManager,
                config,
                macroService,
                config.Features.Autoloot.ToggleKey,
                serviceHub,
                ServiceNames.Autoloot
            )
        { }

        public override void Toggle()
        {
            if (Status == ServiceStatus.STOPPED)
            {
                if (_macroService.Status == ServiceStatus.STOPPED)
                    return;
                _cancellationTokenSource = new CancellationTokenSource();
                Status = ServiceStatus.IDLE;
                _ = Task.Run(() => AutolootRoutine(_cancellationTokenSource.Token));
            }
            else
                StopRoutine();
        }

        protected override void StopRoutine()
        {
            _cancellationTokenSource?.Cancel();
            Status = ServiceStatus.STOPPED;
        }

        protected override void PersistToggleKey(KeyCode key) => _config.Features.Autoloot.ToggleKey = key;

        private async Task AutolootRoutine(CancellationToken cancellationToken)
        {
            var cfg = _config.Features.Autoloot;

            // Capture the center quarter of the screen
            int width = _config.Screen.Width / 2;
            int height = _config.Screen.Height / 2;
            Debug.WriteLine($"Width: {_config.Screen.Width}");
            int captureX = width / 2;
            int captureY = height / 2;
            Rectangle captureRegion = new Rectangle(captureX, captureY, width, height);

            // Center of the captured image — used to find the closest rect
            double imageCenterX = width / 2.0;
            double imageCenterY = height / 2.0;

            var rng = new Random();

            // Pre-allocate Mats outside the loop to avoid GC pressure
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            using var dilated = new Mat();
            using var eroded = new Mat();
            using var gradient = new Mat();
            using var combined = new Mat();

            while (!cancellationToken.IsCancellationRequested)
            {
                using Mat screenImage = ScreenUtils.CaptureScreen(captureRegion);

                // Morphological gradient: dilate - erode highlights edges on all channels
                Cv2.Dilate(screenImage, dilated, kernel);
                Cv2.Erode(screenImage, eroded, kernel);
                Cv2.Subtract(dilated, eroded, gradient);

                // Collapse 3-channel gradient to 1 channel by taking per-pixel max
                Cv2.Split(gradient, out Mat[] ch);
                Cv2.Max(ch[0], ch[1], combined);
                Cv2.Max(combined, ch[2], combined);
                ch[0].Dispose();
                ch[1].Dispose();
                ch[2].Dispose();

                Cv2.Threshold(combined, combined, cfg.Threshold, 255, ThresholdTypes.Binary);

                Cv2.FindContours(
                    combined,
                    out Point[][] contours,
                    out _,
                    RetrievalModes.List,
                    ContourApproximationModes.ApproxSimple
                );

                // Find the rectangle closest to the center of the captured region
                Rect? bestRect = null;
                double bestDist = double.MaxValue;

                foreach (var contour in contours)
                {
                    if (Cv2.ContourArea(contour) < cfg.MinArea)
                        continue;

                    Point[] approx = Cv2.ApproxPolyDP(contour, cfg.Epsilon, true);
                    if (approx.Length != 4 || !Cv2.IsContourConvex(approx))
                        continue;

                    Rect br = Cv2.BoundingRect(approx);
                    double ar = (double)br.Width / br.Height;
                    if (ar < 1.0 || ar > cfg.MaxAspectRatio)
                        continue;
                    double cx = br.X + br.Width / 2.0;
                    double cy = br.Y + br.Height / 2.0;
                    double dist = Math.Sqrt(
                        (cx - imageCenterX) * (cx - imageCenterX) + (cy - imageCenterY) * (cy - imageCenterY)
                    );

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestRect = br;
                    }
                }

                if (bestRect.HasValue && !_serviceHub.IsActive(ServiceNames.Autopress))
                {
                    var br = bestRect.Value;
                    // Randomize the click position within the bounding rect
                    int clickX = captureX + rng.Next(br.X, br.X + br.Width);
                    int clickY = captureY + rng.Next(br.Y, br.Y + br.Height);

                    Status = ServiceStatus.WORKING;
                    await SimulationUtils.MouseMoveAndClickAsync((short)clickX, (short)clickY);
                    Status = ServiceStatus.IDLE;
                }
                else if (Status != ServiceStatus.IDLE)
                {
                    Status = ServiceStatus.IDLE;
                }

                // Rate-limit: wait the remainder of the configured interval
                int delayMs = (int)(1000.0 / cfg.MaxClicksPerSecond);
                await Task.Delay(delayMs);
            }
        }

        public override void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            base.Dispose();
        }
    }

    public interface IAutolootService : IServiceWithStatus, IToggleableService
    { }
}
