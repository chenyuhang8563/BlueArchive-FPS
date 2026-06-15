# Agent Guide

## Project Overview

This is a Unity 6 project for a third-person/FPS prototype named `Blue Archive FPS`.

- Unity version: `6000.0.28f1`
- Product name: `Blue Archive FPS`
- Main build scene: `Assets/Scenes/youga.unity`
- Render pipeline: URP, with `Mobile` and `PC` quality profiles
- Input: Unity Input System is enabled (`activeInputHandler: 2`, both old and new input paths)
- This folder is not currently a Git repository.

Do not treat `Library/`, `Temp/`, `obj/`, `Logs/`, or generated `.csproj` files as source of truth. They are Unity-generated or local build/editor state.

## Source Layout

Primary project code:

- `Assets/Scripts/PlayerMoveTest.cs`
- `Assets/Scripts/PlayerMoveRM.cs`
- `Assets/Scripts/PlayerShooterController.cs`

Starter Assets code and local modifications:

- `Assets/StarterAssets/InputSystem/StarterAssetsInputs.cs`
- `Assets/StarterAssets/ThirdPersonController/Scripts/ThirdPersonController.cs`
- `Assets/StarterAssets/ThirdPersonController/Scripts/ThirdPersonAimController.cs`
- `Assets/StarterAssets/ThirdPersonController/Scripts/BasicRigidBodyPush.cs`
- `Assets/StarterAssets/Mobile/Scripts/**`

Legacy/third-party code:

- `Assets/Standard Assets/Character Controllers/Sources/Scripts/**`
- `Assets/OccaSoftware/Crosshairs/Editor/StartMenu.cs`
- `Assets/Shaders/UnityURPToonLitShaderExample-master/**`

Major asset folders:

- `Assets/Models/`: character and weapon model/material assets, including Chinese-named assets.
- `Assets/Animation/`: controller assets and imported animation packs.
- `Assets/LowPolyAssetBundle/LowPolyFPS/`: low-poly FPS environment assets.
- `Assets/StarterAssets/`: Unity Starter Assets package content.
- `Assets/OccaSoftware/Crosshairs/`: crosshair textures and editor helper.

## Runtime Entry Points

`Assets/Scenes/youga.unity` is the only enabled build scene. The scene contains:

- Main camera and Cinemachine camera objects.
- A `cm` object with PlayerInput-driven bindings.
- A `Top down Cam` virtual camera.
- Basic scene primitives such as `Cube` and `Plane`.

The scene's PlayerInput event wiring currently targets `PlayerMoveTest` for movement, run, and rifle actions.

## Core Gameplay Scripts

### `PlayerMoveTest`

This is the most important custom gameplay controller.

Responsibilities:

- Reads Input System callbacks:
  - `PlayerMove`
  - `PlayerRun`
  - `PlayerAiming`
  - `PlayerarmedRifle`
  - `PlayerLook`
  - `PlayerCrouch`
  - `PlayerJump`
- Drives a `CharacterController` through `OnAnimatorMove`, using root motion while grounded and cached velocity while airborne.
- Tracks posture with `PlayerPosture`: `Crouch`, `Stand`, `Midair`.
- Tracks locomotion with `LocomotionState`: `Idle`, `Walk`, `Run`.
- Tracks arms with `ArmState`: `Normal`, `Aim`.
- Rotates the Cinemachine target from look input.
- Updates animator parameters:
  - `Blend`
  - `Horizontal Speed`
  - `Vertical Speed`
  - `Turn Speed`
  - `Jump Speed`
  - `Feet`
  - `isAiming`
  - `Rifle`
  - `Right Hand Weight`
  - `Left Hand Weight`
- Toggles rifle visibility through `PutGrabRifle(int index)`.
- Copies animator float values into Animation Rigging `TwoBoneIKConstraint` weights.
- Raycasts from screen center for aim/debug targeting.

Maintenance notes:

- `using UnityEngine.Windows;` is present but appears unnecessary.
- The method is named `PlayerarmedRifle`, but the scene references `PlayerArmedRifle`. Unity event method lookup can be case-sensitive; verify this binding in the Inspector before depending on the rifle toggle.
- `AimRayCast` computes `mouseWorldPosition` but the aim rotation logic is commented out.
- `debugTransform`, rifle objects, IK constraints, `CinemachineCameraTarget`, and `aimColliderLayerMask` must be assigned in the Inspector.
- Movement depends heavily on animator parameter names. Renaming controller parameters will break runtime behavior.

### `PlayerMoveRM`

A small root-motion movement test. It reads `PlayerMove` and sets two Chinese-named animator bools:

- forward-walk bool
- backward-walk bool

Use this as an experiment/prototype, not as the main movement controller unless the scene is rewired to it.

### `PlayerShooterController`

Currently empty placeholder script.

## Starter Assets Modifications

