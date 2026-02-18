using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Jido.Config;
using Jido.Utils;
using OpenCvSharp;
using SharpHook;
using SharpHook.Native;
using Color = Jido.Models.Color;
using Point = OpenCvSharp.Point;

namespace Jido.Services
{
    public class AutolootService : IAutolootService, IDisposable
    {
        private IHooksManager _keyHooksManager;
        private CancellationTokenSource _cancellationTokenSource;
        private JidoConfig _config;
        private KeyCode _toggleKey;
        private List<Color> _colors;

        public KeyCode ToggleKey
        {
            get => _toggleKey;
        }

        public List<Color> Colors
        {
            get => _colors;
        }

        public ServiceStatus Status { get; set; } = ServiceStatus.STOPPED;

        public event EventHandler<ServiceStatus> StatusChanged;

        public AutolootService(IHooksManager keyHooksManager, JidoConfig config)
        {
            _keyHooksManager = keyHooksManager;
            _config = config;
            _toggleKey = _config.Features.Autoloot.ToggleKey;
            _colors = _config.Features.Autoloot.Colors;
            _keyHooksManager.RegisterKey(_toggleKey, ToggleAutoloot);
        }

        public Task<KeyCode> ChangeToggleKey()
        {
            var task = _keyHooksManager
                .ListenNextKey()
                .ContinueWith(
                    (key) =>
                    {
                        _keyHooksManager.UnregisterKey(_toggleKey);
                        _toggleKey = key.Result;
                        _config.Features.Autoloot.ToggleKey = key.Result;
                        _config.Persist();
                        _keyHooksManager.RegisterKey(_toggleKey, ToggleAutoloot);
                        return _toggleKey;
                    }
                );
            return task;
        }

        public void UpdateColors(List<Color> colors)
        {
            _colors = colors;
            _config.Features.Autoloot.Colors = colors;
            _config.Persist();
        }

        private void ToggleAutoloot(object? sender, EventArgs e)
        {
            if (Status == ServiceStatus.STOPPED)
            {
                Status = ServiceStatus.IDLE;
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => AutolootRoutine(_cancellationTokenSource.Token))
                    .ContinueWith(
                        (t) =>
                        {
                            if (t.IsFaulted)
                                throw t.Exception;
                        }
                    );
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
            int width = _config.Screen.Width / 2;
            int height = _config.Screen.Height / 2;
            int x = width / 2;
            int y = height / 2;

            Mat lastMask = null;
            Rectangle centerBounds = new Rectangle(x, y, width, height);
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100);
                using Mat screenImage = ScreenUtils.CaptureScreen(centerBounds);
                using Mat finalMask = new Mat(height, width, MatType.CV_8UC3, new Scalar(0));
                foreach (var color in _colors)
                {
                    (Scalar lower, Scalar higher) = color.ToBGRScalarRange(1);
                    using Mat mask = new Mat();
                    Cv2.InRange(screenImage, lower, higher, mask);
                    Cv2.BitwiseOr(finalMask, mask, finalMask);
                }
                if (lastMask != null)
                {
                    using Mat diff = new Mat();
                    Cv2.Absdiff(finalMask, lastMask, diff);
                    var count = Cv2.CountNonZero(diff);
                    if (Cv2.CountNonZero(diff) > 100)
                    {
                        lastMask.Dispose();
                        lastMask = finalMask.Clone();
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
                    lastMask = finalMask.Clone();
                }
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

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _keyHooksManager.Dispose();
        }
    }

    public interface IAutolootService : IServiceWithStatus, IToggleableService
    {
        public void UpdateColors(List<Color> colors);

        public List<Color> Colors { get; }
    }
}
