# Simple XR Device Simulator

Test an XR Interaction Toolkit scene in the Unity Editor with a mouse or trackpad and a keyboard.

![Grabbing a cylinder with Command and rotating it with Control in the XR Interaction Toolkit demo scene](docs/grab-and-turn.gif)

| Look around and teleport | Scale what you're holding |
|---|---|
| ![Looking around with Option, then teleporting with a right click](docs/look-and-teleport.gif) | ![Scaling a held cylinder with Command and scroll](docs/scale.gif) |

Built for quick checks while developing XR apps. Press Play and the mouse becomes your controller. You can look around, click buttons, grab, rotate, scale and teleport.

It works with the XR Interaction Toolkit interactables and UI already in your scene. They respond the same way they do to a real controller, so you can test them as they are.

## Install

In Unity, open **Window > Package Manager**, click **+**, choose **Add package from git URL**, and paste this link:

```
https://github.com/aje0827/Simple-XR-Device-Simulator.git?path=Assets/EditorTools/EditorControls
```

Use this exact link. The page address from your browser won't install.

If Unity asks to enable the Input System's backends and restart, click Yes. The package reads the mouse and keyboard through it.



**Try it:** click **Tools > Simple XR Device Simulator > Open Basic Scene** and press Play. The controls are listed on a panel in the scene.

**Use it in your scene:** drag `Simple XR Device Simulator.prefab` (under **Packages > Simple XR Device Simulator**) into your scene and press Play. It finds the camera, the event system and the controller on its own. It only runs in the Editor, so it has no effect on a headset build.

## What it supports

- XR Interaction Toolkit scenes with an XR Origin rig.
- One controller. Actions that take two hands in the headset, like scaling, get a key instead.



## Not supported

- Other rigs, like Meta's Interaction SDK.
- A second controller, hand tracking and climbing. For those, use Unity's XR Device Simulator.



## Controls


| Look around and teleport                                                                       | Scale what you're holding                                          |
| ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| ![Looking around with Option, then teleporting with a right click](docs/look-and-teleport.gif) | ![Scaling a held cylinder with Command and scroll](docs/scale.gif) |


The mouse points the controller. A laser shows where it's pointing, and hover highlights show up as they do in the headset.


| Action      | Keys                                         | Same as in the headset                                                   |
| ----------- | -------------------------------------------- | ------------------------------------------------------------------------ |
| Look around | Hold Option / Alt and move the mouse         | Turning your head                                                        |
| Trigger     | Left click                                   | Trigger: press buttons and interactables                                 |
| Grab        | Hold Command / Windows                       | Grip: the object follows the mouse, let go to drop it                    |
| Rotate      | Command / Windows + Control + move the mouse | Turning your wrist. Add Shift to roll                                    |
| Scale       | Command / Windows + scroll                   | Two-handed scale, within the limits set on the object's grab transformer |
| Teleport    | Hold right click, then release               | Thumbstick forward. The arc turns red where you can't land               |
| Snap turn   | Left / Right arrow keys                      | Thumbstick left / right                                                  |


On a Mac trackpad, a two-finger click is the right click and a two-finger swipe is the scroll. Other trackpads haven't been tested.

Recording a demo? Turn on **Show Input Label** on the prefab. It shows the control in use in the corner of the Game view and draws the cursor and a ring on each click, since Unity's Recorder doesn't capture the real cursor. The GIFs above were made that way, in the XR Interaction Toolkit's demo scene.

## Requirements

- Unity 2022.3 (built with 2022.3.58f1)
- An XR Interaction Toolkit scene with an XR Origin rig
- The XR Interaction Toolkit 3.x and the Input System. Package Manager installs both with this package if they're missing
- Built and tested on macOS. On Windows, the Windows key takes the place of Command. Windows hasn't been tested yet



## License

MIT. See [LICENSE](LICENSE).