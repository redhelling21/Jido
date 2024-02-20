using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using Jido.Utils;
using Microsoft.Extensions.Configuration;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using SharpHook;
using SharpHook.Native;
using static OpenCvSharp.Stitcher;
using Point = OpenCvSharp.Point;

namespace Jido.Services
{
    public class AutolootService : IAutolootService
    {
        private IKeyHooksManager _keyHooksManager;
        private EventSimulator _simulator = new EventSimulator();
        private CancellationTokenSource _cancellationTokenSource;
        private IConfiguration _config;
        public ServiceStatus Status { get; set; } = ServiceStatus.STOPPED;

        public event EventHandler<ServiceStatus> StatusChanged;

        public AutolootService(IKeyHooksManager keyHooksManager, IConfiguration config)
        {
            _keyHooksManager = keyHooksManager;
            _config = config;

            var toggleKey = SharpHook.Native.KeyCode.VcF3;
            _keyHooksManager.RegisterKey(toggleKey, ToggleAutoloot);
        }

        private void ToggleAutoloot(object? sender, EventArgs e)
        {
            if (Status == ServiceStatus.STOPPED)
            {
                Status = ServiceStatus.IDLE;
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => AutolootRoutine(_cancellationTokenSource.Token));
            }
            else
            {
                _cancellationTokenSource.Cancel();
                Status = ServiceStatus.STOPPED;
            }
            StatusChanged?.Invoke(this, Status);
        }

        private async Task AutolootRoutine(CancellationToken cancellationToken)
        {
            int width = Int32.Parse(_config.GetSection("screen")["width"]) / 2;
            int height = Int32.Parse(_config.GetSection("screen")["height"]) / 2;
            int x = width / 2;
            int y = height / 2;

            Mat lastMask = null;
            Rectangle centerBounds = new Rectangle(x, y, width, height);
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100);
                using Mat screenImage = ScreenUtils.CaptureScreen(centerBounds);
                using Mat thresh = new Mat();
                Scalar lowerBound = new Scalar(253, 0, 253); // Adjusted lower bound
                Scalar upperBound = new Scalar(255, 2, 255); // Adjusted upper bound
                Cv2.InRange(screenImage, lowerBound, upperBound, thresh);
                if (lastMask != null)
                {
                    using Mat diff = new Mat();
                    Cv2.Absdiff(thresh, lastMask, diff);
                    var count = Cv2.CountNonZero(diff);
                    if (Cv2.CountNonZero(diff) > 100)
                    {
                        lastMask.Dispose();
                        lastMask = thresh.Clone();
                        continue;
                    }
                    else if (Status == ServiceStatus.WORKING)
                    {
                        Status = ServiceStatus.IDLE;
                        StatusChanged?.Invoke(this, Status);
                    }
                }
                else
                {
                    lastMask = thresh.Clone();
                }
                Point[][] contours;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(
                    thresh,
                    out contours,
                    out hierarchy,
                    RetrievalModes.List,
                    ContourApproximationModes.ApproxSimple
                );

                for (int i = 0; i < contours.Length; i++)
                {
                    if (Cv2.ContourArea(contours[i]) < 100)
                        continue;
                    Point[] contour = contours[i];
                    Point[] approxCurve = Cv2.ApproxPolyDP(contours[i], Cv2.ArcLength(contour, true) * 0.02, true);
                    if (approxCurve.Length != 4)
                    {
                        continue;
                    }
                    Status = ServiceStatus.WORKING;
                    StatusChanged?.Invoke(this, Status);
                    // Calculate centroid of contour
                    Moments moments = Cv2.Moments(contours[i]);
                    short cx = (short)(moments.M10 / moments.M00);
                    short cy = (short)(moments.M01 / moments.M00);
                    await SimulationUtils.MouseMoveAndClickAsync((short)(cx + x), (short)(cy + y));
                    break;
                }
            }
        }
    }

    public interface IAutolootService : IServiceWithStatus
    { }
}
