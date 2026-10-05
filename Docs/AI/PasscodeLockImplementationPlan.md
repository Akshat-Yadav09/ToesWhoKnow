# Passcode Lock, Door, and Hidden Bookshelf — Implementation Plan

## Goal

Add an interactable wall-mounted passcode lock. Interacting with it opens a keypad inside the existing inspection presentation. Entering the correct code closes the inspection, slides a bookshelf aside to reveal the hidden wall/opening, and unlocks the linked door. Wrong codes provide feedback and do not change the world.

This plan targets the current project rather than introducing a separate interaction framework.

## Confirmed project context

- Unity `6000.6.3f1`, 2D URP, new Input System, and uGUI/TextMeshPro.
- Player interaction is routed through `Akshat.Interaction.IInteractable` and `PlayerInteraction`.
- Inspection is managed by `Akshat.Inspection.InspectionManager`; it already disables player movement and interaction while a modal inspection is open.
- `InspectionManager` already supports instantiating an inspectable's `customVisualPrefab`, but it currently has no binding hook through which an interactive custom view can receive its owning inspectable.
- The gameplay scene `Assets/Final/Final.unity` uses `Akshat.RoomSystem.LinkedDoor`. Do not build this feature against the unused sample `DoorInteractable` unless the target scene is changed.
- `Assets/Final/Final.unity` contains a `BookShelf` object with a Transform and SpriteRenderer, but no movement component or collider.
- `Assets/Obj_assets/Padlock.png` and `Assets/Obj_assets/BookShelf.png` exist. Their GUIDs are not currently referenced by a saved scene or prefab. The wall lock the designer mentioned may be an unsaved Editor change or may use another sprite.
- Build Settings currently contains no enabled scenes. Do not alter Build Settings as part of this feature unless explicitly requested.
- There are no first-party test assemblies at present, although Unity Test Framework is available transitively.

## Assumed player-facing behavior

Unless the designer specifies otherwise, implement these rules:

1. The lock starts unsolved, the target door starts locked, and the bookshelf starts closed.
2. The player approaches the wall lock and sees `[E] Enter Code` through the existing prompt UI.
3. Pressing Interact opens the existing dimmed inspection view and locks gameplay controls.
4. The keypad accepts mouse clicks, keyboard number-row/numpad digits, Backspace, Enter, and UI navigation/Submit for gamepad.
5. The entered value is displayed as masked characters (`•`) and the configured code is never logged.
6. A wrong submission displays brief red/shake feedback, clears the current entry, and leaves the puzzle active. Attempts are unlimited.
7. A correct submission displays success feedback, prevents further input, closes the inspection, and then fires the unlock result exactly once.
8. The unlock result slides the bookshelf to its open local position and unlocks the target `LinkedDoor`.
9. The wall lock no longer offers interaction after it is solved. The result lasts for the current scene session only; save-game persistence is out of scope.
10. The bookshelf opens one way and does not close again during the scene.

Store the code as a string, not an integer, so codes such as `0427` work correctly.

## Recommended architecture

### 1. `PasscodeLockInteractable`

Create `Assets/Akshat/Scripts/Interaction/PasscodeLockInteractable.cs` in namespace `Akshat.Interaction`.

Responsibilities:

- Implement both `IInteractable` and `IInspectable` so it plugs into the existing detection and inspection systems directly.
- Hold serialized authoring data: `correctCode`, prompt, inspection sprite/title/config, keypad custom-view prefab, optional success/failure clips, and a serialized `UnityEvent onUnlocked`.
- Own the authoritative states: `Locked`, `AwaitingInput`, `Solved`, and `CompletionDispatched` (an enum is preferable to several interacting booleans).
- Expose a narrow `TrySubmitCode(string enteredCode)` API returning a result such as `Incomplete`, `Incorrect`, or `Correct`.
- On `Interact`, open itself with `InspectionManager.StartInspection(this)`.
- On correct submission, transition to solved only once. Request inspection exit after the short success feedback.
- Use `InspectionManager.OnInspectionEnded` to dispatch `onUnlocked` after the modal has closed, so the player sees the bookshelf movement clearly. Subscribe/unsubscribe safely and ensure the event cannot fire twice.
- Return `false` from `CanInteract()` after completion so `PlayerInteraction.ExecuteInteraction()` immediately removes the prompt on its rescan.
- Validate authoring data in `OnValidate`: code must be non-empty, numeric, and within a reasonable length such as 1–8 digits. Report invalid setup without printing the code itself.

