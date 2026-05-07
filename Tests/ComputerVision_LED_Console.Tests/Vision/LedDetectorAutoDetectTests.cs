using System;
using System.Linq;
using ComputerVision_LED_Console.Camera;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Utilities;
using ComputerVision_LED_Console.Vision;
using OpenCvSharp;

namespace ComputerVision_LED_Console.Tests.Vision;

public class LedDetectorAutoDetectTests
{
    // BGR (0, 255, 255) = RGB (255, 255, 0) = yellow.
    // In HSV (default OpenCV 8U range) this lands at roughly (30, 255, 255),
    // which sits comfortably inside DetectionConfig's defaults (H 20-35, S 100-255, V 150-255).
    private static readonly Scalar Yellow = new(0, 255, 255);
    private static readonly Scalar Red = new(0, 0, 255);
    private static readonly Scalar Green = new(0, 255, 0);
    private static readonly Scalar DarkBackground = new(30, 30, 30);

    private static FrameData MakeFrameWithDot(int width, int height, OpenCvSharp.Point at, int radius, Scalar color)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, DarkBackground);
        Cv2.Circle(mat, at, radius, color, thickness: -1);
        return new FrameData(mat, DateTime.UtcNow);
    }

    private static FrameData MakeFrameWithYellowDot(int width, int height, OpenCvSharp.Point at, int radius)
        => MakeFrameWithDot(width, height, at, radius, Yellow);

    private static FrameData MakeSolidFrame(int width, int height, Scalar color)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, color);
        return new FrameData(mat, DateTime.UtcNow);
    }

    [Fact]
    public void AutoDetect_OnFrameWithOneLed_AddsOneMarker()
    {
        var detector = new LedDetector(new DetectionConfig(), new SystemTimeProvider());
        using var frame = MakeFrameWithYellowDot(640, 480, new OpenCvSharp.Point(320, 240), radius: 15);

        detector.AutoDetect(frame);

        Assert.Single(detector.Rois);
    }

    [Fact]
    public void AutoDetect_OnSolidDarkFrame_AddsNothing()
    {
        var detector = new LedDetector(new DetectionConfig(), new SystemTimeProvider());
        using var frame = MakeSolidFrame(640, 480, DarkBackground);

        detector.AutoDetect(frame);

        Assert.Empty(detector.Rois);
    }

    [Fact]
    public void AutoDetect_CalledTwice_DoesNotDuplicateMarkers()
    {
        var detector = new LedDetector(new DetectionConfig(), new SystemTimeProvider());
        using var frame = MakeFrameWithYellowDot(640, 480, new OpenCvSharp.Point(320, 240), radius: 15);

        detector.AutoDetect(frame);
        detector.AutoDetect(frame);

        Assert.Single(detector.Rois);
    }

    [Fact]
    public void AutoDetect_FiltersContoursBelowMinRadius()
    {
        // MinDetectRadius default = 4. A radius-2 dot is 4 px across and should be filtered.
        var detector = new LedDetector(new DetectionConfig(), new SystemTimeProvider());
        using var frame = MakeFrameWithYellowDot(640, 480, new OpenCvSharp.Point(320, 240), radius: 2);

        detector.AutoDetect(frame);

        Assert.Empty(detector.Rois);
    }

    [Fact]
    public void AutoDetect_FiltersContoursAboveMaxRadius()
    {
        // MaxDetectRadius default = 60. A radius-80 dot is 160 px across and should be filtered.
        var detector = new LedDetector(new DetectionConfig(), new SystemTimeProvider());
        using var frame = MakeFrameWithYellowDot(640, 480, new OpenCvSharp.Point(320, 240), radius: 80);

        detector.AutoDetect(frame);

        Assert.Empty(detector.Rois);
    }

    [Fact]
    public void AutoDetect_YellowDot_TagsMarkerYellow()
    {
        var detector = new LedDetector(new DetectionConfig(), new SystemTimeProvider());
        using var frame = MakeFrameWithDot(640, 480, new OpenCvSharp.Point(320, 240), radius: 15, Yellow);

        detector.AutoDetect(frame);

        Assert.Single(detector.Rois);
        Assert.Equal(LedColor.Yellow, detector.Rois[0].Color);
    }

    [Fact]
    public void AutoDetect_RedDot_TagsMarkerRed()
    {
        var detector = new LedDetector(new DetectionConfig(), new SystemTimeProvider());
        using var frame = MakeFrameWithDot(640, 480, new OpenCvSharp.Point(320, 240), radius: 15, Red);

        detector.AutoDetect(frame);

        Assert.Single(detector.Rois);
        Assert.Equal(LedColor.Red, detector.Rois[0].Color);
    }

    [Fact]
    public void AutoDetect_GreenDot_TagsMarkerGreen()
    {
        var detector = new LedDetector(new DetectionConfig(), new SystemTimeProvider());
        using var frame = MakeFrameWithDot(640, 480, new OpenCvSharp.Point(320, 240), radius: 15, Green);

        detector.AutoDetect(frame);

        Assert.Single(detector.Rois);
        Assert.Equal(LedColor.Green, detector.Rois[0].Color);
    }

    [Fact]
    public void AutoDetect_MultiColorFrame_TagsEachByItsBand()
    {
        var detector = new LedDetector(new DetectionConfig(), new SystemTimeProvider());
        using var mat = new Mat(480, 640, MatType.CV_8UC3, DarkBackground);
        Cv2.Circle(mat, new OpenCvSharp.Point(120, 240), 15, Red, thickness: -1);
        Cv2.Circle(mat, new OpenCvSharp.Point(320, 240), 15, Yellow, thickness: -1);
        Cv2.Circle(mat, new OpenCvSharp.Point(520, 240), 15, Green, thickness: -1);
        using var frame = new FrameData(mat, DateTime.UtcNow);

        detector.AutoDetect(frame);

        Assert.Equal(3, detector.Rois.Count);
        var colors = detector.Rois.Select(r => r.Color).OrderBy(c => c).ToArray();
        Assert.Equal(new[] { LedColor.Red, LedColor.Yellow, LedColor.Green }, colors);
    }
}
