using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using Jido.Config;
using Jido.Utils;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SharpHook;
using SharpHook.Data;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace Jido.Services
{
    public class AutolootService : BaseToggleableService, IAutolootService
    {
        private readonly ILogger<AutolootService> _logger;
        private CancellationTokenSource _cancellationTokenSource;

        private double _averageCycleMs;
        public double AverageCycleMs => _averageCycleMs;

        public event EventHandler<double>? AverageCycleMsUpdated;

        public double MaxClicksPerSecond => _config.Features.Autoloot.MaxClicksPerSecond;
        public double CaptureRatio => _config.Features.Autoloot.CaptureRatio;

        public AutolootService(
            IHooksManager keyHooksManager,
            JidoConfig config,
            IMacroService macroService,
            IServiceHub serviceHub,
            ILogger<AutolootService> logger
        )
            : base(
                keyHooksManager,
                config,
                macroService,
                config.Features.Autoloot.ToggleKey,
                serviceHub,
                ServiceNames.Autoloot
            )
        {
            _logger = logger;
        }

        public override void Toggle()
        {
            if (Status == ServiceStatus.STOPPED)
            {
                if (_macroService.Status == ServiceStatus.STOPPED)
                {
                    _logger.LogWarning("Cannot start autoloot: macro service is stopped.");
                    return;
                }
                _logger.LogInformation("Autoloot started.");
                _cancellationTokenSource = new CancellationTokenSource();
                Status = ServiceStatus.IDLE;
                _ = Task.Run(() => AutolootRoutine(_cancellationTokenSource.Token));
            }
            else
            {
                _logger.LogInformation("Autoloot stopped.");
                StopRoutine();
            }
        }

        protected override void StopRoutine()
        {
            _cancellationTokenSource?.Cancel();
            Status = ServiceStatus.STOPPED;
        }

        protected override void PersistToggleKey(KeyCode key) => _config.Features.Autoloot.ToggleKey = key;

        public void UpdateConfig(double maxClicksPerSecond, double captureRatio)
        {
            if (Status != ServiceStatus.STOPPED)
                StopRoutine();
            _config.Features.Autoloot.MaxClicksPerSecond = maxClicksPerSecond;
            _config.Features.Autoloot.CaptureRatio = captureRatio;
            _config.Persist();
        }

        private async Task AutolootRoutine(CancellationToken cancellationToken)
        {
            try
            {
                // Snapshot config once — changes require a restart to apply
                var cfg = _config.Features.Autoloot;

                int width = (int)(_config.Screen.Width * cfg.CaptureRatio);
                int height = (int)(_config.Screen.Height * cfg.CaptureRatio);
                // Top-left pixel of the rectangle
                int captureX = (_config.Screen.Width - width) / 2;
                int captureY = (_config.Screen.Height - height) / 2;
                var captureRegion = new Rectangle(captureX, captureY, width, height);

                _logger.LogInformation(
                    "Routine config — capture region: {W}x{H} at ({X},{Y}), threshold: {T}, minArea: {A}, maxAspect: {R}, maxCps: {C}",
                    width,
                    height,
                    captureX,
                    captureY,
                    cfg.Threshold,
                    cfg.MinArea,
                    cfg.MaxAspectRatio,
                    cfg.MaxClicksPerSecond
                );

                double imageCenterX = width / 2.0;
                double imageCenterY = height / 2.0;

                var rng = new Random();

                // Movement detection: skip clicks while the character is walking toward loot
                const double movingThresholdPx = 10.0;
                bool hasPrevRect = false;
                double prevCenterX = 0,
                    prevCenterY = 0;

                // Pre-allocate Mats and bitmap outside the loop to avoid GC pressure
                using var captureBitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);
                using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
                using var dilated = new Mat();
                using var eroded = new Mat();
                using var gradient = new Mat();
                using var combined = new Mat();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var sw = Stopwatch.StartNew();

                    using Mat screenImage = ScreenUtils.CaptureScreen(captureRegion, captureBitmap);

                    // Morphological gradient: dilate − erode highlights edges on all channels
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

                    sw.Stop();

                    // Exponential smoothing
                    double elapsed = sw.Elapsed.TotalMilliseconds;
                    _averageCycleMs = _averageCycleMs == 0 ? elapsed : 0.1 * elapsed + (1 - 0.1) * _averageCycleMs;
                    AverageCycleMsUpdated?.Invoke(this, _averageCycleMs);

                    _logger.LogDebug(
                        "Detection — {N} contours found, cycle: {Ms:F1}ms (avg: {Avg:F1}ms)",
                        contours.Length,
                        elapsed,
                        _averageCycleMs
                    );

                    if (bestRect.HasValue && !_serviceHub.IsActive(ServiceNames.Autopress))
                    {
                        var br = bestRect.Value;
                        double centerX = br.X + br.Width / 2.0;
                        double centerY = br.Y + br.Height / 2.0;

                        double displacement = hasPrevRect
                            ? Math.Sqrt(Math.Pow(centerX - prevCenterX, 2) + Math.Pow(centerY - prevCenterY, 2))
                            : 0;
                        bool isMoving = displacement > movingThresholdPx;

                        _logger.LogDebug(
                            "Best rect: ({X},{Y}) {W}x{H}, center: ({CX:F0},{CY:F0}), displacement: {D:F1}px — {State}",
                            br.X,
                            br.Y,
                            br.Width,
                            br.Height,
                            centerX,
                            centerY,
                            displacement,
                            isMoving ? "MOVING, skip" : "STILL"
                        );

                        prevCenterX = centerX;
                        prevCenterY = centerY;
                        hasPrevRect = true;

                        if (!isMoving)
                        {
                            int clickX = captureX + rng.Next(br.X, br.X + br.Width);
                            int clickY = captureY + rng.Next(br.Y, br.Y + br.Height);

                            _logger.LogInformation("Clicking at ({X},{Y}).", clickX, clickY);
                            Status = ServiceStatus.WORKING;
                            await SimulationUtils.MouseMoveAndClickAsync(
                                (short)clickX,
                                (short)clickY,
                                moveDurationMs: 50
                            );
                            _logger.LogDebug("Click done.");
                            Status = ServiceStatus.IDLE;
                        }
                    }
                    else
                    {
                        if (bestRect.HasValue)
                            _logger.LogDebug("Rect found but autopress is active, skipping.");
                        else
                            _logger.LogDebug("No rect detected.");

                        hasPrevRect = false;
                        if (Status != ServiceStatus.IDLE)
                            Status = ServiceStatus.IDLE;
                    }

                    // Rate-limit: wait the remainder of the configured interval
                    int delayMs = (int)(1000.0 / cfg.MaxClicksPerSecond);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in AutolootRoutine; stopping service.");
                StopRoutine();
            }
        }

        public override void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            base.Dispose();
        }
    }

    public interface IAutolootService : IServiceWithStatus, IToggleableService
    {
        double MaxClicksPerSecond { get; }
        double CaptureRatio { get; }
        double AverageCycleMs { get; }

        event EventHandler<double>? AverageCycleMsUpdated;

        void UpdateConfig(double maxClicksPerSecond, double captureRatio);
    }
}
