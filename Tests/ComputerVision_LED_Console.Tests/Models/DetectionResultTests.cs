using ComputerVision_LED_Console.Models;

namespace ComputerVision_LED_Console.Tests.Models;

public class DetectionResultTests
{
    [Fact]
    public void DefaultConstruction_HasUnknownStatus()
    {
        DetectionResult result = new();

        Assert.Equal(LedStatus.Unknown, result.Status);
    }
}
