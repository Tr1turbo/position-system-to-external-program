Position System to External Program
====

*Position System to External Program* is a **prefab** and a **program** that lets you connect the position of standard DPS-like lights
to a robotic arm.

Other users can remotely control the position and rotation of your robotic arm through the virtual space.

> [!TIP]
> Only the **computer connected** to the robotic arm needs the software and the prefab. The other users in the virtual space do not need it,
> they just need a standard DPS-like light.
>
> If they already have a standard DPS-like light, then they can control your robotic arm, no additional setup needed from them.

## How is it done?

This is achieved by encoding pixels to the window screen or the image that is projected into the HMD using a special shader.
Our program then reads those pixels.

Data extraction is done using **harmless screen capture** techniques similar to those used by window and VR live-streaming capture programs.
There is no tampering of the computer program nor any active process. There is no OSC either.

In addition:
- The position and rotation of the camera in world space is also extracted. This could be used to pin SteamVR overlays in world space.
- This optionally exposes a WebSocket service to enable control of the robotic arm from virtual space systems like Resonite.

# User documentation

If you are a user looking to use this software, please [check out the end-user documentation](https://alleyway.hai-vr.dev/docs/products/position-system-to-external-program).

- **[📘 Open documentation](https://alleyway.hai-vr.dev/docs/products/position-system-to-external-program)**

&nbsp;

&nbsp;

&nbsp;

&nbsp;

&nbsp;

-----

# Developer documentation

The information below is for developers looking to maintain the application. If you are a user, [check out the end-user documentation instead](https://alleyway.hai-vr.dev/docs/products/position-system-to-external-program).

### Project structure

Main application execution projects:
- **program**: The main entry point is `program/Program.cs`; this bootstraps all dependencies.
- **application-loop**: The main application loop is in `application-loop/Routine.cs`.
  - When the UI window closes, the UI window will ask the application loop to exit the loop.
- **ui-imgui**: The default UI is in `ui-imgui/UiMainApplication.cs`.
  - This runs in a separate UI thread.
  - The framerate of the application loop does not depend on the refresh rate of the UI.
  - All actions from the UI thread that affect the main application are enqueued through `ui-imgui/UiActions.cs` to run in the main thread.
- **ui-actions**: Contains a class that communicates with the application loop. This restricts UI access to the rest of the application.

Core projects:
- **core** contains data structures shared by many projects in this solution.
- **decoder** contains the logic necessary to decode images into usable data.
- **robotics** contains the controller that decides how the data will drive the robotic arm.
- **config** contains the save file.

External system projects:
- **extractor-gdi**, **extractor-openvr**, **transmit-intiface**, and **transmit-tcode** interact with various external system APIs.
- **service-websockets** is only used if *WebSockets* support is enabled; this skips lights altogether for programs like *Resonite*.

Unity:
- **Packages/dev.hai-vr.alleyway.position-system/** contains the shader and prefab.

### Data extraction procedure

Data extraction goes through this:
- We get a reference to a native texture handle. That texture is very large.
- Using that native texture, we extract a subregion of that texture into a byte array.
- We sample some of these pixels, turning them into zeroes and ones.
- Using that array of bits, we decode the data.

![DataExtraction.png](DataExtraction.png)

If *WebSockets* support is enabled, the position can be submitted directly from other programs, such as a Websocket component within *Resonite*,
rather than going through data extraction.

### Data

Data is made out of sequential 32-bit groups; least significant bit first, little endian.

Data visually starts at the top-left of the region, scans horizontally up until the layout's width, then vertically.
By default, the layout uses 16 columns, with a margin of 1 on every side.

When rendered on a window, it is drawn at the top left. When rendered in VR, it is drawn centered vertically, located against the left edge of the left eye.
The size of the squares in VR is a fixed proportion of the vertical resolution to counteract the *Resolution Per Eye* setting.

By default, the shader outputs:
- 50% gray for its true value. It is 50% so that it does not trigger bloom on post-processing heavy scenarios.
- A pixel made of negative colors (-10000, -10000, -10000, 1) for its false value, which is perceived as black. The pixel is made of
  negative values so that bloom will not affect the black pixels.

On the program side, we use [Otsu's method](https://en.wikipedia.org/wiki/Otsu%27s_method) on the red channel to choose a threshold
so that the decoding process would function even when post-processing significantly dims the entire screen.

### Shader version 1.1.0

Adding new lines at the end is considered to be a breaking change because the checksum would change, and it would be vertically taller, compromising the vertical centering.
This is why there is reserved space for future use. 

| Group  | Description                                                                                                                                                                                                                                              | Added in |
|--------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------|
| **0**  | Checksum. See [checksum](#checksum) section below.                                                                                                                                                                                                       |          |
| **1**  | Time since level load, as given by `_Time.y`. Intended to let the decoder know when the data has changed<br>(i.e. for interpolation, or to detect data stalling).                                                                                        |          |
| **2**  | A version identifier (x: 1366692562).                                                                                                                                                                                                                    |          |
| **3**  | A version identifier (y: 1001000). Translates as 1 001 000, because the version is 1.1.0                                                                                                                                                                 |          |
| **4**  | Position of the 0th light (x), as given by `unity_4LightPosX0[0]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **5**  | Position of the 0th light (y), as given by `unity_4LightPosY0[0]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **6**  | Position of the 0th light (z), as given by `unity_4LightPosZ0[0]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **7**  | Position of the 1st light (x), as given by `unity_4LightPosX0[1]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **8**  | Position of the 1st light (y), as given by `unity_4LightPosY0[1]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **9**  | Position of the 1st light (z), as given by `unity_4LightPosZ0[1]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **10** | Position of the 2nd light (x), as given by `unity_4LightPosX0[2]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **11** | Position of the 2nd light (y), as given by `unity_4LightPosY0[2]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **12** | Position of the 2nd light (z), as given by `unity_4LightPosZ0[2]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **13** | Position of the 3rd light (x), as given by `unity_4LightPosX0[3]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **14** | Position of the 3rd light (y), as given by `unity_4LightPosY0[3]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **15** | Position of the 3rd light (z), as given by `unity_4LightPosZ0[3]`, **in the MeshRenderer's local space**.                                                                                                                                                |          |
| **16** | Color of the 0th light (r), as given by `unity_LightColor[0].x`.                                                                                                                                                                                         |          |
| **17** | Color of the 0th light (g), as given by `unity_LightColor[0].y`.                                                                                                                                                                                         |          |
| **18** | Color of the 0th light (b), as given by `unity_LightColor[0].z`.                                                                                                                                                                                         |          |
| **19** | Color of the 0th light (a), as given by `unity_LightColor[0].w`.                                                                                                                                                                                         |          |
| **20** | Color of the 1st light (r), as given by `unity_LightColor[1].x`.                                                                                                                                                                                         |          |
| **21** | Color of the 1st light (g), as given by `unity_LightColor[1].y`.                                                                                                                                                                                         |          |
| **22** | Color of the 1st light (b), as given by `unity_LightColor[1].z`.                                                                                                                                                                                         |          |
| **23** | Color of the 1st light (a), as given by `unity_LightColor[1].w`.                                                                                                                                                                                         |          |
| **24** | Color of the 2nd light (r), as given by `unity_LightColor[2].x`.                                                                                                                                                                                         |          |
| **25** | Color of the 2nd light (g), as given by `unity_LightColor[2].y`.                                                                                                                                                                                         |          |
| **26** | Color of the 2nd light (b), as given by `unity_LightColor[2].z`.                                                                                                                                                                                         |          |
| **27** | Color of the 2nd light (a), as given by `unity_LightColor[2].w`.                                                                                                                                                                                         |          |
| **28** | *Supposed to be* the color of the 3rd light (r), as given by `unity_LightColor[3].x`. See notes below\*                                                                                                                                                  |          |
| **29** | *Supposed to be* the color of the 3rd light (g), as given by `unity_LightColor[3].y`. See notes below\*                                                                                                                                                  |          |
| **30** | *Supposed to be* the color of the 3rd light (b), as given by `unity_LightColor[3].z`. See notes below\*                                                                                                                                                  |          |
| **31** | *Supposed to be* the color of the 3rd light (a), as given by `unity_LightColor[3].w`. See notes below\*                                                                                                                                                  |          |
| **32** | Attenuation of the 0th light, as given by `unity_4LightAtten0[0]`.                                                                                                                                                                                       |          |
| **33** | Attenuation of the 1st light, as given by `unity_4LightAtten0[1]`.                                                                                                                                                                                       |          |
| **34** | Attenuation of the 2nd light, as given by `unity_4LightAtten0[2]`.                                                                                                                                                                                       |          |
| **35** | Attenuation of the 3rd light, as given by `unity_4LightAtten0[3]`.                                                                                                                                                                                       |          |
| **36** | Position of the camera (x) in world space.                                                                                                                                                                                                               | \>=1.1.0 |
| **37** | Position of the camera (y) in world space.                                                                                                                                                                                                               | \>=1.1.0 |
| **38** | Position of the camera (z) in world space.                                                                                                                                                                                                               | \>=1.1.0 |
| **39** | Euler angles of the camera (x) in world space, in degrees, using the ZXY rotation order (same as [Unity](https://docs.unity3d.com/ScriptReference/Quaternion.Euler.html)).                                                                               | \>=1.1.0 |
| **40** | Euler angles of the camera (y) in world space, in degrees, using the ZXY rotation order (same as [Unity](https://docs.unity3d.com/ScriptReference/Quaternion.Euler.html)).                                                                               | \>=1.1.0 |
| **41** | Euler angles of the camera (y) in world space, in degrees, using the ZXY rotation order (same as [Unity](https://docs.unity3d.com/ScriptReference/Quaternion.Euler.html)).                                                                               | \>=1.1.0 |
| **42** | Nonzero opaque identifier for the selected SPS2 socket, derived from its player ID and unique ID. Zero means no validated SPS2 socket frame is available.                                                                                                | \>=1.2.0 |
| **43** | Selected SPS2 socket forward axis (x) in encoder-local space.                                                                                                                                                                                             | \>=1.2.0 |
| **44** | Selected SPS2 socket forward axis (y) in encoder-local space.                                                                                                                                                                                             | \>=1.2.0 |
| **45** | Selected SPS2 socket forward axis (z) in encoder-local space.                                                                                                                                                                                             | \>=1.2.0 |
| **46** | Selected SPS2 socket frame-up axis (x) in encoder-local space.                                                                                                                                                                                            | \>=1.2.0 |
| **47** | Selected SPS2 socket frame-up axis (y) in encoder-local space.                                                                                                                                                                                            | \>=1.2.0 |
| **48** | Selected SPS2 socket frame-up axis (z) in encoder-local space.                                                                                                                                                                                            | \>=1.2.0 |
| **49** | Selected SPS2 socket flags.                                                                                                                                                                                                                               | \>=1.2.0 |
| **50** | Selected SPS2 socket scalar world scale.                                                                                                                                                                                                                  | \>=1.2.0 |
| **51** | Canary. Equal to 1431677610, which results in a checkerboard pattern in binary. This is used to help solve alignment issues.<br/>This value can change in the future. The program will not check if this value is equal, but it is part of the checksum. |          |

\* *The value of `unity_LightColor[3]` may be disrupted if the scene contains a directional light due to a Unity quirk, so this value may not be trusted to detect point lights.*

### Checksum

The data can easily get corrupted if a SteamVR overlay overlaps the data region, or in some cases, game UI, post-processing, some rare transparent objects,
or special shaders may interfere with the data region.

When this happens, we need to detect this happening and disregard any decoded data.

To do this, we calculate a CRC-32 hash in the shader that this program will check.

If the check fails, we reuse the last known valid data.

The CRC-32 hash is based on groups 1 to 51 (inclusive). Protocol 1.2 uses groups 42 to 50 for the SPS2 target frame:

- 42 is a nonzero opaque identifier for the selected socket, derived from the SPS2 player ID and socket unique ID. Zero means that no validated SPS2 socket frame is available.
- 43 to 45 contain the selected socket forward axis.
- 46 to 48 contain the selected socket frame-up axis.
- 49 contains the SPS2 socket flags.
- 50 contains the selected socket's scalar world scale.

For SPS2 packets, the standard light fields remain a legacy-compatible target representation. Light 0 is the
selected socket root and Light 1 is a synthetic front marker positioned so that `normalize(root - front)` reproduces the
socket forward vector. Lights 2 and 3 are zeroed and disabled so real legacy lights cannot compete with the selected SPS2
target. The desktop program always runs its ordinary light target-selection algorithm first, then uses a valid SPS2 extension
to authoritatively augment that same target. At the SPS2 boundary these axes are forward and frame-up; the application
maps them to its established normal and tangent fields.

The SPS2 encoder always reserves synthetic Lights 2 and 3 and serializes exact zero for their local position, RGBA, and
attenuation, even when no valid SPS2 target is selected. Their alpha is zero, so they are disabled. When no valid SPS2
target is selected, only Lights 0 and 1 retain Unity vertex-light fallback values. The ordinary non-SPS2 encoder continues
to expose Unity's historical values for all four light slots.

The VRCFury prefab uses a separate static Position System shader which includes the modular SPS2 atlas readers from the
officially installed VRCFury package. It reads `_VFGridFinal`, uses the SPS cell dictionary to enumerate occupied groups,
validates socket cells, and selects the socket nearest to the encoder origin. The atlas scan runs in the vertex stage so
the fragment-stage packet and CRC serialization do not repeat it for every bit.
The selection is recomputed for every encoder draw and has no persistent target identity, so when two sockets exchange
distance order the observer switches to the newly nearest valid socket.
The identifier in group 42 remains stable while the same SPS2 `(player ID, unique ID)` pair is selected. It is intended
for detecting target changes, not for recovering either source identifier; as a 32-bit hash, collisions are possible.

The SPS2 integration requires VRCFury to be installed in the Unity project, but it does not require any VRCFury
components to be used on the avatar. If VRCFury is not installed, the dedicated VRCFury SPS2 shader will fail to
compile. The ordinary non-SPS2 encoder shader used by the Modular Avatar and ChilloutVR prefabs does not compile any
VRCFury code and remains available.

The shader references VRCFury's installed `sps_cell_layout.cginc` module, which transitively provides the texture and
codec helpers it needs, without copying or modifying them. This makes that include path a compile-time dependency for the
VRCFury prefab. If no validated SPS2 socket is visible, the VRCFury shader exports Unity's four vertex lights unchanged.
Radius-offset is exported as a socket flag; the raw socket position is retained because a read-only observer has no plug
radius with which to apply it.
Socket world scale is accepted only when it is finite, positive, and between `1e-6` and `1e6`. If it is invalid, the
shader keeps the valid synthetic root/front lights but clears words 42 to 50, so the desktop safely uses ordinary legacy
light interpretation for that frame.
These include paths and helper names are internal to VRCFury 1.1417.0 rather than a documented stable integration API.

VRCFury 1.1417.0 explicitly excludes its SPS2 socket-marker and resolver shaders from Unity's Metal renderer. The Position
System SPS2 observer likewise excludes Metal and targets the Windows Direct3D rendering path only.

### WebSockets as an alternative input system

If *WebSockets* support is enabled, we will expose a websocket on port **56247** at url `ws://localhost:56247/ws`

Send the following string to it that represents an interpreted position and normal:
```text
PositionSystemInterpreted PositionX PositionY PositionZ NormalX NormalY NormalZ
```
- *PositionX*, *PositionY*, *PositionZ* is the position in local space, where (0, 0, 0) is the bottommost center, and (0, 1, 0) is the uppermost center.
- *NormalX*, *NormalY*, *NormalZ* is the direction, represented as a vector of length 1. It doesn't matter if you don't make it length 1, we will normalize it anyway.

While you're at it, you can also submit the tangent, which can be useful to define the twist, but this is optional:
```text
PositionSystemInterpreted PositionX PositionY PositionZ NormalX NormalY NormalZ TangentX TangentY TangentZ
```
- *TangentX*, *TangentY*, *TangentZ* is the tangent (which is a vector perpendicular to the direction), represented as a vector of length 1. It doesn't matter if you don't make it length 1, we will normalize it anyway.
  
- TODO: Clarify the expected coordinate space.
- TODO: Clarify where the direction should point to.

Here is a short Python Jupyter notebook that sends a message to this service:
```python
#%%
!pip install websockets nest_asyncio

import asyncio
import websockets
import nest_asyncio

# This allows asyncio to work properly in Jupyter
nest_asyncio.apply()

async def send_message():
    uri = "ws://localhost:56247/ws"
    async with websockets.connect(uri) as websocket:
        message = "PositionSystemInterpreted 0.0 0.1 0.2 0.3 0.4 0.5"
        await websocket.send(message)
        print(f"Sent message: {message}")

await send_message()
```

### Third-party acknowledgements

Third party acknowledgements can also be found in the thirdparty-licenses/ThirdParty/ subfolder:
- For the full license text of the third party dependencies, open thirdparty-licenses/THIRDPARTY-LICENSES/ folder

Included in source code form and DLLs:
- ImGui.NET SampleProgram @ https://github.com/ImGuiNET/ImGui.NET/tree/master/src/ImGui.NET.SampleProgram ([MIT license](https://github.com/ImGuiNET/ImGui.NET/blob/master/LICENSE)) by Eric Mellino and ImGui.NET contributors
- OpenVR API @ https://github.com/ValveSoftware/openvr ([BSD-3-Clause license](https://github.com/ValveSoftware/openvr/blob/master/LICENSE)) by Valve Corporation
- openvr-screengrab @ https://github.com/cnlohr/openvr-screengrab ([MIT license](https://github.com/cnlohr/openvr-screengrab/blob/master/LICENSE)) by CNLohr

Dependencies included through NuGet:
- Dear ImGui @ https://github.com/ocornut/imgui ([MIT license](https://github.com/ocornut/imgui/blob/master/LICENSE.txt)) by Omar Cornut
- ImGui.NET @ https://github.com/ImGuiNET/ImGui.NET ([MIT license](https://github.com/ImGuiNET/ImGui.NET/blob/master/LICENSE)) by Eric Mellino and ImGui.NET contributors
- Veldrid @ https://github.com/veldrid/veldrid ([MIT license](https://github.com/veldrid/veldrid/blob/master/LICENSE)) by Eric Mellino and Veldrid contributors
- Vortice.Windows @ https://github.com/amerkoleci/Vortice.Windows ([MIT license](https://github.com/amerkoleci/Vortice.Windows/blob/main/LICENSE)) by Amer Koleci and Contributors
- Buttplug @ https://github.com/buttplugio/buttplug-csharp ([BSD 3-Clause](https://github.com/buttplugio/buttplug-csharp/blob/master/LICENSE)) by Nonpolynomial Labs, LLC
- (there may be other implicit packages)

Asset dependencies:
- ProggyClean font @ http://www.proggyfonts.net/ ([MIT License (According to https://github.com/ocornut/imgui/blob/master/docs/FONTS.md#creditslicenses-for-fonts-included-in-repository)](https://github.com/ocornut/imgui/blob/master/docs/FONTS.md#creditslicenses-for-fonts-included-in-repository)) by Tristan Grimmer
- Noto Sans TC @ https://github.com/google/fonts ([SIL Open Font License](https://openfontlicense.org)) by Adobe and the Noto project authors
