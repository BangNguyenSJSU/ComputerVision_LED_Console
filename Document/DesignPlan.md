You are a senior software engineer helping me build a maintainable C# computer vision application for LED ON/OFF detection using a webcam.

Project goal:
Build a desktop-side computer vision tool that captures frames from a webcam, detects whether a target LED is ON or OFF inside a configurable ROI, and publishes the latest status for future integration with localhost, TCP/IP, and HTTP interfaces.

Important priorities:
1. Clean architecture
2. Maintainability
3. Easy feature expansion
4. Clear separation of responsibilities
5. Production-minded code structure, not a one-file demo

Tech constraints:
- Language: C#
- Computer vision library: OpenCvSharp
- Initial runtime can be Console App for simplicity
- Visual Studio compatibility is important
- Avoid unnecessary GUI coupling in the core design
- Design so it can later support WinForms or WPF without rewriting core logic

Architectural requirements:
Organize the solution by responsibility using folders and classes similar to this:

ComputerVision_LED/
├─ Program.cs
├─ App/
│  ├─ AppController.cs
│  └─ AppState.cs
├─ Camera/
│  ├─ ICameraSource.cs
│  ├─ OpenCvCameraSource.cs
│  └─ FrameData.cs
├─ Vision/
│  ├─ ILedDetector.cs
│  ├─ LedDetector.cs
│  ├─ DetectionResult.cs
│  └─ RoiConfig.cs
├─ Network/
│  ├─ IStatusPublisher.cs
│  ├─ TcpStatusServer.cs
│  ├─ HttpStatusServer.cs
│  └─ LocalhostApiServer.cs
├─ Config/
│  ├─ AppConfig.cs
│  ├─ CameraConfig.cs
│  ├─ DetectionConfig.cs
│  └─ NetworkConfig.cs
├─ Models/
│  ├─ LedStatus.cs
│  └─ SystemStatus.cs
├─ Services/
│  ├─ StatusService.cs
│  └─ ConfigService.cs
├─ UI/
│  ├─ MainForm.cs
│  └─ MainFormPresenter.cs
└─ Utilities/
   ├─ Logger.cs
   └─ TimeProvider.cs

Design rules:
- Camera layer only handles frame acquisition
- Vision layer only handles LED detection logic
- Network layer only handles HTTP/TCP/localhost publishing
- UI layer only handles user interaction and display
- Shared state should be managed through a service layer
- Detection logic must not depend on UI or transport
- Network code must not depend on OpenCV Mat internals
- Configuration values must not be scattered as hardcoded constants across the codebase
- Use simple interfaces where it helps future replacement and testing
- Favor small classes with clear responsibility
- Keep code readable and practical, not overengineered

Core functional requirements for version 1:
1. Open webcam using OpenCvSharp
2. Grab frames continuously
3. Apply LED detection in a configurable ROI
4. Convert ROI to grayscale
5. Measure average brightness
6. Use hysteresis thresholds:
   - OFF -> ON when brightness >= onThreshold
   - ON -> OFF when brightness <= offThreshold
7. Return structured detection results including:
   - LED state
   - brightness
   - timestamp
8. Store latest result in a shared status service
9. Display basic debug output in the console
10. Keep the code ready for future HTTP/TCP integration

Future expansion requirements:
Design now so the project can later support:
- localhost HTTP endpoint such as GET /status
- TCP status publishing
- JSON serialization of current system state
- multiple LEDs / multiple ROIs
- GUI integration
- config file loading
- logging
- unit testing

Expected model examples:
- LedStatus enum
- DetectionResult model
- SystemStatus model
- CameraConfig, DetectionConfig, NetworkConfig

Implementation guidance:
- Start with a minimal but clean solution
- Version 1 can be a Console App
- Use a main application controller to wire the modules together
- Use a thread-safe status service if needed
- Keep methods short and clear
- Add comments where design intent matters
- Avoid massive classes
- Avoid putting all logic in Program.cs

Output requirements:
1. First propose the final folder structure
2. Then explain the role of each class in a concise way
3. Then generate the code files one by one
4. For each file, include the full code
5. Ensure the code compiles as a coherent project
6. Include NuGet package requirements
7. Include a brief run instruction section
8. Include notes about where to extend for HTTP/TCP later

Coding style:
- Use explicit, readable names
- Keep business logic easy to trace
- Prefer pragmatic engineering style over academic patterns
- Do not add unnecessary frameworks
- Do not introduce dependency injection containers unless clearly justified
- Use plain C# classes/interfaces unless a stronger reason exists

Please begin by:
1. Proposing the concrete simplified version of this architecture for version 1
2. Listing the files to create
3. Then generating the full code for each file


Act as a senior C# software engineer. Build a maintainable C# OpenCvSharp webcam LED detection project with clean architecture.

I do not want a one-file demo. I want a small but professional structure that can grow into localhost HTTP and TCP/IP status publishing later.

Requirements:
- C#
- OpenCvSharp
- Start as Console App
- Clean separation:
  - Camera
  - Vision
  - Models
  - Services
  - Config
  - Network placeholders
- Detect LED ON/OFF using ROI brightness and hysteresis
- Produce structured status objects
- Keep latest status in a shared service
- Console output for now
- Ready to add GET /status later

Please generate:
1. folder structure
2. file list
3. complete code for each file
4. NuGet packages needed
5. explanation of how the runtime flow works
6. specific guidance for where HTTP and TCP integration should be added later

Design constraints:
- detector must not depend on UI or network
- network must not depend on OpenCV Mat details
- config values centralized in config classes
- classes should be small and focused
- keep code practical and easy to maintain

Use this target structure as guidance, but simplify if needed for version 1:

- Program.cs
- App/AppController.cs
- Camera/ICameraSource.cs
- Camera/OpenCvCameraSource.cs
- Camera/FrameData.cs
- Vision/ILedDetector.cs
- Vision/LedDetector.cs
- Vision/RoiConfig.cs
- Models/LedStatus.cs
- Models/SystemStatus.cs
- Models/DetectionResult.cs
- Services/StatusService.cs
- Config/AppConfig.cs
- Config/CameraConfig.cs
- Config/DetectionConfig.cs
- Config/NetworkConfig.cs
- Network/HttpStatusServer.cs
- Network/TcpStatusServer.cs

Please keep the first version compileable and not overly complex.