Do not add a second `InspectableObject` component to the same GameObject; having two `IInteractable` implementations on one hierarchy would make the interaction target ambiguous.

### 2. Bindable custom inspection view

Create `Assets/Akshat/Scripts/Inspection/IInspectionCustomView.cs` with a small lifecycle contract, for example:

```csharp
public interface IInspectionCustomView
{
    void Bind(IInspectable inspectable, InspectionManager manager);
    void Unbind();
}
```

Update `InspectionManager` narrowly:

- After it instantiates `CustomVisualPrefab`, find one `IInspectionCustomView` on the spawned hierarchy and call `Bind(currentInspectable, this)`.
- Before destroying the custom visual on exit/disable, call `Unbind()`.
- Do not add passcode-specific fields or logic to `InspectionManager`.
- Preserve all existing inspection behavior for ordinary photos and clues.
- Make cleanup safe if the manager, lock, or spawned view is disabled during inspection.

This is the only required extension to the generic inspection system.

### 3. `PasscodeKeypadView`

Create `Assets/Akshat/Scripts/Inspection/PasscodeKeypadView.cs` and a reusable UI prefab at `Assets/Akshat/Prefabs/PasscodeKeypadPanel.prefab`.

The view should contain:

- A masked entry TMP label.
- A short status TMP label.
- Buttons `0`–`9`, Backspace, Clear, Submit, and Close/Cancel.
- A CanvasGroup used to disable input after a correct answer.
- Optional error shake/color feedback and optional success feedback.

Responsibilities:

- In `Bind`, require that the provided inspectable is a `PasscodeLockInteractable`; fail visibly in the Console if authoring is incorrect.
- Register button listeners once and remove them in `Unbind`/`OnDisable`.
- Keep only the temporary input buffer; the lock component owns solved state and validation.
- Enforce the configured maximum length before submission.
- Read new Input System keyboard controls without adding another player action map: number row/numpad digits, Backspace/Delete, and Enter/Numpad Enter.
- Let `InspectionManager` continue to own Escape/E exit behavior.
- Select the first keypad button when opened so the existing Input System UI module can provide gamepad navigation.
- Use a grid layout with explicit navigation if Unity's automatic navigation is unreliable.

The prefab should be a `RectTransform` UI hierarchy sized for the existing `customVisualMount`. Reset its anchors/anchored position on instantiation rather than relying only on `Transform.localPosition`.

### 4. `SlidingBookshelf`

Create `Assets/Akshat/Scripts/Interaction/SlidingBookshelf.cs` (or a `Puzzle` folder if one is introduced consistently).

Responsibilities:

- Capture the closed local position in `Awake`.
- Expose an `openLocalOffset`, duration, and AnimationCurve rather than hard-coded world coordinates.
- Provide a public parameterless `Open()` method for UnityEvent wiring.
- Animate with one guarded coroutine and `Time.deltaTime`; ignore repeat calls.
- Move the whole bookshelf GameObject so any child/blocking collider moves with it.
- Snap exactly to the final position on completion.
- If disabled mid-animation, leave it in a valid, deterministic state; do not start duplicate coroutines.
- Optionally play a one-shot movement sound and expose `OnOpened` only if another system genuinely needs it.

Do not use an Animator Controller for this single translation unless the final visual design requires authored animation. A local-position coroutine matches the project's existing transition style and keeps setup small.

### 5. Extend `LinkedDoor` with lock state

Modify `Assets/Akshat/Scripts/RoomSystem/LinkedDoor.cs`, because that is the door type used in `Final.unity`.

Add:

- Serialized `isLocked`, `lockedPrompt`, and optional locked-feedback sound.
- A read-only `IsLocked` property.
- Public `SetLocked(bool locked)` and parameterless `Unlock()` methods for inspector event wiring.
- A dynamic `InteractionPrompt` returning `lockedPrompt` while locked.
- An early locked check in `Interact()` before playing the normal door sound or starting a room transition.

Keep `CanInteract()` based on transition availability, not on `isLocked`. This allows the existing prompt UI to show `Door Locked`; `Interact()` then provides locked feedback without transitioning. Preserve all existing pairing, camera, spawn, and room-transition behavior.

## Scene and prefab wiring

