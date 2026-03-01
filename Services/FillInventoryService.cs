using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Jido.Config;
using Jido.Utils;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SharpHook;
using SharpHook.Data;

namespace Jido.Services
{
    public class FillInventoryService : BaseToggleableService, IFillInventoryService
    {
        private static readonly EventSimulator _simulator = new EventSimulator();
        private readonly ILogger<FillInventoryService> _logger;
        private CancellationTokenSource? _cts;

        public FillInventoryConfig Config => _config.Features.FillInventory;

        public FillInventoryService(
            IHooksManager keyHooksManager,
            JidoConfig config,
            IMacroService macroService,
            IServiceHub serviceHub,
            ILogger<FillInventoryService> logger
        )
            : base(
                keyHooksManager,
                config,
                macroService,
                config.Features.FillInventory.ToggleKey,
                serviceHub,
                ServiceNames.FillInventory
            )
        {
            _logger = logger;
        }

        public void UpdateConfig(int clickDelayMs)
        {
            _config.Features.FillInventory.ClickDelayMs = clickDelayMs;
            _config.Persist();
        }

        public override void Toggle()
        {
            if (Status == ServiceStatus.WORKING)
            {
                StopRoutine();
                return;
            }

            if (_macroService.Status == ServiceStatus.STOPPED)
                return;

            // Prevent the routine from running if another inv-related service runs
            if (_serviceHub.IsActive(ServiceNames.InventoryManagement))
                return;

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => FillRoutine(_cts.Token));
        }

        protected override void StopRoutine()
        {
            _cts?.Cancel();
            Status = ServiceStatus.STOPPED;
        }

        protected override void PersistToggleCombo(KeyCombo combo) => _config.Features.FillInventory.ToggleKey = combo;

        public override void Dispose()
        {
            _cts?.Dispose();
            base.Dispose();
        }

        // Finds ⌟ corners via template matching.
        private List<(int x, int y)> FindCorners(Mat mat, FillInventoryConfig cfg)
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
            Cv2.Rectangle(
                templ,
                new OpenCvSharp.Point(arm - thick, 0),
                new OpenCvSharp.Point(arm - 1, arm - 1),
                Scalar.All(255),
                -1
            );
            // Horizontal line
            Cv2.Rectangle(
                templ,
                new OpenCvSharp.Point(0, arm - thick),
                new OpenCvSharp.Point(arm - 1, arm - 1),
                Scalar.All(255),
                -1
            );

            // Match the template with the mask
            using var result = new Mat();
            Cv2.MatchTemplate(mask, templ, result, TemplateMatchModes.CCoeffNormed);

            // Group results by location, pick the best match, remove the others close to it, next
            var targets = new List<(int x, int y)>();
            using var work = result.Clone();
            while (true)
            {
                // Best match
                Cv2.MinMaxLoc(work, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                if (maxVal < cfg.MatchThreshold)
                    break;

                // Flag the location to be clicked
                targets.Add((maxLoc.X + arm / 2, maxLoc.Y + arm / 2));

                // Remove the others around it
                int sx = Math.Max(0, maxLoc.X - arm / 2);
                int sy = Math.Max(0, maxLoc.Y - arm / 2);
                int ex = Math.Min(work.Cols - 1, maxLoc.X + arm / 2);
                int ey = Math.Min(work.Rows - 1, maxLoc.Y + arm / 2);
                Cv2.Rectangle(work, new OpenCvSharp.Point(sx, sy), new OpenCvSharp.Point(ex, ey), Scalar.All(0.0), -1);
            }

            return targets;
        }

        private async Task FillRoutine(CancellationToken token)
        {
            try
            {
                Status = ServiceStatus.WORKING;

                var cfg = Config;
                // Read only the left-half of the screen, where the stash is
                var region = new Rectangle(0, 0, _config.Screen.Width / 2, _config.Screen.Height);

                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    using var mat = OpenCVUtils.EnsureBgr(ScreenUtils.CaptureScreen(region));
                    // Detect the corners in the image
                    var targets = FindCorners(mat, cfg);
                    _logger.LogDebug("FillInventory: {Count} corner(s) detected", targets.Count);

                    if (targets.Count == 0)
                        break;

                    try
                    {
                        _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
                        foreach (var (x, y) in targets)
                        {
                            token.ThrowIfCancellationRequested();
                            await SimulationUtils.MouseMoveAndClickAsync(
                                (short)x,
                                (short)y,
                                left: true,
                                moveDurationMs: 30,
                                token
                            );
                            await Task.Delay(cfg.ClickDelayMs, token);
                        }
                    }
                    finally
                    {
                        _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
                    }
                    // Loop to re-check we didn't miss a corner on the current pass
                }

                _logger.LogInformation("FillInventory completed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("FillInventory cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in FillRoutine");
            }
            finally
            {
                Status = ServiceStatus.STOPPED;
            }
        }
    }

    public interface IFillInventoryService : IServiceWithStatus, IToggleableService
    {
        FillInventoryConfig Config { get; }

        void UpdateConfig(int clickDelayMs);
    }
}
