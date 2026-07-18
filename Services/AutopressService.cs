using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Jido.Config;
using Jido.Models;
using Jido.Utils;
using Microsoft.Extensions.Logging;
using SharpHook;
using SharpHook.Data;

namespace Jido.Services
{
    public class AutopressService : BaseToggleableService, IAutopressService
    {
        private static readonly string _buildsFolder =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autopress-builds");

        private static readonly JsonSerializerOptions _buildSerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly ILogger<AutopressService> _logger;
        private EventSimulator _eventSimulator = new EventSimulator();
        private readonly System.Timers.Timer _suspendTimer = new() { AutoReset = false };
        private readonly ConcurrentQueue<LowLevelCommand> _queuedCommands = new();
        private readonly List<AutopressBuild> _builds = new();
        public List<HighLevelCommand> ScheduledCommands { get; private set; }
        public List<ConstantCommand> ConstantCommands { get; private set; }
        public int ClickDelay { get; private set; }
        public double IntervalRandomizationRatio { get; private set; }

        public AutopressService(
            IHooksManager keyHooksManager,
            JidoConfig config,
            IMacroService macroService,
            IServiceHub serviceHub,
            ILogger<AutopressService> logger
        )
            : base(
                keyHooksManager,
                config,
                macroService,
                config.Features.Autopress.ToggleKey,
                serviceHub,
                ServiceNames.Autopress
            )
        {
            _logger = logger;
            InitFromConfig();
            LoadBuildsFromDisk();
            _suspendTimer.Elapsed += OnSuspendTimerElapsed;
            _keyHooksManager.RegisterMouseClick(MouseButton.Button1, SuspendAutoPress);
        }

        private void LoadBuildsFromDisk()
        {
            if (!Directory.Exists(_buildsFolder)) return;
            foreach (var file in Directory.GetFiles(_buildsFolder, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var buildConfig = JsonSerializer.Deserialize<AutopressConfig>(json, _buildSerializerOptions);
                    if (buildConfig != null)
                        _builds.Add(new AutopressBuild { Name = Path.GetFileNameWithoutExtension(file), Config = buildConfig });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load autopress build from {Path}.", file);
                }
            }
        }

        private void InitFromConfig()
        {
            ScheduledCommands = _config.Features.Autopress.ScheduledCommands;
            ConstantCommands = _config.Features.Autopress.ConstantCommands;
            ClickDelay = _config.Features.Autopress.ClickDelay;
            IntervalRandomizationRatio = _config.Features.Autopress.IntervalRandomizationRatio;
        }

        public override void Toggle()
        {
            if (_macroService.Status == ServiceStatus.STOPPED)
                return;

            if (Status == ServiceStatus.STOPPED)
            {
                _logger.LogInformation("Autopress started");
                StartRoutine();
            }
            else
            {
                _logger.LogInformation("Autopress stopped");
                // Stop the suspend timer first so OnSuspendTimerElapsed cannot race and restart the routine
                _suspendTimer.Stop();
                StopRoutine();
                if (Status != ServiceStatus.STOPPED)
                    Status = ServiceStatus.STOPPED;
            }
        }

        public void UpdateConfig(AutopressConfig config)
        {
            if (Status != ServiceStatus.STOPPED)
                StopRoutine();
            var replaced = ScheduledCommands;
            config.ToggleKey = ToggleKey;
            _config.Features.Autopress = config;
            _config.Persist();
            InitFromConfig();
            // If the commands were replaced, dispose the previous ones
            if (replaced is not null && !ReferenceEquals(replaced, ScheduledCommands))
                foreach (var cmd in replaced)
                    cmd.Dispose();
        }

        public IReadOnlyList<AutopressBuild> Builds => _builds.AsReadOnly();

        public void SaveBuild(string name, AutopressConfig config)
        {
            try
            {
                Directory.CreateDirectory(_buildsFolder);
                var path = Path.Combine(_buildsFolder, $"{name}.json");
                File.WriteAllText(path, JsonSerializer.Serialize(config, _buildSerializerOptions));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save autopress build '{Name}'.", name);
                return;
            }
            var existing = _builds.FirstOrDefault(b => b.Name == name);
            if (existing != null)
                existing.Config = config;
            else
                _builds.Add(new AutopressBuild { Name = name, Config = config });
        }

        public void LoadBuild(string name)
        {
            var build = _builds.FirstOrDefault(b => b.Name == name);
            if (build is null) return;
            var copy = DeepCopy(build.Config);
            if (copy is null) return;
            UpdateConfig(copy);
        }

