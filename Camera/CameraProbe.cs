using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace ComputerVision_LED_Console.Camera
{
    public static class CameraProbe
    {
        public static List<int> ScanAvailable(int maxIndexToProbe)
        {
            var available = new List<int>();

            for (int i = 0; i <= maxIndexToProbe; i++)
            {
                using var probe = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                if (!probe.IsOpened())
                {
                    continue;
                }

                using var testFrame = new Mat();
                probe.Read(testFrame);
                if (!testFrame.Empty())
                {
                    available.Add(i);
                }
            }

            return available;
        }

        public static int PromptUserSelection(IReadOnlyList<int> available)
        {
            if (available.Count == 0)
            {
                Console.WriteLine("No cameras detected.");
                return -1;
            }

            Console.WriteLine("Available cameras:");
            for (int i = 0; i < available.Count; i++)
            {
                string hint = available[i] == 0 ? " (typically integrated webcam)" : " (external / secondary)";
                Console.WriteLine($"  [{i}] Camera index {available[i]}{hint}");
            }

            if (available.Count == 1)
            {
                Console.WriteLine("Only one camera found — selecting it automatically.");
                return available[0];
            }

            while (true)
            {
                Console.Write($"Select a camera [0-{available.Count - 1}] (Enter for 0): ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    return available[0];
                }

                if (int.TryParse(input, out int choice) && choice >= 0 && choice < available.Count)
                {
                    return available[choice];
                }

                Console.WriteLine("Invalid selection, try again.");
            }
        }
    }
}
