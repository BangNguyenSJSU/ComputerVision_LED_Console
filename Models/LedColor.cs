namespace ComputerVision_LED_Console.Models
{
    public enum LedColor
    {
        // Unknown is intentionally 0 so default(LedColor) never silently reads as a real color.
        Unknown = 0,
        Red = 1,
        Yellow = 2,
        Green = 3,
    }
}
