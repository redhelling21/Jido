using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Avalonia.Styling;
using Jido.Models;
using Jido.Utils;
using Microsoft.Extensions.Logging;
using SharpHook.Data;
using static Jido.Models.CompositeHighLevelCommand;

namespace Jido.Config;

public class JidoConfig
{
    [JsonIgnore]
    private readonly JsonSerializerOptions? _serializerOptions;

    [JsonIgnore]
    private readonly ILogger<JidoConfig>? _logger;

    [JsonIgnore]
    private string PersistentFileLocation { get; set; } = AppPaths.SettingsFile;

    public JidoConfig()
    { }

    public JidoConfig(string filename, ILogger<JidoConfig>? logger = null)
    {
        _logger = logger;
        _serializerOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        PersistentFileLocation = Path.Combine(AppPaths.DataFolder, filename);
        if (string.IsNullOrWhiteSpace(PersistentFileLocation))
            throw new ArgumentException("File path cannot be null or empty.", nameof(PersistentFileLocation));

        if (File.Exists(PersistentFileLocation))
        {
            try
            {
                var json = File.ReadAllText(PersistentFileLocation);
                var config = JsonSerializer.Deserialize<JidoConfig>(json, _serializerOptions);
                if (config != null)
                {
                    foreach (PropertyInfo property in typeof(JidoConfig).GetProperties())
                    {
                        if (property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                            continue;
                        var value = property.GetValue(config);
                        if (value != null)
                            property.SetValue(this, value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load config from {Path}, using defaults.", PersistentFileLocation);
                BackupUnreadableConfig();
            }
        }
        else
        {
            Persist();
        }
    }

    #region Methods

    /// <summary>
    /// Persist `this` in a file
    /// </summary>
    public void Persist()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, _serializerOptions);
            WriteAtomic(PersistentFileLocation, json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save config to {Path}.", PersistentFileLocation);
        }
    }

    /// <summary>
    /// Copies an unreadable settings file aside so the user can recover hand-edited values.
    /// </summary>
    private void BackupUnreadableConfig()
    {
        try
        {
            var backup = PersistentFileLocation + ".corrupted";
            File.Copy(PersistentFileLocation, backup, overwrite: true);
            _logger?.LogWarning("Unreadable config backed up to {Path} before falling back to defaults.", backup);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to back up unreadable config at {Path}.", PersistentFileLocation);
        }
    }

    /// <summary>
    /// Write <paramref name="contents"/> to <paramref name="path"/> without ever leaving the
    /// destination in a partially-written state.
    /// </summary>
    private static void WriteAtomic(string path, string contents)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, contents);
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && attempt < maxAttempts)
            {
                Thread.Sleep(20 * attempt);
            }
        }
    }

    #endregion Methods

    #region Properties

    // Screen dimensions used to be stored here. They are now detected at runtime via
    // ScreenUtils.PrimaryWidth/PrimaryHeight — a stale hand-entered value silently mis-aimed every
    // capture region. An obsolete "screen" key in an existing settings.json is simply ignored.
    public FeaturesConfig Features { get; set; } = new();

    public KeyCombo ToggleKey { get; set; } = new(SharpHook.Data.KeyCode.VcF7);

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppTheme Theme { get; set; } = AppTheme.Auto;

    #endregion Properties
}

public enum AppTheme
{
    Auto,
    Light,
    Dark
}

public static class AppThemeExtensions
{
    public static ThemeVariant ToVariant(this AppTheme theme) =>
        theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default // Auto: follow the OS
        };
}

public class FeaturesConfig
{
    public AutolootConfig Autoloot { get; set; } = new();
    public AutopressConfig Autopress { get; set; } = new();
    public InventoryManagementConfig InventoryManagement { get; set; } = new();
    public FillInventoryConfig FillInventory { get; set; } = new();
    public BulkUseItemConfig BulkUseItem { get; set; } = new();
}

public class AutolootConfig
{
    public KeyCombo ToggleKey { get; set; } = new(KeyCode.VcF3);

    public List<Color> Colors { get; set; } =
        new List<Color>()
        {
            new Color() { Name = "Default", RGB = [253, 0, 253] }
        };

    // Minimum size of the rectangle
    public int MinArea { get; set; } = 2000;

    // Maximum deviation from straight lines
    public int Epsilon { get; set; } = 2;

    public int Threshold { get; set; } = 90;
    public double MaxAspectRatio { get; set; } = 2000.0;
    public int CycleDelayMs { get; set; } = 333;
    public double CaptureRatio { get; set; } = 0.5;
}

public class AutopressConfig
{
    public KeyCombo ToggleKey { get; set; } = new(KeyCode.VcQ);
    public int ClickDelay { get; set; } = 1200;
    public double IntervalRandomizationRatio { get; set; } = 0.1;
    public List<HighLevelCommand> ScheduledCommands { get; set; } = new();
    public List<ConstantCommand> ConstantCommands { get; set; } = new();
}

public class AutopressBuild
{
    public string Name { get; set; } = string.Empty;
    public AutopressConfig Config { get; set; } = new();
}

public class FillInventoryConfig
{
    public KeyCombo ToggleKey { get; set; } = new(KeyCode.VcF2, ctrl: true);
    public int[] LineColor { get; set; } = [231, 180, 119]; // RGB
    public int ColorTolerance { get; set; } = 3;

    // Corner shape configuration
    public int ArmLengthPx { get; set; } = 10; // length of each arm of the ⌟

    public int LineThicknessPx { get; set; } = 1; // thickness of the corner lines
    public float MatchThreshold { get; set; } = 0.7f; // minimum matching score
    public int ClickDelayMs { get; set; } = 80;
}

public class BulkUseItemConfig
{
    public KeyCombo ToggleKey { get; set; } = new(KeyCode.VcF2, ctrl: true, shift: true);
    public int ClickDelayMs { get; set; } = 120;
}

public class InventoryManagementConfig
{
    public const int GridWidth = 12;
    public const int GridHeight = 5;

    public int InventoryWidth { get; set; } = 600;
    public int InventoryHeight { get; set; } = 250;
    public int[] InventoryPosition { get; set; } = { 1000, 1000 };
    public bool[][] InventorySlots { get; set; } = new bool[GridWidth][];
    public KeyCombo EmptyInventoryKey { get; set; } = new(KeyCode.VcF2);
    public int ClickDelayMs { get; set; } = 80;
}
