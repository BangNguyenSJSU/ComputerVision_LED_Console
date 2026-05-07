using ComputerVision_LED_Console.App;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Tests.Camera;
using ComputerVision_LED_Console.Utilities;
using ComputerVision_LED_Console.Vision;
using OpenCvSharp;

namespace ComputerVision_LED_Console.Tests.App;

public class InputHandlerWheelTests
{
    private static (InputHandler input, AppState state, LedDetector det, AuthGate auth) Wire()
    {
        var detection = new DetectionConfig();
        var cameraCfg = new CameraConfig();
        var detector = new LedDetector(detection, new SystemTimeProvider());
        var state = new AppState();
        var camera = new FakeCameraSource(System.Array.Empty<string>());

        // Lock disabled so we can drive the wheel path without prior unlock.
        var auth = new AuthGate(new SecurityConfig { LockEnabled = false }, state);
        var input = new InputHandler(detector, state, detection, camera, cameraCfg, auth);
        return (input, state, detector, auth);
    }

    private static MouseEventFlags WheelDelta(int delta)
    {
        // OpenCV packs wheel delta into the high 16 bits as a signed short.
        return (MouseEventFlags)((delta & 0xFFFF) << 16);
    }

    [Fact]
    public void MouseWheel_OverMarker_GrowsRadius()
    {
        var (input, _, det, _) = Wire();
        det.AddRoi(100, 100, 10);

        input.OnMouse(MouseEventTypes.MouseWheel, x: 100, y: 100, WheelDelta(+120), System.IntPtr.Zero);

        Assert.Equal(11, det.Rois[0].Radius);
    }

    [Fact]
    public void MouseWheel_OverMarker_ShrinksRadius()
    {
        var (input, _, det, _) = Wire();
        det.AddRoi(100, 100, 10);

        input.OnMouse(MouseEventTypes.MouseWheel, x: 100, y: 100, WheelDelta(-120), System.IntPtr.Zero);

        Assert.Equal(9, det.Rois[0].Radius);
    }

    [Fact]
    public void MouseWheel_NotOverMarker_FallsBackToSelected()
    {
        var (input, state, det, _) = Wire();
        det.AddRoi(100, 100, 10);
        state.SelectedMarkerIndex = 0;

        input.OnMouse(MouseEventTypes.MouseWheel, x: 500, y: 500, WheelDelta(+120), System.IntPtr.Zero);

        Assert.Equal(11, det.Rois[0].Radius);
    }

    [Fact]
    public void MouseWheel_NoHoverAndNoSelection_NoOp()
    {
        var (input, _, det, _) = Wire();
        det.AddRoi(100, 100, 10);

        input.OnMouse(MouseEventTypes.MouseWheel, x: 500, y: 500, WheelDelta(+120), System.IntPtr.Zero);

        Assert.Equal(10, det.Rois[0].Radius);
    }

    [Fact]
    public void MouseWheel_OverMarker_SelectsThatMarker()
    {
        var (input, state, det, _) = Wire();
        det.AddRoi(100, 100, 10);
        det.AddRoi(300, 300, 10);
        state.SelectedMarkerIndex = 0;

        input.OnMouse(MouseEventTypes.MouseWheel, x: 300, y: 300, WheelDelta(+120), System.IntPtr.Zero);

        Assert.Equal(1, state.SelectedMarkerIndex);
        Assert.Equal(11, det.Rois[1].Radius);
    }

    [Fact]
    public void MouseWheel_BlockedWhenLocked()
    {
        // Re-wire with lock enabled and a configured password so the gate engages.
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
        detector.AddRoi(100, 100, 10);

        Assert.False(auth.IsUnlocked);
        input.OnMouse(MouseEventTypes.MouseWheel, x: 100, y: 100, WheelDelta(+120), System.IntPtr.Zero);

        Assert.Equal(10, detector.Rois[0].Radius);
    }
}
