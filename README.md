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

## Use it in your project

1. Copy `Assets/EditorTools/EditorControls/` into your project's `Assets/` folder.
2. Drag `~EditorControls.prefab` into your XR scene.
3. Press Play.

There's nothing to wire up. It finds the camera, the event system and the controller pointer itself. It runs only in the Editor (`#if UNITY_EDITOR`), so nothing ends up in a headset build.

## Requirements

- Unity 2022.3 (built with 2022.3.58f1)
- XR Interaction Toolkit 3.x (built with 3.1.2) and an XR Origin rig in the scene
- Either input setting: the new Input System or the old Input Manager. `EditorControlsInput.cs` reads whichever one is on
- Developed on macOS. Grip uses the Command key, which is the Windows key on a PC

## What's inside

| File | Job |
|---|---|
| `EditorMouseClickTester.cs` | Turns mouse clicks into controller selects and UI presses, draws the laser, handles grab, teleport and snap turn |
| `EditorMouseRotationTester.cs` | Alt-look. Switches UI input off while you look, so moving the view never clicks anything |
| `EditorControlsInput.cs` | Reads the mouse and keyboard under either input setting |
| `EditorScriptedPointer.cs` | Lets a script drive the pointer for one frame, for automated UI checks |

The controls don't know anything about a particular app: no state manager, no step system. A project that needs extra behaviour (logging, for example) adds its own script next to these.

## License

MIT. See [LICENSE](LICENSE).