Perform scene/prefab edits through the Unity Editor and keep serialized diffs narrow.

1. Save the intended lock sprite placement first. If no lock object is present after saving, create a GameObject using `Assets/Obj_assets/Padlock.png` at the designer's chosen wall position.
2. Put the lock GameObject on the existing `Interactable` layer and add/size a trigger `Collider2D` for the player detection radius.
3. Add `PasscodeLockInteractable`; assign its inspection sprite, keypad prefab, `InspectionManager`, and configured code.
4. Add `SlidingBookshelf` to the `BookShelf` GameObject in `Assets/Final/Final.unity`. Set an open local offset that reveals the intended opening without changing Z depth or sorting order unexpectedly.
5. If the bookshelf should physically block the passage, add or identify a collider as a child of the moving bookshelf. Do not disable unrelated room colliders.
6. Identify the exact `LinkedDoor` revealed/controlled by this puzzle. Set its initial lock state to true.
7. In the lock's `onUnlocked` UnityEvent, wire:
   - `SlidingBookshelf.Open()`
   - Target `LinkedDoor.Unlock()`
8. Assign optional audio clips only if assets already exist; missing audio must not block puzzle completion.
9. Keep the inspection-system prefab as the owner of modal presentation. Assign the keypad prefab as the lock's custom visual rather than placing a second always-on Canvas in the scene.
10. Verify there is exactly one active `EventSystem`/`InputSystemUIInputModule` at runtime. The project already includes one through existing scene/prefab setup; do not add another blindly.

## Implementation order for Antigravity

1. Open and save the target scene, then confirm the lock object, bookshelf, target `LinkedDoor`, and active `InspectionManager` references.
2. Add the custom-view binding interface and the minimal `InspectionManager` integration. Regression-test a normal existing inspectable before proceeding.
3. Implement `PasscodeLockInteractable` and `PasscodeKeypadView`.
4. Build and wire `PasscodeKeypadPanel.prefab`.
5. Implement `SlidingBookshelf` and validate it independently through its context-menu/debug invocation or Inspector event.
6. Add lock support to `LinkedDoor` and verify an unlocked door still behaves exactly as before.
7. Wire the lock's success event to both targets.
8. Compile, inspect Console output, run the full Play Mode acceptance matrix, and review serialized diffs for unrelated scene/prefab changes.

## Acceptance criteria

- Approaching the wall lock shows the correct existing interaction prompt.
- Interacting opens one inspection modal, hides the normal prompt, and prevents player movement and world interaction.
- Mouse, keyboard, and gamepad/UI navigation can operate the keypad.
- Leading-zero codes work.
- Input cannot exceed the configured code length.
- Empty, incomplete, and incorrect submissions do not unlock anything.
- Wrong input gives feedback and permits another attempt.
- Escape/E closes an unsolved keypad without altering the door or bookshelf; reopening starts with an empty entry.
- Correct input is accepted once, closes the inspection cleanly, slides the bookshelf once, and unlocks the door once.
- The revealed door cannot transition while locked and behaves exactly like the old `LinkedDoor` after unlocking.
- Repeated Submit presses, reopening attempts, repeated `Open()`/`Unlock()` calls, component disable, and scene reload produce no duplicate event, coroutine, or listener behavior.
- Existing ordinary `InspectableObject` instances still open, rotate/zoom, and close correctly.
- No passcode is written to logs, and no new Unity Console errors or missing serialized references appear.
- The solution does not edit Build Settings, add packages, add a global manager/singleton, or introduce a second interaction system.

## Validation notes

- Establish a Console-error baseline before making changes.
- Compile in the Unity Editor after each script group.
- Play-test in `Assets/Final/Final.unity`; Build Settings is currently empty, so do not claim build validation unless a scene is deliberately configured later.
- Automated tests are optional but useful for the plain code-entry rules (leading zeros, wrong/correct code, maximum length, and completion firing once). If tests are added, create a proper EditMode test assembly instead of placing NUnit code in the runtime assembly.
- Inspect the final scene and prefab diffs to ensure Unity did not rewrite unrelated objects, lighting, cameras, or prefab overrides.

## Out of scope

- Save-game persistence across scene/application reloads.
- Randomized or data-driven codes.
- Multiple locks sharing progression state.
- Hint/clue discovery logic.
- Networking.
- Changing scene Build Settings.

These can be layered on later without changing the interaction contract.
