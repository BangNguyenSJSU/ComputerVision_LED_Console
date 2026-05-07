using System.Text.Json;
using System.Text.Json.Serialization;
using ComputerVision_LED_Console.Config;

namespace ComputerVision_LED_Console.Tests.Config;

public class SecurityConfigPersistenceTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void SecurityFields_RoundTripThroughAppConfigJson()
    {
        var cfg = new AppConfig();
        cfg.Security.LockEnabled = true;
        cfg.Security.PasswordHash = "abc123hash==";
        cfg.Security.PasswordSalt = "saltsalt==";
        cfg.Security.PasswordIterations = 50_000;

        string json = JsonSerializer.Serialize(cfg, Options);
        var loaded = JsonSerializer.Deserialize<AppConfig>(json, Options);

        Assert.NotNull(loaded);
        Assert.True(loaded!.Security.LockEnabled);
        Assert.Equal("abc123hash==", loaded.Security.PasswordHash);
        Assert.Equal("saltsalt==", loaded.Security.PasswordSalt);
        Assert.Equal(50_000, loaded.Security.PasswordIterations);
    }

    [Fact]
    public void LegacyJsonWithoutSecurity_DefaultsToConfiguredButUnlocked()
    {
        // Older config files written before SecurityConfig existed must still parse cleanly.
        string legacyJson = """
        {
          "camera": {},
          "detection": {},
          "network": {}
        }
        """;

        var loaded = JsonSerializer.Deserialize<AppConfig>(legacyJson, Options);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.Security);
        Assert.True(loaded.Security.LockEnabled);
        Assert.Equal("", loaded.Security.PasswordHash);
        Assert.Equal("", loaded.Security.PasswordSalt);
    }
}
