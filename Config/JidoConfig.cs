using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Jido.Models;
using OpenCvSharp;
using SharpHook.Native;
using static Jido.Models.CompositeHighLevelCommand;

namespace Jido.Config;

public class JidoConfig
{
    [JsonIgnore]
    private readonly JsonSerializerOptions? _serializerOptions;

    [JsonIgnore]
    private string PersistentFileLocation { get; set; } =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public JidoConfig()
    { }

    public JidoConfig(string filename)
    {
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

        // Check if the file exists
        if (File.Exists(PersistentFileLocation))
        {
            using var sr = new StreamReader(PersistentFileLocation);
            var config = JsonSerializer.Deserialize<JidoConfig>(sr.ReadToEnd(), _serializerOptions);
            PropertyInfo[] properties = typeof(JidoConfig).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                if (property.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                {
                    property.SetValue(this, property.GetValue(config));
                }
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
        using var sw = new StreamWriter(PersistentFileLocation);
        var json = JsonSerializer.Serialize(this, _serializerOptions);
        sw.Write(json);
    }

    /// <summary>
    /// Persist `this` in a file asynchronously
    /// </summary>
    public async Task PersistAsync()
    {
        await using var sw = new StreamWriter(PersistentFileLocation);
        var json = JsonSerializer.Serialize(this, _serializerOptions);
        await sw.WriteAsync(json);
    }

    #endregion Methods

    #region Properties

    public ScreenConfig Screen { get; set; } = new();
    public FeaturesConfig Features { get; set; } = new();

    public KeyCode ToggleKey { get; set; } = KeyCode.VcF7;

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
}

public class AutolootConfig
{
    public KeyCode ToggleKey { get; set; } = KeyCode.VcF3;

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
    public double MaxClicksPerSecond { get; set; } = 5.0;
}

public class AutopressConfig
{
    public KeyCode ToggleKey { get; set; } = KeyCode.VcQ;
    public int ClickDelay { get; set; } = 1200;
    public double IntervalRandomizationRatio { get; set; } = 0.1;
    public List<HighLevelCommand> ScheduledCommands { get; set; } = new();
    public List<ConstantCommand> ConstantCommands { get; set; } = new();
}

public class InventoryManagementConfig
{
    public int InventoryWidth { get; set; } = 600;
    public int InventoryHeight { get; set; } = 250;
    public int[] InventoryPosition { get; set; } = { 1000, 1000 };
    public bool[][] InventorySlots { get; set; } = new bool[12][];
    public KeyCode EmptyInventoryKey { get; set; } = KeyCode.VcF4;
}
