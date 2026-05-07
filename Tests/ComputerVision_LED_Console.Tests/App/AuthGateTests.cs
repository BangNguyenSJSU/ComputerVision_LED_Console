using System.Text.Json;
using System.Text.Json.Serialization;
using ComputerVision_LED_Console.App;
using ComputerVision_LED_Console.Config;

namespace ComputerVision_LED_Console.Tests.App;

public class AuthGateTests
{
    private static (AuthGate gate, SecurityConfig cfg, AppState state) MakeConfigured(string password)
    {
        var cfg = new SecurityConfig { LockEnabled = true, PasswordIterations = 10_000 };
        var state = new AppState();
        var bootstrap = new AuthGate(cfg, state);
        var (hash, salt) = bootstrap.HashNewPassword(password);
        cfg.PasswordHash = hash;
        cfg.PasswordSalt = salt;

        var freshState = new AppState();
        return (new AuthGate(cfg, freshState), cfg, freshState);
    }

    [Fact]
    public void Constructor_WithLockEnabledAndConfigured_StartsLocked()
    {
        var (gate, _, _) = MakeConfigured("hunter2");

        Assert.False(gate.IsUnlocked);
    }

    [Fact]
    public void Constructor_WithLockDisabled_StartsUnlocked()
    {
        var cfg = new SecurityConfig { LockEnabled = false };
        var state = new AppState();

        var gate = new AuthGate(cfg, state);

        Assert.True(gate.IsUnlocked);
    }

    [Fact]
    public void Constructor_WithLockEnabledButNoPasswordSet_StartsUnlocked()
    {
        var cfg = new SecurityConfig { LockEnabled = true };
        var state = new AppState();

        var gate = new AuthGate(cfg, state);

        Assert.True(gate.IsUnlocked);
        Assert.False(gate.IsConfigured);
    }

    [Fact]
    public void TryUnlock_WrongPassword_StaysLocked()
    {
        var (gate, _, _) = MakeConfigured("hunter2");

        bool ok = gate.TryUnlock("not-it");

        Assert.False(ok);
        Assert.False(gate.IsUnlocked);
    }

    [Fact]
    public void TryUnlock_CorrectPassword_Unlocks()
    {
        var (gate, _, _) = MakeConfigured("hunter2");

        bool ok = gate.TryUnlock("hunter2");

        Assert.True(ok);
        Assert.True(gate.IsUnlocked);
    }

    [Fact]
    public void Lock_SetsLocked()
    {
        var (gate, _, _) = MakeConfigured("hunter2");
        gate.TryUnlock("hunter2");
        Assert.True(gate.IsUnlocked);

        gate.Lock();

        Assert.False(gate.IsUnlocked);
    }

    [Fact]
    public void IsConfigured_FalseWhenHashEmpty()
    {
        var cfg = new SecurityConfig { PasswordHash = "", PasswordSalt = "" };
        var gate = new AuthGate(cfg, new AppState());

        Assert.False(gate.IsConfigured);
    }

    [Fact]
    public void VerifyPassword_DoesNotMutateLockState()
    {
        var (gate, _, _) = MakeConfigured("hunter2");
        Assert.False(gate.IsUnlocked);

        bool ok = gate.VerifyPassword("hunter2");

        Assert.True(ok);
        Assert.False(gate.IsUnlocked);
    }

    [Fact]
    public void VerifyPassword_WrongPasswordReturnsFalse()
    {
        var (gate, _, _) = MakeConfigured("hunter2");

        Assert.False(gate.VerifyPassword("nope"));
    }

    [Fact]
    public void VerifyPassword_NotConfigured_ReturnsTrue()
    {
        var gate = new AuthGate(new SecurityConfig(), new AppState());

        Assert.True(gate.VerifyPassword("anything"));
    }

    [Fact]
    public void Hash_RoundTripSurvivesSerialization()
    {
        var (_, cfg, _) = MakeConfigured("correct horse battery staple");

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };
        string json = JsonSerializer.Serialize(cfg, options);
        var loaded = JsonSerializer.Deserialize<SecurityConfig>(json, options);

        Assert.NotNull(loaded);
        var reGate = new AuthGate(loaded!, new AppState());
        Assert.True(reGate.TryUnlock("correct horse battery staple"));
        Assert.False(new AuthGate(loaded!, new AppState()).TryUnlock("nope"));
    }
}
