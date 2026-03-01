using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharpHook.Data;

namespace Jido.Utils;

[JsonConverter(typeof(KeyComboJsonConverter))]
public readonly struct KeyCombo : IEquatable<KeyCombo>
{
    // Represents a normal key + modifiers
    public KeyCode Key { get; init; }

    public bool Ctrl { get; init; }
    public bool Alt { get; init; }
    public bool Shift { get; init; }

    public KeyCombo(KeyCode key, bool ctrl = false, bool alt = false, bool shift = false)
    {
        Key = key;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
    }

    public override string ToString()
    {
        var keyName = Key.ToString().StartsWith("Vc") ? Key.ToString()[2..] : Key.ToString();
        var parts = new List<string>();
        if (Ctrl)
            parts.Add("Ctrl");
        if (Alt)
            parts.Add("Alt");
        if (Shift)
            parts.Add("Shift");
        parts.Add(keyName);
        return string.Join("+", parts);
    }

    /// <summary>
    /// Parses strings like "Ctrl+Alt+F2", "F7", or legacy "VcF7" (backward-compatible).
    /// </summary>
    public static KeyCombo Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new KeyCombo(KeyCode.VcUndefined);

        var parts = value.Split('+');
        bool ctrl = false,
            alt = false,
            shift = false;
        KeyCode key = KeyCode.VcUndefined;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
            {
                ctrl = true;
                continue;
            }
            if (trimmed.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
                continue;
            }
            if (trimmed.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
                continue;
            }

            // "VcF7" (legacy) or "F7" → try as-is first, then prepend "Vc"
            if (Enum.TryParse<KeyCode>(trimmed, ignoreCase: true, out var direct))
            {
                key = direct;
                continue;
            }

            var candidate = "Vc" + char.ToUpper(trimmed[0]) + trimmed[1..];
            if (Enum.TryParse<KeyCode>(candidate, ignoreCase: true, out var prefixed))
                key = prefixed;
        }

        return new KeyCombo(key, ctrl, alt, shift);
    }

    public bool Equals(KeyCombo other) =>
        Key == other.Key && Ctrl == other.Ctrl && Alt == other.Alt && Shift == other.Shift;

    public override bool Equals(object? obj) => obj is KeyCombo other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Key, Ctrl, Alt, Shift);

    public static bool operator ==(KeyCombo left, KeyCombo right) => left.Equals(right);

    public static bool operator !=(KeyCombo left, KeyCombo right) => !left.Equals(right);
}

public class KeyComboJsonConverter : JsonConverter<KeyCombo>
{
    // HElper for JSON config conversion
    public override KeyCombo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return KeyCombo.Parse(reader.GetString() ?? string.Empty);
        throw new JsonException($"Unexpected token {reader.TokenType} for KeyCombo.");
    }

    public override void Write(Utf8JsonWriter writer, KeyCombo value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
