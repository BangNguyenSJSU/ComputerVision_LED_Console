using ComputerVision_LED_Console.Models;

namespace ComputerVision_LED_Console.Tests.Models;

public class LedStatusTests
{
    [Fact]
    public void DefaultValue_IsUnknown()
    {
        LedStatus value = default;

        Assert.Equal(LedStatus.Unknown, value);
    }
}
