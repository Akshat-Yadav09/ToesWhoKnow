# Light Switch, Room Electricity, and Generator — Simple Implementation Plan

## Player flow

1. Most rooms begin with electricity. Their light switches toggle their assigned lights normally.
2. One chosen room begins without electricity. Its lights are forced off.
3. The first time the player uses that room's switch:
   - Show: `There is no electricity in this room.`
   - Activate the disabled writing/text GameObject in the other room.
4. Further attempts may show the shorter message: `The switch does nothing.` They must not activate the writing repeatedly.
5. The player finds the generator and repeatedly presses E. Use about 10–12 interactions rather than an excessive number.
6. The interaction prompt shows progress, for example: `[E] Start Generator (5/12)`.
7. On the final press, show: `The generator roars to life. Power has been restored.`
8. The generator restores electricity to the powerless room.
9. The player returns to the switch and can now turn that room's lights on and off normally.

## Keep it simple

Create only four small scripts:

1. `RoomElectricity` — owns whether one room has electricity.
2. `LightSwitchInteractable` — handles a switch and its assigned lights.
3. `GeneratorInteractable` — counts repeated E interactions and restores power.
4. `PlayerMessageUI` — displays short character thoughts/messages.

Do not create a global power manager, quest system, save system, new input action, or full dialogue framework. The existing `PlayerInteraction` already sends one `Interact()` call for each E press.

## 1. `RoomElectricity.cs`

Location:

`Assets/Akshat/Scripts/RoomSystem/RoomElectricity.cs`

Add this component to each room that has electrical switches.

Serialized field:

- `startsWithElectricity` — true for normal rooms and false for the powerless room.

Public API:

- `bool HasElectricity { get; }`
- `event Action<bool> ElectricityChanged`
- `void RestoreElectricity()`
- Optional `void SetElectricity(bool value)` for testing in the Inspector/context menu.

Behavior:

- Initialize `HasElectricity` from `startsWithElectricity` in `Awake`.
- Fire `ElectricityChanged` only when the value actually changes.
- This is the single source of truth. Do not store separate copies of the electricity state in the generator and every switch.

## 2. `LightSwitchInteractable.cs`

Location:

`Assets/Akshat/Scripts/Interaction/LightSwitchInteractable.cs`

Implement the existing `IInteractable` interface.

Serialized fields:

- `RoomElectricity roomElectricity`
- `GameObject[] controlledLightObjects`
- `bool startsOn`
- `GameObject hiddenWritingObject`
- `PlayerMessageUI messageUI`
- `string noElectricityMessage`
- `string repeatedNoPowerMessage`
- Optional switch-on/switch-off sprites and click sound

Runtime fields:

- `bool isOn`
- `bool hasTriggeredNoPowerClue`

Behavior:

- `InteractionPrompt` returns `Turn Light On` or `Turn Light Off`. While there is no power it can return `Try Light Switch`.
- `CanInteract()` always returns true.
- On interaction without electricity:
  - Do not change `isOn`.
  - On the first attempt, show the no-electricity message and call `hiddenWritingObject.SetActive(true)`.
  - Set `hasTriggeredNoPowerClue = true` so this happens once.
  - Later attempts only show the shorter message.
- On interaction with electricity, toggle `isOn` and apply it to all controlled light GameObjects.
- Subscribe to `roomElectricity.ElectricityChanged` in `OnEnable` and unsubscribe in `OnDisable`.
- The actual visible light state is `isOn && roomElectricity.HasElectricity`.
- If power is lost, immediately switch the controlled lights off. Restoring power should not automatically turn them on; the player must use the switch afterward.

Use `GameObject[]` so one switch can enable a URP `Light2D`, glow sprite, and any small visual effect together. Do not disable the scene's global light, because that would affect every room.

## 3. `GeneratorInteractable.cs`

Location:

`Assets/Akshat/Scripts/Interaction/GeneratorInteractable.cs`

Implement `IInteractable`.

