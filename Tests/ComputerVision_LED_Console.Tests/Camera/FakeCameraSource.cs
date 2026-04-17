using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ComputerVision_LED_Console.Camera;
using OpenCvSharp;

namespace ComputerVision_LED_Console.Tests.Camera;

public class FakeCameraSource : ICameraSource
{
    private readonly IReadOnlyList<string> _paths;
    private readonly bool _loop;
    private int _index;

    public FakeCameraSource(IReadOnlyList<string> framePaths, bool loop = true)
    {
        _paths = framePaths;
        _loop = loop;
    }

    public bool IsOpen { get; private set; }
    public int DeviceIndex { get; } = -1;
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }
    public double FramesPerSecond { get; } = 30.0;

    public bool Open()
    {
        if (_paths.Count == 0)
        {
            return false;
        }

        using var first = Cv2.ImRead(_paths[0]);
        if (first.Empty())
        {
            return false;
        }

        FrameWidth = first.Width;
        FrameHeight = first.Height;
        _index = 0;
        IsOpen = true;
        return true;
    }

    public bool TryReadFrame([NotNullWhen(true)] out FrameData? frame)
    {
        if (!IsOpen || _paths.Count == 0)
        {
            frame = null;
            return false;
        }

        if (_index >= _paths.Count)
        {
            if (!_loop)
            {
                frame = null;
                return false;
            }
            _index = 0;
        }

        var mat = Cv2.ImRead(_paths[_index]);
        _index++;
        if (mat.Empty())
        {
            mat.Dispose();
            frame = null;
            return false;
        }

        frame = new FrameData(mat, DateTime.UtcNow);
        return true;
    }

    public void Close() => IsOpen = false;

    public void Dispose() => Close();
}
