using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Vision;
using OpenCvSharp;

namespace ComputerVision_LED_Console.Tests.Vision;

public class LedDetectorHysteresisTests
{
    [Theory]
    [InlineData(new double[] { 110 }, LedStatus.Off)]
    [InlineData(new double[] { 140 }, LedStatus.Off)]
    [InlineData(new double[] { 150 }, LedStatus.On)]
    [InlineData(new double[] { 100, 150 }, LedStatus.On)]
    [InlineData(new double[] { 100, 150, 140 }, LedStatus.On)]
    [InlineData(new double[] { 100, 150, 140, 120 }, LedStatus.Off)]
    [InlineData(new double[] { 100, 150, 140, 120, 140 }, LedStatus.Off)]
    public void Transition_Sequence_EndsInExpectedState(double[] brightness, LedStatus expected)
    {
        LedStatus state = LedStatus.Off;
        foreach (double b in brightness)
        {
            state = LedDetector.Transition(state, b, onThreshold: 150, offThreshold: 120);
        }

        Assert.Equal(expected, state);
    }

    [Fact]
    public void EnforceThresholdGap_NarrowsOff_WhenAdjustOnFalse()
    {
        var roi = new RoiConfig { OnThreshold = 150, OffThreshold = 148 };

        LedDetector.EnforceThresholdGap(roi, minGap: 5, adjustOn: false);

        Assert.Equal(145, roi.OffThreshold);
        Assert.Equal(150, roi.OnThreshold);
    }

    [Fact]
    public void EnforceThresholdGap_RaisesOn_WhenAdjustOnTrue()
    {
        var roi = new RoiConfig { OnThreshold = 122, OffThreshold = 120 };

        LedDetector.EnforceThresholdGap(roi, minGap: 5, adjustOn: true);

        Assert.Equal(125, roi.OnThreshold);
        Assert.Equal(120, roi.OffThreshold);
    }

    [Fact]
    public void EnforceThresholdGap_NoOp_WhenGapAlreadyLargeEnough()
    {
        var roi = new RoiConfig { OnThreshold = 150, OffThreshold = 120 };

        LedDetector.EnforceThresholdGap(roi, minGap: 5, adjustOn: false);

        Assert.Equal(150, roi.OnThreshold);
        Assert.Equal(120, roi.OffThreshold);
    }

    [Fact]
    public void ClampRectToFrame_ReturnsEmpty_WhenRectFullyOutside()
    {
        Rect rect = new(1000, 1000, 100, 100);

        Rect clamped = LedDetector.ClampRectToFrame(rect, 640, 480);

        Assert.Equal(0, clamped.Width);
        Assert.Equal(0, clamped.Height);
    }

    [Fact]
    public void ClampRectToFrame_ClampsNegativeOrigin_ButPreservesWidth()
    {
        // Preserves the original (v0) behavior: origin clamped to 0, width not reduced
        // by the pre-clamp overflow. Real markers never have negative centers in practice.
        Rect rect = new(-10, -10, 50, 50);

        Rect clamped = LedDetector.ClampRectToFrame(rect, 640, 480);

        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
        Assert.Equal(50, clamped.Width);
        Assert.Equal(50, clamped.Height);
    }

    [Fact]
    public void ClampRectToFrame_ClipsWidth_WhenExtendingBeyondRightEdge()
    {
        Rect rect = new(600, 400, 100, 100);

        Rect clamped = LedDetector.ClampRectToFrame(rect, 640, 480);

        Assert.Equal(600, clamped.X);
        Assert.Equal(400, clamped.Y);
        Assert.Equal(40, clamped.Width);
        Assert.Equal(80, clamped.Height);
    }

    [Fact]
    public void FindRoiAt_ReturnsIndex_WhenInsideRadius()
    {
        var detector = new LedDetector(new DetectionConfig());
        detector.AddRoi(x: 100, y: 100, radius: 20);

        int idx = detector.FindRoiAt(x: 105, y: 95);

        Assert.Equal(0, idx);
    }

    [Fact]
    public void FindRoiAt_ReturnsMinusOne_WhenOutside()
    {
        var detector = new LedDetector(new DetectionConfig());
        detector.AddRoi(x: 100, y: 100, radius: 20);

        int idx = detector.FindRoiAt(x: 500, y: 500);

        Assert.Equal(-1, idx);
    }

    [Fact]
    public void AddRoi_AssignsIncrementingIds()
    {
        var detector = new LedDetector(new DetectionConfig());

        detector.AddRoi(0, 0, 10);
        detector.AddRoi(10, 10, 10);

        Assert.Equal(1, detector.Rois[0].Id);
        Assert.Equal(2, detector.Rois[1].Id);
    }

    [Fact]
    public void RemoveRoiAt_ReturnsFalse_WhenIndexOutOfRange()
    {
        var detector = new LedDetector(new DetectionConfig());

        Assert.False(detector.RemoveRoiAt(0));
        Assert.False(detector.RemoveRoiAt(-1));
    }
}
