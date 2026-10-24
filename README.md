# EditorTools-Unity: XR editor controls

A lightweight way to test an XR scene in the Unity Editor with a mouse or trackpad and a keyboard, without a headset.

I find Unity's XR Device Simulator hard to control, and it has far more inputs than I need to test in my apps. I built this to check small changes in the VR training apps I make without putting the headset on and off each time.

It simulates one controller and looking around. The mouse is the controller's pointer, Alt turns your head, and a few keys cover the trigger, grip and thumbstick. Press Play and you can point at things, click buttons and hotspots, grab objects, teleport and turn. Everything goes through the toolkit's own interaction manager, so an object reacts the same way it does with a real controller, and nothing needs to be added to your interactables.

## What it covers, and what it doesn't

- One controller, driven by the mouse, and head look with Alt.
- Things that take two hands in the headset get a one-hand shortcut instead. Scaling, for example, is a two-controller gesture in the headset; here it's Command + scroll on the object you're holding.
- No second controller, no hand tracking, and no buttons beyond trigger, grip and thumbstick. If a scene depends on those, the XR Device Simulator is the better tool.

## Controls

| Input | Does what the headset does with |
|---|---|
| Move the mouse | Point the controller. Hover highlights and outlines show up as they would in the headset |
| Left click | Trigger: press UI buttons, hotspots and other interactables. A laser shows where you're pointing |
| Hold Alt (Option on Mac) and move the mouse | Look around |
| Hold Command | Grip: grab what you're pointing at. It follows the mouse; let go to drop it |
| Command + right mouse button + move | Turn the held object |
| Command + scroll | Scale the held object, within the limits set on its grab transformer |
| Hold right click, release | Thumbstick forward: teleport arc, then jump (red means nowhere to land) |
| Left / Right arrow keys | Thumbstick left / right: snap turn |

Works with a mouse or a Mac trackpad: a two-finger click is the right click, and a two-finger swipe is the scroll. Other trackpads haven't been tested.

## Install

In Unity, open **Window > Package Manager**, click **+**, choose **Add package from git URL**, and paste:

```
https://github.com/aje0827/EditorTools-Unity.git?path=Assets/EditorTools/EditorControls
```

Then drag `~EditorControls.prefab` (under Packages > XR Editor Controls) into your XR scene and press Play.

There's nothing to wire up. It finds the camera, the event system and the controller pointer itself. The controls only run in the Editor, so they do nothing in a headset build.

You can also copy the `Assets/EditorTools/EditorControls/` folder into your project instead.

## Requirements

- Unity 2022.3 (built with 2022.3.58f1)
- XR Interaction Toolkit 3.x (built with 3.1.2) and an XR Origin rig in the scene
- The Input System (the XR Interaction Toolkit needs it too)
- Developed on macOS. Grip uses the Command key, which is the Windows key on a PC

## What's inside

One script per job, all in the `XREditorControls` namespace:

| File | Job |
|---|---|
| `EditorMouseClickTester.cs` | The component on the prefab. Holds the settings and runs the parts below in order |
| `MousePointer.cs` | The ray from the camera through the cursor, what it hits, and the interactors that act for the controller |
| `MouseUiClick.cs` | Pressing UI: a press counts on release, on the same control, like a controller's trigger |
| `MouseWorldClick.cs` | Pressing 3D interactables through the XR Interaction Manager |
| `MouseGrab.cs` | Grip: grab, turn and scale through the toolkit's own held select |
| `MouseLocomotion.cs` | Thumbstick: teleport arc and snap turn |
| `MouseLaser.cs` | The laser and arc you see in the Game view |
| `EditorMouseRotationTester.cs` | Alt-look. Switches UI input off while you look, so moving the view never clicks anything |
| `EditorControlsInput.cs` | Reads the mouse and keyboard through the Input System |
| `EditorScriptedPointer.cs`, `ScriptedMouseClick.cs` | Let a script drive the pointer or send a real click, for automated UI checks |

An object without an XR interactable can still take mouse clicks by implementing `IClickable` (add `using XREditorControls;`). The controls don't know anything about a particular app: no state manager, no step system.

## License

MIT. See [LICENSE](LICENSE).