        /// <summary>
        /// Round-trips a config through JSON to produce a fully independent object graph
        /// </summary>
        private AutopressConfig? DeepCopy(AutopressConfig source)
        {
            try
            {
                var json = JsonSerializer.Serialize(source, _buildSerializerOptions);
                return JsonSerializer.Deserialize<AutopressConfig>(json, _buildSerializerOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy autopress build configuration.");
                return null;
            }
        }

        public void DeleteBuild(string name)
        {
            var build = _builds.FirstOrDefault(b => b.Name == name);
            if (build is null) return;
            try
            {
                var path = Path.Combine(_buildsFolder, $"{name}.json");
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete autopress build '{Name}'.", name);
                return;
            }
            _builds.Remove(build);
        }

        public void SuspendAutoPress(object? sender, EventArgs e)
        {
            if (Status is ServiceStatus.IDLE or ServiceStatus.WORKING)
            {
                StopRoutine();
                Status = ServiceStatus.PAUSED;
                _suspendTimer.Interval = ClickDelay;
                _suspendTimer.Start();
            }
            else if (Status == ServiceStatus.PAUSED)
            {
                // User clicked again before the resume timer fired — reset the delay
                _suspendTimer.Stop();
                _suspendTimer.Interval = ClickDelay;
                _suspendTimer.Start();
            }
        }

        private void OnSuspendTimerElapsed(object? sender, EventArgs e)
        {
            // Guard against the case where Toggle() or the macro stop fired during the suspension
            // window, both of which set Status away from PAUSED.
            if (Status == ServiceStatus.PAUSED)
            {
                if (_macroService.Status != ServiceStatus.STOPPED)
                    StartRoutine();
                else
                    Status = ServiceStatus.STOPPED;
            }
        }

        private void StartRoutine()
        {
            var token = ResetCts().Token;
            foreach (var cmd in ConstantCommands)
                _eventSimulator.SimulateKeyPress(cmd.KeyToPress);

            _ = Task.Run(() => KeyPressRoutine(token));

            // Each command enqueues once immediately on Start(), then continues on its own timer.
            // KeyPressRoutine consumes the shared queue and handles the actual key simulation.
            foreach (var cmd in ScheduledCommands)
                cmd.Start(_queuedCommands, IntervalRandomizationRatio);
            Status = ServiceStatus.IDLE;
        }

        protected override void StopRoutine()
        {
            // Guard against double-stop: called from Toggle, SuspendAutoPress, UpdateConfig, and
            // the base class macro-stop handler — any of which may race with each other.
            // Read _cts once: a concurrent ResetCts can swap it between the check and the Cancel.
            var cts = Volatile.Read(ref _cts);
            if (cts is null || cts.IsCancellationRequested)
                return;
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                return; 
            }

            _suspendTimer.Stop();

            foreach (var cmd in ConstantCommands)
                _eventSimulator.SimulateKeyRelease(cmd.KeyToPress);

            foreach (var cmd in ScheduledCommands)
                cmd.Stop();

            // Drain stale commands so they don't fire on the next StartRoutine.
            while (_queuedCommands.TryDequeue(out _)) { }

            Status = ServiceStatus.STOPPED;
        }

        private async Task KeyPressRoutine(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_queuedCommands.TryDequeue(out var command))
                    {
                        if (command is WaitCommand wait)
                            await Task.Delay(wait.WaitTimeInMs, cancellationToken);
                        else if (command is PressCommand press)
                        {
                            await Task.Delay(300, cancellationToken);
                            _eventSimulator.SimulateKeyPress(press.KeyToPress);
                            // finally guarantees release even if cancellation fires mid-hold,
                            // preventing keys from getting stuck in a pressed state.
                            try
                            {
                                await Task.Delay(press.PressDurationInMs, cancellationToken);
                            }
                            finally
                            {
                                _eventSimulator.SimulateKeyRelease(press.KeyToPress);
                            }
                        }
                    }
                    else
                        await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in KeyPressRoutine; stopping service.");
                StopRoutine();
            }
        }

        protected override void PersistToggleCombo(KeyCombo combo) => _config.Features.Autopress.ToggleKey = combo;

        public override void Dispose()
        {
            _keyHooksManager.UnRegisterMouseClick(MouseButton.Button1, SuspendAutoPress);
            _suspendTimer?.Dispose();

            foreach (var cmd in ScheduledCommands)
                cmd.Dispose();
            foreach (var build in _builds)
                foreach (var cmd in build.Config.ScheduledCommands)
                    cmd.Dispose();

            base.Dispose();
        }
    }

    public interface IAutopressService : IServiceWithStatus, IToggleableService
    {
        public List<HighLevelCommand> ScheduledCommands { get; }
        public List<ConstantCommand> ConstantCommands { get; }

        public int ClickDelay { get; }

        public double IntervalRandomizationRatio { get; }

        public void UpdateConfig(AutopressConfig config);

        IReadOnlyList<AutopressBuild> Builds { get; }
        void SaveBuild(string name, AutopressConfig config);
        void LoadBuild(string name);
        void DeleteBuild(string name);
    }
}
