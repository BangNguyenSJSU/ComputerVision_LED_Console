namespace ComputerVision_LED_Console.Models
{
    public enum LedStatus
    {
        // Unknown is intentionally 0 so default(LedStatus) never silently reads as Off or On.
        Unknown = 0,
        Off = 1,
        On = 2,
    }
}
