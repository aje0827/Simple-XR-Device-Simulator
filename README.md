# Simple XR Device Simulator

Test an XR Interaction Toolkit scene in the Unity Editor with a mouse or trackpad and a keyboard.

<img src="docs/grab-and-turn.gif" width="100%" alt="Grabbing a cylinder with Command and rotating it with Control in the XR Interaction Toolkit demo scene">

| Look around and teleport | Scale what you're holding |
|---|---|
| ![Looking around with Option, then teleporting with a right click](docs/look-and-teleport.gif) | ![Scaling a held cylinder with Command and scroll](docs/scale.gif) |

Built for quick checks while developing XR apps. Putting a headset on and off for every small change is slow, and Unity's XR Device Simulator covers every headset input, so you press Tab or the bracket keys to switch between moving your head and each controller. This package keeps only the controls you use most, on keys that are easy to reach.

Press Play and the mouse becomes your controller. You can look around, click buttons, grab, rotate, scale and teleport.

It works with the XR Interaction Toolkit interactables and UI already in your scene. They respond the same way they do to a real controller, so you can test them as they are.

## Install

In Unity, open **Window > Package Manager**, click **+**, choose **Add package from git URL**, and paste this link:

```
https://github.com/aje0827/Simple-XR-Device-Simulator.git?path=Assets/SimpleXRDeviceSimulator
```

Use this exact link. The page address from your browser won't install.

To stay on one version, add its tag to the end of the link, like `#v3.0.0`. What changed in each version is in the [changelog](Assets/SimpleXRDeviceSimulator/CHANGELOG.md).

If Unity asks to enable the Input System's backends and restart, click Yes. The package reads the mouse and keyboard through it.



**Try it:** click **Tools > Simple XR Device Simulator > Open Basic Scene** and press Play. The controls are listed on a panel in the scene.

**Use it in your scene:** drag `Simple XR Device Simulator.prefab` (under **Packages > Simple XR Device Simulator**) into your scene and press Play. It finds the camera and the event system on its own. It only runs in the Editor, so it has no effect on a headset build.

## What it supports

- XR Interaction Toolkit scenes with an XR Origin rig.
- One controller. Actions that take two hands in the headset, like scaling, get a key instead.
- A left click does only what the trigger does, and Command only what the grip does, through the toolkit's own controller ray. If a button or a grab is broken in the headset, it's broken here too. For example, UI can't be pressed in a scene whose EventSystem has no XR UI Input Module, in the headset or here. The package warns about that in the Console and the Game view when you press Play. If your rig grabs with the trigger (its Select is set to the same action as Activate), a left click grabs too, as the trigger does in the headset.
- The right controller's A and B buttons, on 1 and 2. Anything bound to the right controller's `primaryButton` or `secondaryButton` works as it is.



## Not supported

- Other rigs, like Meta's Interaction SDK.
- A second controller (so no X and Y buttons), hand tracking and climbing. For those, use Unity's XR Device Simulator.



## Controls

The mouse points the controller. A laser shows where it's pointing, and hover highlights show up as they do in the headset.


| Action      | Keys                                         | Same as in the headset                                                   |
| ----------- | -------------------------------------------- | ------------------------------------------------------------------------ |
| Look around | Hold Option / Alt and move the mouse         | Turning your head                                                        |
| Trigger     | Left click                                   | Trigger: press UI buttons, and use what you're holding                   |
| Grab        | Hold Command / Windows                       | Grip: the object follows the mouse, let go to drop it                    |
| Rotate      | Command / Windows + Control + move the mouse | Turning your wrist. Add Shift to roll                                    |
| Scale       | Command / Windows + scroll                   | Two-handed scale, within the limits set on the object's grab transformer |
| Teleport    | Hold right click, then release               | Thumbstick forward. The arc turns red where you can't land               |
| Snap turn   | Left / Right arrow keys                      | Thumbstick left / right                                                  |
| A button    | Hold 1                                       | A on the right controller                                                |
| B button    | Hold 2                                       | B on the right controller                                                |


On a Mac trackpad, a two-finger click is the right click and a two-finger swipe is the scroll. Other trackpads haven't been tested.

Recording a demo? Turn on **Show Input Label** on the prefab. It shows the control in use in the corner of the Game view and draws the cursor and a ring on each click, since Unity's Recorder doesn't capture the real cursor. The GIFs above were made that way, in the XR Interaction Toolkit's demo scene.

## Requirements

- Unity 2022.3 (built with 2022.3.58f1)
- An XR Interaction Toolkit scene with an XR Origin rig
- The XR Interaction Toolkit 3.x and the Input System. Package Manager installs both with this package if they're missing
- Built and tested on macOS. On Windows, the Windows key takes the place of Command. Windows hasn't been tested yet



## License

MIT. See [LICENSE](LICENSE).