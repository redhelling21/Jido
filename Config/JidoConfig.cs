using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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
    private string PersistentFileLocation { get; set; } =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

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
        string appRootFolder = AppDomain.CurrentDomain.BaseDirectory;
        PersistentFileLocation = Path.Combine(appRootFolder, filename);
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
                        if (property.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                            property.SetValue(this, property.GetValue(config));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load config from {Path}, using defaults.", PersistentFileLocation);
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
            File.WriteAllText(PersistentFileLocation, json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save config to {Path}.", PersistentFileLocation);
        }
    }

    /// <summary>
    /// Persist `this` in a file asynchronously
    /// </summary>
    public async Task PersistAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, _serializerOptions);
            await File.WriteAllTextAsync(PersistentFileLocation, json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save config to {Path}.", PersistentFileLocation);
        }
    }

    #endregion Methods

    #region Properties

    public ScreenConfig Screen { get; set; } = new();
    public FeaturesConfig Features { get; set; } = new();

    public KeyCombo ToggleKey { get; set; } = new(SharpHook.Data.KeyCode.VcF7);

    #endregion Properties
}

public class ScreenConfig
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
}

public class FeaturesConfig
{
    public AutolootConfig Autoloot { get; set; } = new();
    public AutopressConfig Autopress { get; set; } = new();
    public InventoryManagementConfig InventoryManagement { get; set; } = new();
    public FillInventoryConfig FillInventory { get; set; } = new();
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

public class FillInventoryConfig
{
    public KeyCombo ToggleKey { get; set; } = new(KeyCode.VcF2);
    public int[] LineColor { get; set; } = [231, 180, 119]; // RGB
    public int ColorTolerance { get; set; } = 3;

    // Corner shape configuration
    public int ArmLengthPx { get; set; } = 10; // length of each arm of the ⌟

    public int LineThicknessPx { get; set; } = 1; // thickness of the corner lines
    public float MatchThreshold { get; set; } = 0.7f; // minimum matching score
    public int ClickDelayMs { get; set; } = 80;
}

public class InventoryManagementConfig
{
    public const int GridWidth = 12;
    public const int GridHeight = 5;

    public int InventoryWidth { get; set; } = 600;
    public int InventoryHeight { get; set; } = 250;
    public int[] InventoryPosition { get; set; } = { 1000, 1000 };
    public bool[][] InventorySlots { get; set; } = new bool[GridWidth][];
    public KeyCombo EmptyInventoryKey { get; set; } = new(SharpHook.Data.KeyCode.VcF4);
    public int ClickDelayMs { get; set; } = 80;
}
