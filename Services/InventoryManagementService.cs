using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jido.Config;
using Jido.Utils;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SharpHook.Data;

namespace Jido.Services
{
    public class InventoryManagementService : BaseToggleableService, IInventoryManagementService
    {

        // How similar a cell must be to the empty reference to be considered empty.
        private const double EmptyCheckRmsThreshold = 40.0;

        // How different a cell must be from the initial snapshot to be considered "changed" since
        // the routine started
        private const double ChangeCheckRmsThreshold = 15.0;

        private readonly ILogger<InventoryManagementService> _logger;

        // Empty-inventory reference image (always 3-channel BGR), guarded by _referenceLock.
        private readonly object _referenceLock = new();

        private Mat? _emptyReference;

        private string EmptyReferencePath => AppPaths.EmptyInventoryReferenceFile;

        public InventoryManagementConfig Config => _config.Features.InventoryManagement;

        public InventoryManagementService(
            IHooksManager keyHooksManager,
            JidoConfig config,
            IMacroService macroService,
            IServiceHub serviceHub,
            ILogger<InventoryManagementService> logger
        )
            : base(
                keyHooksManager,
                config,
                macroService,
                config.Features.InventoryManagement.EmptyInventoryKey,
                serviceHub,
                ServiceNames.InventoryManagement
            )
        {
            _logger = logger;
            LoadReferenceFromDisk();
        }

        private void LoadReferenceFromDisk()
        {
            if (File.Exists(EmptyReferencePath))
            {
                // Get the saved empty inventory image
                lock (_referenceLock)
                {
                    _emptyReference?.Dispose();
                    _emptyReference = Cv2.ImRead(EmptyReferencePath, ImreadModes.Color);
                    _logger.LogDebug(
                        "Empty reference loaded from disk ({W}x{H}, {Ch}ch)",
                        _emptyReference.Width,
                        _emptyReference.Height,
                        _emptyReference.Channels()
                    );
                }
            }
        }

        public void UpdateConfig(InventoryManagementConfig config)
        {
            // Preserve the current toggle key — key changes must go through ChangeToggleKey().
            config.EmptyInventoryKey = ToggleKey;
            _config.Features.InventoryManagement = config;
            _config.Persist();
        }

        // Save a screenshot of the (hopefully) empty inventory to use as reference
        public void CaptureAndSaveEmptyReference()
        {
            var cfg = Config;
            var region = new System.Drawing.Rectangle(
                cfg.InventoryPosition[0],
                cfg.InventoryPosition[1],
                cfg.InventoryWidth,
                cfg.InventoryHeight
            );

            // No `using` — EnsureBgr either returns raw unchanged (3-ch) or disposes it and
            // returns a new Mat. Either way bgr is stored long-term in _emptyReference.
            var raw = ScreenUtils.CaptureScreen(region);

            // Normalise to 3-channel BGR to match what Cv2.ImRead returns loading the file afterwards
            var bgr = OpenCVUtils.EnsureBgr(raw);

            Cv2.ImWrite(EmptyReferencePath, bgr);
            _logger.LogDebug(
                "Empty inventory reference saved ({W}x{H}, {Ch}ch, at {X},{Y})",
                bgr.Width,
                bgr.Height,
                bgr.Channels(),
                cfg.InventoryPosition[0],
                cfg.InventoryPosition[1]
            );

            lock (_referenceLock)
            {
                _emptyReference?.Dispose();
                _emptyReference = bgr;
            }
        }

        protected override string[] ExclusiveWith => ServiceGroups.InventoryFeatures;

        public override void Toggle() => ToggleOneShotRoutine(EmptyInventoryRoutine);

        protected override void PersistToggleCombo(KeyCombo combo) =>
            _config.Features.InventoryManagement.EmptyInventoryKey = combo;

        public override void Dispose()
        {
            lock (_referenceLock)
            {
                _emptyReference?.Dispose();
                _emptyReference = null;
            }
            base.Dispose();
        }

        // Difference between two mats (to check if they are similar enough)
        private static double CellRms(Mat a, Mat b) =>
            Cv2.Norm(a, b, NormTypes.L2) / Math.Sqrt(a.Rows * a.Cols * a.Channels());

