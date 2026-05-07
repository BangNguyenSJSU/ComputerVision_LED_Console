using ComputerVision_LED_Console.App;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Tests.Camera;
using ComputerVision_LED_Console.Utilities;
using ComputerVision_LED_Console.Vision;
using OpenCvSharp;

namespace ComputerVision_LED_Console.Tests.App;

public class InputHandlerLockTests
{
    private static (InputHandler input, AuthGate auth, FakeCameraSource cam, LedDetector det, AppState state) Wire()
    {
        var detection = new DetectionConfig();
        var cameraCfg = new CameraConfig();
        var detector = new LedDetector(detection, new SystemTimeProvider());
        var state = new AppState();
        var camera = new FakeCameraSource(System.Array.Empty<string>());

        var sec = new SecurityConfig { LockEnabled = true, PasswordIterations = 1_000 };
        var bootstrap = new AuthGate(sec, new AppState());
        var (hash, salt) = bootstrap.HashNewPassword("pw");
        sec.PasswordHash = hash;
        sec.PasswordSalt = salt;

        var auth = new AuthGate(sec, state);
        var input = new InputHandler(detector, state, detection, camera, cameraCfg, auth);
        return (input, auth, camera, detector, state);
    }

    [Fact]
    public void OnMouse_LButtonDown_DoesNotAddMarkerWhileLocked()
    {
        var (input, auth, _, det, _) = Wire();
        Assert.False(auth.IsUnlocked);

        input.OnMouse(MouseEventTypes.LButtonDown, x: 50, y: 50, (MouseEventFlags)0, System.IntPtr.Zero);

        Assert.Empty(det.Rois);
    }

    [Fact]
    public void HandleKey_S_DoesNotReturnSaveWhileLocked()
    {
        var (input, _, _, _, _) = Wire();

        var action = input.HandleKey('s');

        Assert.Equal(KeyAction.Continue, action);
    }

    [Theory]
    [InlineData('+')]
    [InlineData('-')]
    [InlineData(',')]
    [InlineData('.')]
    [InlineData('a')]
    public void HandleKey_CameraKeys_NoOpWhileLocked(char key)
    {
        var (input, _, cam, _, _) = Wire();

        input.HandleKey(key);

        Assert.Equal(0, cam.ZoomAdjustCalls);
        Assert.Equal(0, cam.ExposureAdjustCalls);
        Assert.Equal(0, cam.AutoExposureToggleCalls);
    }

    [Fact]
    public void HandleKey_QuitAndRescan_AlwaysAllowedWhileLocked()
    {
        var (input, _, _, _, _) = Wire();

        Assert.Equal(KeyAction.Quit, input.HandleKey('q'));
        Assert.Equal(KeyAction.Quit, input.HandleKey(27));
        Assert.Equal(KeyAction.Rescan, input.HandleKey('r'));
    }

    [Fact]
    public void HandleKey_UAndL_RecognizedWhileLocked()
    {
        var (input, _, _, _, _) = Wire();

        Assert.Equal(KeyAction.UnlockPrompt, input.HandleKey('u'));
        Assert.Equal(KeyAction.Lock, input.HandleKey('l'));
    }

    [Fact]
    public void HandleKey_P_ReturnsPasswordToggleWhileLocked()
    {
        var (input, _, _, _, _) = Wire();

        Assert.Equal(KeyAction.PasswordToggle, input.HandleKey('p'));
        Assert.Equal(KeyAction.PasswordToggle, input.HandleKey('P'));
    }

    [Fact]
    public void HandleKey_P_ReturnsPasswordToggleWhileUnlocked()
    {
        var (input, auth, _, _, _) = Wire();
        auth.TryUnlock("pw");

        Assert.Equal(KeyAction.PasswordToggle, input.HandleKey('p'));
    }

    [Fact]
    public void HandleKey_AfterUnlock_SaveReturnsSave()
    {
        var (input, auth, _, _, _) = Wire();
        auth.TryUnlock("pw");

        var action = input.HandleKey('s');

        Assert.Equal(KeyAction.Save, action);
    }

    [Fact]
    public void OnMouse_AfterUnlock_AddsMarker()
    {
        var (input, auth, _, det, _) = Wire();
        auth.TryUnlock("pw");

        input.OnMouse(MouseEventTypes.LButtonDown, x: 50, y: 50, (MouseEventFlags)0, System.IntPtr.Zero);

        Assert.Single(det.Rois);
    }

    [Fact]
    public void HandleKey_CameraKeys_ApplyAfterUnlock()
    {
        var (input, auth, cam, _, _) = Wire();
        auth.TryUnlock("pw");

        input.HandleKey('+');

        Assert.Equal(1, cam.ZoomAdjustCalls);
    }
}