### `StarterAssetsInputs`

Mostly standard Starter Assets input state, with one local addition:

- `public bool aim`
- `OnAim(InputValue value)`
- `AimInput(bool newAimState)`

### `ThirdPersonController`

Mostly standard Starter Assets third-person movement:

- CharacterController movement.
- Camera-relative movement.
- Jump/gravity.
- Grounded checks.
- Footstep and landing audio events.

Local addition:

- `Aim()` toggles animator bool `isAim` when `_input.aim` is true, then resets `_input.aim = false`.

### `ThirdPersonAimController`

Enables/disables an aim virtual camera based on `StarterAssetsInputs.aim`.

Important behavior difference:

- `ThirdPersonController.Aim()` treats `aim` as a one-frame toggle.
- `ThirdPersonAimController` treats `aim` as a held state.

If both scripts are active on the same character, aim camera behavior may flicker or fail to stay active because `ThirdPersonController` resets `aim` to false.

### Mobile Input Scripts

The mobile input scripts are standard virtual input adapters:

- `UICanvasControllerInput` forwards virtual move/look/jump/sprint to `StarterAssetsInputs`.
- `UIVirtualButton` emits button down/up/click UnityEvents.
- `UIVirtualJoystick` emits normalized stick vectors.
- `UIVirtualTouchZone` emits drag delta vectors.
- `MobileDisableAutoSwitchControls` disables PlayerInput auto-switching on iOS/Android when the Input System is enabled.

## Input Actions

Main action asset:

- `Assets/InputSystem_Actions.inputactions`

Important `Player` actions:

- `Move`
- `Look`
- `Attack`
- `Interact`
- `Crouch`
- `Jump`
- `Previous`
- `Next`
- `Sprint`
- `Run`
- `Aim`
- `Rifle`

Scene wiring observed in `youga.unity`:

- `Player/Move` -> `PlayerMoveTest.PlayerMove`
- `Player/Run` -> `PlayerMoveTest.PlayerRun`
- `Player/Rifle` -> scene currently references `PlayerMoveTest.PlayerArmedRifle`

Also present:

- `Assets/StarterAssets/InputSystem/StarterAssets.inputactions`

Keep action names and PlayerInput event method names aligned. If actions are renamed in the Input Actions editor, update both callback methods and scene bindings.

## Rendering And Packages

Key packages from `Packages/manifest.json`:

- `com.unity.render-pipelines.universal` `17.0.3`
- `com.unity.inputsystem` `1.11.2`
- `com.unity.cinemachine` `3.1.3`
- `com.unity.animation.rigging` `1.3.0`
- `com.unity.ai.navigation` `2.0.4`
- `com.unity.probuilder` `6.0.5`
- `com.unity.test-framework` `1.4.5`

Quality profiles:

- `Mobile`
- `PC`

The global graphics pipeline points at the PC URP asset.

## Third-Party And Legacy Code

`Assets/OccaSoftware/Crosshairs/Editor/StartMenu.cs` is an editor-only window that opens a vendor start menu and external links. Avoid changing it unless updating/removing the crosshair package.

`Assets/Standard Assets/Character Controllers/Sources/Scripts/` contains old Unity Standard Assets FPS controller code:

- `MouseLook.cs`
- `FPSInputController.js`
- `CharacterMotor.js`

These use the old `Input` API and UnityScript. They are legacy assets and should not be used as the basis for new gameplay unless there is a deliberate migration plan.

## Conventions For Future Agents

- Prefer the Unity Input System for new gameplay input.
- Prefer C# scripts under `Assets/Scripts/` for project-specific gameplay.
- Keep imported package/sample files isolated unless a change is explicitly about that package.
- Preserve `.meta` files when moving or renaming assets.
- Avoid editing generated folders: `Library/`, `Temp/`, `obj/`, `Logs/`.
- Treat animator parameter names as part of the runtime contract.
- Check scene Inspector references after renaming methods, actions, scripts, or serialized fields.
- For root-motion character changes, verify `OnAnimatorMove`, `CharacterController.Move`, grounded detection, and animator controller parameters together.
- For aim changes, first decide whether aim should be a toggle or hold behavior, then make `StarterAssetsInputs`, `ThirdPersonController`, `ThirdPersonAimController`, and Input Actions consistent.

## Verification Checklist

After code changes, use the Unity Editor with version `6000.0.28f1` and verify:

- Project opens without compile errors.
- `Assets/Scenes/youga.unity` loads.
- PlayerInput events still point to existing methods.
- Movement works with WASD.
- Look works with mouse delta.
- Jump, crouch, run, aim, and rifle actions trigger the expected animator state.
- No missing references on `PlayerMoveTest` serialized fields.
- URP materials and shaders render correctly in the scene view/game view.

There are no dedicated automated tests in this project at the time this guide was created.