        private async Task EmptyInventoryRoutine(CancellationToken cancellationToken)
        {
            try
            {
                Status = ServiceStatus.WORKING;

                var cfg = Config;
                var inventoryRegion = new System.Drawing.Rectangle(
                    cfg.InventoryPosition[0],
                    cfg.InventoryPosition[1],
                    cfg.InventoryWidth,
                    cfg.InventoryHeight
                );

                int cellW = cfg.InventoryWidth / InventoryManagementConfig.GridWidth;
                int cellH = cfg.InventoryHeight / InventoryManagementConfig.GridHeight;

                // Clone the reference for thread-safety
                Mat? reference;
                lock (_referenceLock)
                    reference = _emptyReference?.Clone();

                // Check that we have a correct empty inventory reference
                bool hasReference =
                    reference != null
                    && !reference.Empty()
                    && reference.Width == cfg.InventoryWidth
                    && reference.Height == cfg.InventoryHeight
                    && reference.Channels() == 3;

                _logger.LogInformation(
                    "EmptyInventory started — hasReference={HasRef} ({W}x{H})",
                    hasReference,
                    reference?.Width ?? 0,
                    reference?.Height ?? 0
                );

                if (!hasReference)
                    _logger.LogWarning("No valid empty reference found.");

                using var captureBitmap = new Bitmap(
                    cfg.InventoryWidth,
                    cfg.InventoryHeight,
                    PixelFormat.Format32bppRgb
                );

                // Get an initial screenshot to see when inv. slots content changes (generally after
                // it was clicked)
                using Mat initialMat = OpenCVUtils.EnsureBgr(ScreenUtils.CaptureScreen(inventoryRegion, captureBitmap));
                // Remember which slot we already clicked
                var clicked = new bool[InventoryManagementConfig.GridWidth, InventoryManagementConfig.GridHeight];

                try
                {
                    // Press CTRL at the beginning
                    SimulationUtils.Simulator.SimulateKeyPress(KeyCode.VcLeftControl);
                    for (int row = 0; row < InventoryManagementConfig.GridHeight; row++)
                    {
                        for (int col = 0; col < InventoryManagementConfig.GridWidth; col++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            // Was configured as not clickable ?
                            bool isProtected =
                                cfg.InventorySlots != null
                                && col < cfg.InventorySlots.Length
                                && cfg.InventorySlots[col] != null
                                && row < cfg.InventorySlots[col].Length
                                && cfg.InventorySlots[col][row];

                            if (isProtected || clicked[col, row])
                                continue;

                            // Avoid looking too close to the borders of the slot (apply a 20% margin)
                            int marginX = cellW / 5;
                            int marginY = cellH / 5;
                            var cellRect = new OpenCvSharp.Rect(
                                col * cellW + marginX,
                                row * cellH + marginY,
                                cellW - 2 * marginX,
                                cellH - 2 * marginY
                            );
                            using var initialCell = new Mat(initialMat, cellRect);

                            if (hasReference)
                            {
                                // Compare slot to empty inv. reference to see if it was empty from
                                // the beginning
                                using var referenceCell = new Mat(reference!, cellRect);
                                double emptyRms = CellRms(initialCell, referenceCell);
                                if (emptyRms <= EmptyCheckRmsThreshold)
                                {
                                    // Skip it
                                    _logger.LogDebug(
                                        "Cell ({Col},{Row}): empty at start (RMS={Rms:F1} ≤ {Thr}), skipping",
                                        col,
                                        row,
                                        emptyRms,
                                        EmptyCheckRmsThreshold
                                    );
                                    continue;
                                }
                            }

                            // Actualize the content of the slot
                            using Mat currentRaw = ScreenUtils.CaptureScreen(inventoryRegion, captureBitmap);
                            using Mat currentMat = OpenCVUtils.EnsureBgr(currentRaw);
                            using var currentCell = new Mat(currentMat, cellRect);
                            double changeRms = CellRms(currentCell, initialCell);
                            if (changeRms > ChangeCheckRmsThreshold)
                            {
                                // Slot content changed since the start of the routine It may have
                                // been part of an already clicked multi-slot object, etc... => skip it
                                _logger.LogDebug(
                                    "Cell ({Col},{Row}): changed from initial (RMS={Rms:F1} > {Thr}), skipping",
                                    col,
                                    row,
                                    changeRms,
                                    ChangeCheckRmsThreshold
                                );
                                continue;
                            }

                            // Finally, a slot that we can click !
                            int screenX = cfg.InventoryPosition[0] + col * cellW + cellW / 2;
                            int screenY = cfg.InventoryPosition[1] + row * cellH + cellH / 2;

                            _logger.LogDebug(
                                "Cell ({Col},{Row}): clicking (changeRms={ChangeRms:F1})",
                                col,
                                row,
                                changeRms
                            );

                            await SimulationUtils.MouseMoveAndClickAsync(
                                (short)screenX,
                                (short)screenY,
                                left: true,
                                moveDurationMs: 30,
                                cancellationToken
                            );

                            clicked[col, row] = true;
                            await Task.Delay(cfg.ClickDelayMs, cancellationToken);
                        }
                    }
                }
                finally
                {
                    // Always release the ctrl key
                    SimulationUtils.Simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
                    reference?.Dispose();
                }

                _logger.LogInformation("EmptyInventory completed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("EmptyInventory cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in EmptyInventoryRoutine");
            }
            finally
            {
                Status = ServiceStatus.STOPPED;
            }
        }
    }

    public interface IInventoryManagementService : IServiceWithStatus, IToggleableService
    {
        InventoryManagementConfig Config { get; }

        void UpdateConfig(InventoryManagementConfig config);

        void CaptureAndSaveEmptyReference();
    }
}
