using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Jido.Config;
using Jido.Utils;
using Microsoft.Extensions.Logging;
using SharpHook.Data;

namespace Jido.Services
{
    public class FillInventoryService : BaseToggleableService, IFillInventoryService
    {
        private readonly ILogger<FillInventoryService> _logger;

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

        protected override string[] ExclusiveWith => ServiceGroups.InventoryFeatures;

        public override void Toggle() => ToggleOneShotRoutine(FillRoutine);

        protected override void PersistToggleCombo(KeyCombo combo) => _config.Features.FillInventory.ToggleKey = combo;

        private async Task FillRoutine(CancellationToken token)
        {
            try
            {
                Status = ServiceStatus.WORKING;

                var cfg = Config;
                // Read only the left-half of the screen, where the stash is
                var region = new Rectangle(0, 0, ScreenUtils.PrimaryWidth / 2, ScreenUtils.PrimaryHeight);

                const int MaxPasses = 20;
                int pass = 0;
                while (pass++ < MaxPasses)
                {
                    token.ThrowIfCancellationRequested();

                    using var raw = ScreenUtils.CaptureScreen(region);
                    using var mat = OpenCVUtils.ToBgr(raw);
                    // Detect the corners in the image
                    var targets = OpenCVUtils.FindCorners(mat, cfg);
                    _logger.LogDebug("FillInventory: {Count} corner(s) detected (pass {Pass})", targets.Count, pass);

                    if (targets.Count == 0)
                        break;

                    try
                    {
                        SimulationUtils.Simulator.SimulateKeyPress(KeyCode.VcLeftControl);
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
                        SimulationUtils.Simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
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
                Status = ServiceStatus.ERROR;
            }
            finally
            {
                if (Status != ServiceStatus.ERROR)
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
