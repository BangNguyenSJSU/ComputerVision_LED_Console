using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
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

        // FriendlyNames in DirectShow enumeration order. Position N corresponds to the
        // OpenCV DSHOW device index N, including indices that ScanAvailable rejected.
        // Returns an empty list if enumeration fails (non-Windows, COM error, no devices).
        [SupportedOSPlatform("windows")]
        public static IReadOnlyList<string> GetDirectShowVideoInputNames()
        {
            var names = new List<string>();
            object? devEnumObj = null;
            IEnumMoniker? enumMoniker = null;
            try
            {
                var devEnumType = Type.GetTypeFromCLSID(SystemDeviceEnumClsid);
                if (devEnumType is null) return names;

                devEnumObj = Activator.CreateInstance(devEnumType);
                if (devEnumObj is not ICreateDevEnum devEnum) return names;

                Guid videoInputCategory = VideoInputDeviceCategory;
                int hr = devEnum.CreateClassEnumerator(ref videoInputCategory, out enumMoniker, 0);
                if (hr != 0 || enumMoniker is null) return names;

                var moniker = new IMoniker[1];
                while (enumMoniker.Next(1, moniker, IntPtr.Zero) == 0)
                {
                    names.Add(ReadFriendlyName(moniker[0]));
                    Marshal.ReleaseComObject(moniker[0]);
                }
            }
            catch
            {
                // Best-effort. Fall back to anonymous indices.
            }
            finally
            {
                if (enumMoniker is not null) Marshal.ReleaseComObject(enumMoniker);
                if (devEnumObj is not null) Marshal.ReleaseComObject(devEnumObj);
            }
            return names;
        }

        [SupportedOSPlatform("windows")]
        private static string ReadFriendlyName(IMoniker moniker)
        {
            object? bagObj = null;
            try
            {
                Guid bagId = typeof(IPropertyBag).GUID;
                moniker.BindToStorage(null!, null!, ref bagId, out bagObj);
                if (bagObj is not IPropertyBag bag) return "(unknown camera)";

                object? value = null;
                int hr = bag.Read("FriendlyName", ref value, IntPtr.Zero);
                return (hr == 0 && value is string s) ? s : "(unknown camera)";
            }
            catch
            {
                return "(unknown camera)";
            }
            finally
            {
                if (bagObj is not null) Marshal.ReleaseComObject(bagObj);
            }
        }

        public static int PromptUserSelection(IReadOnlyList<int> available, IReadOnlyList<string> namesByIndex)
        {
            if (available.Count == 0)
            {
                Console.WriteLine("No cameras detected.");
                return -1;
            }

            Console.WriteLine("Available cameras:");
            for (int i = 0; i < available.Count; i++)
            {
                int idx = available[i];
                string name = idx >= 0 && idx < namesByIndex.Count ? namesByIndex[idx] : "(unknown camera)";
                string hint = idx == 0 ? " (typically integrated webcam)" : "";
                Console.WriteLine($"  [{i}] {name} — index {idx}{hint}");
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

        // ---- DirectShow COM interop ----

        private static readonly Guid SystemDeviceEnumClsid = new("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
        private static readonly Guid VideoInputDeviceCategory = new("860BB310-5D01-11d0-BD3B-00A0C911CE86");

        [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICreateDevEnum
        {
            [PreserveSig]
            int CreateClassEnumerator([In] ref Guid pCategory, out IEnumMoniker? ppEnumMoniker, int dwFlags);
        }

        [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyBag
        {
            [PreserveSig]
            int Read(
                [MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
                [In, Out, MarshalAs(UnmanagedType.Struct)] ref object? pVar,
                IntPtr pErrorLog);

            [PreserveSig]
            int Write(
                [MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
                [In, MarshalAs(UnmanagedType.Struct)] ref object pVar);
        }
    }
}