Serialized fields:

- `RoomElectricity electricityToRestore`
- `int requiredInteractions = 12`
- `PlayerMessageUI messageUI`
- `string completionMessage`
- Optional per-press sound, completion sound, idle sprite, and running sprite

Runtime fields:

- `int interactionCount`
- `bool isRunning`

Behavior:

- Each E press calls `Interact()` and increments `interactionCount` once.
- Use a dynamic prompt such as `Start Generator (4/12)` so the player understands that repeated presses are intentional.
- Clamp progress so it cannot exceed the required count.
- On the final interaction:
  - Set `isRunning = true` first so completion cannot happen twice.
  - Call `electricityToRestore.RestoreElectricity()`.
  - Show the completion message.
  - Play the completion sound and change to the running sprite if assigned.
- After completion, `CanInteract()` returns false so the interaction prompt disappears.
- Progress lasts for the current scene and does not decay between presses.

Suggestion: add a tiny sprite wobble or alternate between two generator sprites on each press only if those assets already exist. Do not build a full animation system for this.

## 4. `PlayerMessageUI.cs`

Location:

`Assets/Akshat/Scripts/Interaction/PlayerMessageUI.cs`

Add one small message panel to the existing interaction canvas in `Interaction_Inspection_System.prefab`.

The component needs:

- A TMP text reference.
- A CanvasGroup or panel GameObject.
- `ShowMessage(string message, float duration = 2.5f)`.
- One coroutine using `Time.unscaledDeltaTime` so messages work even if gameplay is temporarily paused.
- If a new message arrives, cancel the previous hide coroutine and replace the text.

This is not a dialogue system. It only displays short character thoughts and can also be reused by later interactions.

## Scene wiring

1. Add `RoomElectricity` to every room that needs switches.
2. Set `startsWithElectricity = true` in normal rooms.
3. Set it to false in the one powerless room.
4. Add `LightSwitchInteractable` and a trigger `Collider2D` to each switch; use the existing `Interactable` layer.
5. Assign each switch to its own room's `RoomElectricity`.
6. Assign only that room's `Light2D`/glow GameObjects to `controlledLightObjects`.
7. On the powerless-room switch, assign the disabled writing/text object from the other room.
8. Keep that writing object's own `activeSelf` false at scene start. It may be under an inactive room parent; setting the child active will make it appear when that room is later activated.
9. Add `GeneratorInteractable`, a trigger collider, and the Interactable layer to the generator.
10. Assign the powerless room's `RoomElectricity` to `electricityToRestore`.
11. Add one `PlayerMessageUI` panel to the shared interaction canvas and assign it to the switch and generator.

## Suggested text

- First powerless switch attempt: `There is no electricity in this room.`
- Later powerless attempts: `The switch does nothing.`
- Generator progress should stay in the interaction prompt rather than creating a message on every press.
- Generator completion: `The generator roars to life. Power has been restored.`
- Optional first powered switch use: `The lights are working again.`

## Validation checklist

- Switches in powered rooms toggle only their assigned room lights.
- The powerless room starts dark even if its switch has `startsOn` enabled.
- The first powerless switch interaction shows the message and activates the hidden writing once.
- Repeated powerless interactions do not repeatedly trigger the clue.
- The writing appears when the player later enters the other room.
- Each physical E press advances the generator exactly once.
- Generator progress is visible and cannot exceed the required count.
- The final press restores electricity and completes only once.
- Restoring electricity does not immediately turn on the room lights.
- The switch works normally after power is restored.
- No global light or unrelated room light is disabled.
- Scene reload resets everything, since save persistence is intentionally out of scope.
- No new Console errors, duplicate event subscriptions, or missing Inspector references appear.

## Optional improvements only if time remains

- Switch and generator sound effects.
- On/off switch sprites.
- Generator idle/running sprites.
- A brief light flicker when power returns.

Build and verify the basic electricity, repeated interaction, clue activation, and light toggle first.
