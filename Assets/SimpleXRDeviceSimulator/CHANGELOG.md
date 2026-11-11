# Changelog

## [3.0.0] - 2026-11-11

### Changed
- The package folder is now `Assets/SimpleXRDeviceSimulator`, so the install link ends in `?path=Assets/SimpleXRDeviceSimulator`.
- The namespace is now `SimpleXRDeviceSimulator` (was `XREditorControls`). Code that uses the package needs its `using` line changed.

## [2.1.1] - 2026-11-03

### Changed
- A left click fires the trigger on a pointed-at item, as the headset does.
- One click presses UI once.
- A left click grabs when the rig grabs with the trigger, so menus move as they do in the headset.
- The panel warning skips panels with nothing to press, and the Show Scene Warnings checkbox hides the on-screen box.

## [2.1.0] - 2026-10-25

### Added
- A and B buttons: hold 1 and 2 to press the right controller's primary and secondary buttons.

## [2.0.0] - 2026-10-25

### Changed
- A left click does only what the trigger does, through the toolkit's own controller ray, so UI and grabs work on the laptop only when they work in the headset.

## [1.0.0] - 2026-10-24

### Added
- First release: look around, click, grab, rotate, scale, teleport and snap turn with a mouse and keyboard.
- Basic Scene sample.
