namespace ComputerVision_LED_Console.App
{
    public class AppState
    {
        public bool IsRunning { get; set; }
        public int SelectedMarkerIndex { get; set; } = -1;
        public int DragMarkerIndex { get; set; } = -1;
        public double DisplayScale { get; set; } = 1.0;
    }
}
