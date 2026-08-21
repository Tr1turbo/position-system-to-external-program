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

### Shader protocol 2.0.0

Protocol 2 converts all shader sources into the same entity records. Classic DPS/SPS1 lights and SPS2 atlas cells therefore share one data layout; consumers do not need separate position/orientation fields for each source. The data is still exactly 52 IEEE 754/binary32 words, and the CRC-32 still covers words 1 through 51.

| Words | Field |
|-------|-------|
| **0** | CRC-32 of words 1 through 51. |
| **1** | Unity time (`_Time.y`) as float32. |
| **2** | Position System protocol identifier `1366692562`. |
| **3** | Protocol 2 version `2000000` (2.0.0). |
| **4** | Presence mask described below. |
| **5-7** | Camera world position `(x, y, z)`. |
| **8-10** | Camera world Euler angles `(x, y, z)` in degrees, using Unity's ZXY rotation order. |
| **11-26** | Protocol 2 entity slot 0. The encoder normally places the nearest eligible socket here. |
| **27-42** | Protocol 2 entity slot 1. The SPS2 encoder normally places the nearest atlas plug here. |
| **43-50** | Reserved; must be zero. |
| **51** | Canary `1431677610`. |

Each entity slot is 16 words:

| Offset | Field |
|--------|-------|
| **+0** | Descriptor: entity kind in bits 0-7, source kind in bits 8-15, bits 16-31 zero. |
| **+1** | Owner/player identity. This is opaque protocol data. |
| **+2** | Entity identity. This is opaque protocol data. |
| **+3 to +5** | Position `(x, y, z)` in the encoder renderer's local space. |
| **+6 to +9** | Orientation, using one of the representations below. |
| **+10** | Scalar world scale. |
| **+11 to +15** | Reserved; must be zero. |

Source kinds are `0 = Unknown`, `1 = ClassicLight`, `2 = ClassicSps1Light`, `3 = Sps2CompatibilityLight`, and `4 = Sps2Atlas`. Entity kinds are `0 = Unknown`, `1 = Hole`, `2 = Ring`, `3 = OneWayRing`, and `16 = Plug`. Unknown descriptors remain available for diagnostics but are not selected as robotics targets.

The descriptor intentionally contains both source and entity kind. Entity kind defines behavior, while source kind records how the entity record was produced. A plug does not need a separate target kind: it uses the same record and frame conventions as a socket, with entity kind `Plug`.

Presence-mask bits are grouped by slot:

| Bit | Meaning |
|-----|---------|
| **0-5** | Slot 0: present, owner ID, entity ID, forward, up/full quaternion, explicit scale. |
| **6-11** | Slot 1: the same six flags in the same order. |
| **12** | Camera position is present. |
| **13** | Camera Euler rotation is present. |
| **14-31** | Reserved; must be zero. |

Orientation always reserves four words:

- No orientation: all four words are canonical quiet NaN (`0x7FC00000`).
- Forward only: words +6 to +8 are normalized forward `(x, y, z)` and word +9 is canonical NaN. The forward bit is set and the up bit is clear.
- Full frame: words +6 to +9 are a normalized quaternion `(x, y, z, w)`. Both forward and up bits are set. Quaternion `w` is not reconstructed or omitted.

The Protocol 2 frame uses local `+Z` as forward and local `+Y` as up. The desktop's existing socket interpretation maps this to `normal = -forward` and `tangent = up`.

Canonical NaN means absent or invalid data. An absent/invalid slot encodes NaN for position, orientation, and scale. A valid record whose source simply does not define scale encodes `1.0` with the explicit-scale bit clear. If a source supplied a malformed scale, the entity position remains valid but scale is canonical NaN with the explicit-scale bit clear. A valid record with no orientation likewise keeps its position and scale, but encodes canonical NaN in all four orientation words. Non-finite or degenerate position data invalidates the entity; malformed optional orientation or scale data is not forwarded.

The default encoder policy is:

1. Slot 0 contains the nearest eligible socket.
2. Slot 1 contains the nearest SPS2 atlas plug.

The wire format itself is generic and permits any pair of entities. The desktop retains both slots, but its current robotics adapter selects the nearest known socket-like entity and ignores plugs as motion targets. Ties prefer the lower slot number.

Classic light decoding preserves both established DPS root/front channels: root channels 1/2 pair with front channel 5, while root channels 3/4 pair with front channel 6. Producer family is classified from the light-range suffix: ordinary DPS/classic ranges are `ClassicLight`, suffix `.0002` is `ClassicSps1Light`, and suffixes `.0005` through `.0007` are `Sps2CompatibilityLight`. Roots pair only with front markers from the same source family, and the existing maximum root/front distance is retained. Ordinary DPS rings map to one-sided `OneWayRing`; SPS1 double-sided ring compatibility data maps to `Ring`.

The ordinary encoder permits all three light families. The SPS2 atlas encoder permits `ClassicLight`, `ClassicSps1Light`, and `Sps2Atlas`, but excludes `Sps2CompatibilityLight` roots and fronts so compatibility lights cannot compete with their authoritative atlas socket or provide orientation to another family. The nearest eligible socket wins; an exact tie may prefer the richer atlas record.

SPS2 socket flags map as follows:

| SPS2 Hole | SPS2 DoubleSided | Entity kind |
|-----------|------------------|-------------|
| 1 | 0 | `Hole` |
| 0 | 1 | `Ring` |
| 0 | 0 | `OneWayRing` |
| 1 | 1 | Invalid/ambiguous; rejected |

Other SPS2 target flags are not serialized because they do not change the Protocol 2 position frame used by this application.

The desktop accepts the Protocol 1 version family (`1_xxx_xxx`, including upstream's `1_001_001` and the earlier SPS2 `1_002_000`) and Protocol 2 version `2_000_000`. Protocol 1 retains its original four-light and camera-Euler layout unchanged; Protocol 2 is emitted by the updated Unity encoders. The SPS2 shader reads VRCFury's installed SPS2 includes and atlas; those files are not copied into this package. SPS2 atlas validation requires the supported VRCFury version and a Windows Direct3D environment. The ordinary encoder remains independent of VRCFury.

The WebSocket service is a separate interpreted-input path and is unchanged by shader protocol 2.0.

### Protocol 1 compatibility

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
| **41** | Euler angles of the camera (z) in world space, in degrees, using the ZXY rotation order (same as [Unity](https://docs.unity3d.com/ScriptReference/Quaternion.Euler.html)).                                                                               | \>=1.1.0 |
| **42-50** | Reserved; zero. | 1.1.0 |
| **51** | Canary. Equal to 1431677610, which results in a checkerboard pattern in binary. This is used to help solve alignment issues.<br/>This value can change in the future. The program will not check if this value is equal, but it is part of the checksum. |          |

\* *The value of `unity_LightColor[3]` may be disrupted if the scene contains a directional light due to a Unity quirk, so this value may not be trusted to detect point lights.*

### Checksum

The data can easily get corrupted if a SteamVR overlay overlaps the data region, or in some cases, game UI, post-processing, some rare transparent objects,
or special shaders may interfere with the data region.

When this happens, we need to detect this happening and disregard any decoded data.

To do this, we calculate a CRC-32 hash in the shader that this program will check.

If the check fails, we reuse the last known valid data.

The CRC-32 hash is based on groups 1 to 51 inclusive.

## SPS2 Integration

The VRCFury prefab adds VRChat SPS2 socket and plug support while preserving classic DPS/SPS1 light input. The current encoder exports all recognized sources through the Protocol 2 entity slots described above.

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
