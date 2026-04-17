using System;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Vision;
using OpenCvSharp;

namespace ComputerVision_LED_Console.App
{
    public enum KeyAction
    {
        Continue,
        Quit,
        Rescan,
    }

    public class InputHandler
    {
        private readonly LedDetector _detector;
        private readonly AppState _state;
        private readonly DetectionConfig _config;

        public InputHandler(LedDetector detector, AppState state, DetectionConfig config)
        {
            _detector = detector;
            _state = state;
            _config = config;
        }

        public void OnMouse(MouseEventTypes eventType, int x, int y, MouseEventFlags flags, IntPtr userData)
        {
            float nx = (float)(x / _state.DisplayScale);
            float ny = (float)(y / _state.DisplayScale);

            switch (eventType)
            {
                case MouseEventTypes.LButtonDown:
                {
                    int hit = _detector.FindRoiAt(nx, ny);
                    if (hit >= 0)
                    {
                        _state.DragMarkerIndex = hit;
                        _state.SelectedMarkerIndex = hit;
                    }
                    else
                    {
                        int newIdx = _detector.AddManualRoi(nx, ny);
                        _state.DragMarkerIndex = newIdx;
                        _state.SelectedMarkerIndex = newIdx;
                    }
                    break;
                }
                case MouseEventTypes.MouseMove:
                    if ((flags & MouseEventFlags.LButton) != 0
                        && _state.DragMarkerIndex >= 0
                        && _state.DragMarkerIndex < _detector.Rois.Count)
                    {
                        _detector.MoveRoi(_state.DragMarkerIndex, nx, ny);
                    }
                    break;
                case MouseEventTypes.LButtonUp:
                    _state.DragMarkerIndex = -1;
                    break;
                case MouseEventTypes.RButtonDown:
                {
                    int hit = _detector.FindRoiAt(nx, ny);
                    if (hit >= 0)
                    {
                        _detector.RemoveRoiAt(hit);
                        if (_state.DragMarkerIndex == hit) _state.DragMarkerIndex = -1;
                        else if (_state.DragMarkerIndex > hit) _state.DragMarkerIndex--;
                        if (_state.SelectedMarkerIndex == hit) _state.SelectedMarkerIndex = -1;
                        else if (_state.SelectedMarkerIndex > hit) _state.SelectedMarkerIndex--;
                    }
                    break;
                }
            }
        }

        public KeyAction HandleKey(int key)
        {
            if (key == 'q' || key == 'Q' || key == 27) return KeyAction.Quit;

            if (key == 'r' || key == 'R')
            {
                _detector.Clear();
                _state.DragMarkerIndex = -1;
                _state.SelectedMarkerIndex = -1;
                return KeyAction.Rescan;
            }

            int sel = _state.SelectedMarkerIndex;
            if (sel >= 0 && sel < _detector.Rois.Count)
            {
                double step = _config.ThresholdStep;
                if (key == 'c' || key == 'C') _detector.ToggleCalibrate(sel);
                else if (key == ']') _detector.TuneOnThreshold(sel, +step);
                else if (key == '[') _detector.TuneOnThreshold(sel, -step);
                else if (key == '\'') _detector.TuneOffThreshold(sel, +step);
                else if (key == ';') _detector.TuneOffThreshold(sel, -step);
            }

            return KeyAction.Continue;
        }
    }
}
