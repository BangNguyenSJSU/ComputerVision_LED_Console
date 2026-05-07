using System.Text.Json;
using System.Text.Json.Serialization;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;

namespace ComputerVision_LED_Console.Tests.Config;

public class MarkerSnapshotColorRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Snapshot_WithMixedColors_RoundTripsThroughJson()
    {
        var snapshot = new ConfigSnapshot();
        snapshot.Markers.Add(new MarkerSnapshot { Id = 1, CenterX = 10, CenterY = 20, Radius = 5, OnThreshold = 12, OffThreshold = 6, Color = LedColor.Red });
        snapshot.Markers.Add(new MarkerSnapshot { Id = 2, CenterX = 30, CenterY = 40, Radius = 5, OnThreshold = 12, OffThreshold = 6, Color = LedColor.Yellow });
        snapshot.Markers.Add(new MarkerSnapshot { Id = 3, CenterX = 50, CenterY = 60, Radius = 5, OnThreshold = 12, OffThreshold = 6, Color = LedColor.Green });

        string json = JsonSerializer.Serialize(snapshot, Options);
        var loaded = JsonSerializer.Deserialize<ConfigSnapshot>(json, Options);

        Assert.NotNull(loaded);
        Assert.Equal(3, loaded!.Markers.Count);
        Assert.Equal(LedColor.Red, loaded.Markers[0].Color);
        Assert.Equal(LedColor.Yellow, loaded.Markers[1].Color);
        Assert.Equal(LedColor.Green, loaded.Markers[2].Color);
    }

    [Fact]
    public void Snapshot_WithoutColorField_DefaultsToUnknown()
    {
        // Simulates loading a config file written by an older build that has no "color" key.
        string legacyJson = """
        {
          "version": 1,
          "app": { "camera": {}, "detection": {}, "network": {} },
          "markers": [
            { "id": 1, "centerX": 10, "centerY": 20, "radius": 5, "onThreshold": 12, "offThreshold": 6 }
          ]
        }
        """;

        var loaded = JsonSerializer.Deserialize<ConfigSnapshot>(legacyJson, Options);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Markers);
        Assert.Equal(LedColor.Unknown, loaded.Markers[0].Color);
    }
}
