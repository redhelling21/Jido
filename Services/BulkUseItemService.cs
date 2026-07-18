using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Jido.Config;
using Jido.Utils;
using Microsoft.Extensions.Logging;
using SharpHook;
using SharpHook.Data;

namespace Jido.Services
{
    public class BulkUseItemService : BaseToggleableService, IBulkUseItemService
    {
        private static readonly EventSimulator _simulator = new EventSimulator();
        private readonly ILogger<BulkUseItemService> _logger;

        public BulkUseItemConfig Config => _config.Features.BulkUseItem;

        public BulkUseItemService(
            IHooksManager keyHooksManager,
            JidoConfig config,
            IMacroService macroService,
            IServiceHub serviceHub,
            ILogger<BulkUseItemService> logger
        )
            : base(
                keyHooksManager,
                config,
                macroService,
                config.Features.BulkUseItem.ToggleKey,
                serviceHub,
                ServiceNames.BulkUseItem
            )
        {
            _logger = logger;
        }

        public void UpdateConfig(int clickDelayMs)
        {
            _config.Features.BulkUseItem.ClickDelayMs = clickDelayMs;
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

            if (
                _serviceHub.IsActive(ServiceNames.FillInventory)
                || _serviceHub.IsActive(ServiceNames.InventoryManagement)
            )
                return;

            _ = Task.Run(() => BulkUseRoutine(ResetCts().Token));
        }

        protected override void StopRoutine()
        {
            CancelCts();
            Status = ServiceStatus.STOPPED;
        }

        protected override void PersistToggleCombo(KeyCombo combo) => _config.Features.BulkUseItem.ToggleKey = combo;

        public override void Dispose()
        {
            base.Dispose();
        }

        private async Task BulkUseRoutine(CancellationToken token)
        {
            try
            {
                Status = ServiceStatus.WORKING;

                var cfg = Config;
                var fillCfg = _config.Features.FillInventory;
                var region = new Rectangle(0, 0, ScreenUtils.PrimaryWidth / 2, ScreenUtils.PrimaryHeight);

                try
                {
                    // Shift + right-click at current mouse position
                    _simulator.SimulateKeyPress(KeyCode.VcLeftShift);
                    _simulator.SimulateMousePress(MouseButton.Button2);
                    await Task.Delay(50, token);
                    _simulator.SimulateMouseRelease(MouseButton.Button2);

                    token.ThrowIfCancellationRequested();

                    using var mat = OpenCVUtils.EnsureBgr(ScreenUtils.CaptureScreen(region));
                    var targets = OpenCVUtils.FindCorners(mat, fillCfg);

                    _logger.LogInformation("BulkUseItem: {Count} corner(s) detected", targets.Count);

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
                    _simulator.SimulateKeyRelease(KeyCode.VcLeftShift);
                }

                _logger.LogInformation("BulkUseItem completed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("BulkUseItem cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in BulkUseRoutine");
            }
            finally
            {
                Status = ServiceStatus.STOPPED;
            }
        }
    }

    public interface IBulkUseItemService : IServiceWithStatus, IToggleableService
    {
        BulkUseItemConfig Config { get; }

        void UpdateConfig(int clickDelayMs);
    }
}
