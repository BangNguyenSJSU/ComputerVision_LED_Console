using System;
using ComputerVision_LED_Console.Camera;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Vision;
using OpenCvSharp;

namespace ComputerVision_LED_Console.App
{
    public enum KeyAction
    {
        Continue,
        Quit,
        Rescan,
        Save,
        UnlockPrompt,
        Lock,
        PasswordToggle,
    }

    public class InputHandler
    {
        private readonly LedDetector _detector;
        private readonly AppState _state;
        private readonly DetectionConfig _config;
        private readonly ICameraSource _camera;
        private readonly CameraConfig _cameraConfig;
        private readonly AuthGate _auth;
        private Func<float, float, LedColor>? _colorSampler;

        public InputHandler(LedDetector detector, AppState state, DetectionConfig config, ICameraSource camera, CameraConfig cameraConfig, AuthGate auth)
        {
            _detector = detector;
            _state = state;
            _config = config;
            _camera = camera;
            _cameraConfig = cameraConfig;
            _auth = auth;
        }

        public void UpdateColorSampler(Func<float, float, LedColor>? sampler) => _colorSampler = sampler;

        public void OnMouse(MouseEventTypes eventType, int x, int y, MouseEventFlags flags, IntPtr userData)
        {
            if (!_auth.IsUnlocked) return;

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
                        var color = _colorSampler?.Invoke(nx, ny) ?? LedColor.Unknown;
                        int newIdx = _detector.AddManualRoi(nx, ny, color);
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
                case MouseEventTypes.MouseWheel:
                {
                    // OpenCV packs the wheel delta into the high 16 bits of `flags` as a signed short.
                    // Typical OS reports +/-120 per notch; we only care about the sign.
                    int sign = Math.Sign((short)(((int)flags >> 16) & 0xFFFF));
                    if (sign == 0) break;

                    int target = _detector.FindRoiAt(nx, ny);
                    if (target < 0) target = _state.SelectedMarkerIndex;
                    if (target < 0 || target >= _detector.Rois.Count) break;

                    _detector.AdjustRoiRadius(target, sign * _config.RoiRadiusStep);
                    _state.SelectedMarkerIndex = target;
                    break;
                }
            }
        }

        public KeyAction HandleKey(int key)
        {
            // Always-allowed: quit, rescan, lock/unlock prompts.
            if (key == 'q' || key == 'Q' || key == 27) return KeyAction.Quit;

            if (key == 'r' || key == 'R')
            {
                _detector.Clear();
                _state.DragMarkerIndex = -1;
                _state.SelectedMarkerIndex = -1;
                return KeyAction.Rescan;
            }

            if (key == 'u' || key == 'U') return KeyAction.UnlockPrompt;
            if (key == 'l' || key == 'L') return KeyAction.Lock;
            if (key == 'p' || key == 'P') return KeyAction.PasswordToggle;

            if (!_auth.IsUnlocked) return KeyAction.Continue;

            if (key == 's' || key == 'S')
            {
                return KeyAction.Save;
            }

            if (key == '=' || key == '+') { _camera.AdjustZoom(+_cameraConfig.ZoomStep); return KeyAction.Continue; }
            if (key == '-' || key == '_') { _camera.AdjustZoom(-_cameraConfig.ZoomStep); return KeyAction.Continue; }
            if (key == '.') { _camera.AdjustExposure(+_cameraConfig.ExposureStep); return KeyAction.Continue; }
            if (key == ',') { _camera.AdjustExposure(-_cameraConfig.ExposureStep); return KeyAction.Continue; }
            if (key == 'a' || key == 'A') { _camera.ToggleAutoExposure(); return KeyAction.Continue; }

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
