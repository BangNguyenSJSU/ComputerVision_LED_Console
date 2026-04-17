using System;
using System.IO;
using OpenCvSharp;

namespace ComputerVision_LED_Console.Tests.Camera;

public class FakeCameraSourceTests : IDisposable
{
    private readonly string _tempDir;

    public FakeCameraSourceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FakeCameraSourceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string WriteSolidFrame(Scalar color)
    {
        string path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".png");
        using var mat = new Mat(64, 96, MatType.CV_8UC3, color);
        Cv2.ImWrite(path, mat);
        return path;
    }

    [Fact]
    public void TryReadFrame_ReturnsFramesInOrder()
    {
        string p1 = WriteSolidFrame(new Scalar(255, 0, 0));
        string p2 = WriteSolidFrame(new Scalar(0, 255, 0));
        using var src = new FakeCameraSource(new[] { p1, p2 }, loop: false);
        Assert.True(src.Open());

        Assert.True(src.TryReadFrame(out var f1));
        using (f1)
        {
            Assert.NotNull(f1);
            Assert.Equal(96, f1.Frame.Width);
            Assert.Equal(64, f1.Frame.Height);
        }

        Assert.True(src.TryReadFrame(out var f2));
        f2!.Dispose();
    }

    [Fact]
    public void TryReadFrame_ReturnsFalseWhenExhausted()
    {
        string p1 = WriteSolidFrame(new Scalar(100, 100, 100));
        using var src = new FakeCameraSource(new[] { p1 }, loop: false);
        Assert.True(src.Open());

        Assert.True(src.TryReadFrame(out var f1));
        f1!.Dispose();

        Assert.False(src.TryReadFrame(out var f2));
        Assert.Null(f2);
    }

    [Fact]
    public void Loop_RewindsToFirstFrame()
    {
        string p1 = WriteSolidFrame(new Scalar(100, 100, 100));
        using var src = new FakeCameraSource(new[] { p1 }, loop: true);
        Assert.True(src.Open());

        Assert.True(src.TryReadFrame(out var f1));
        f1!.Dispose();

        Assert.True(src.TryReadFrame(out var f2));
        Assert.NotNull(f2);
        f2.Dispose();
    }
}
