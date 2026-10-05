# Black Tape Pickup and Warehouse Wire Repair — Simple Implementation Plan

## Player flow

1. The warehouse begins without power and its room lights are off.
2. The player interacts with the damaged warehouse wire without tape:
   - Show: `The wire is damaged. I need something to insulate it.`
   - Nothing else changes.
3. The player enters the kitchen and finds visible black tape on a shelf.
4. Interacting with it shows `Picked up black tape.`, hides the pickup, and records the tape on the Player.
5. The player returns to the warehouse. The tape remains collected even though the kitchen room has been deactivated.
6. Interacting with the damaged wire consumes the tape, enables the wire's existing disabled `BlackTape` child, restores warehouse electricity, and turns on the room light.
7. Show: `That should hold. The power is back on.`
8. The repaired wire and collected pickup stay in their new states while moving between rooms. They reset only when the scene restarts.

## Important room-loading finding

The current project does not load and unload separate Unity scenes for each room. `PerspectiveTransitionManager` keeps all rooms inside `Assets/Final/Final.unity` and toggles each `RoomZone` GameObject active or inactive.

Therefore:

- Direct serialized references between room objects are still valid.
- Inactive room objects retain their fields and `activeSelf` state.
- No `DontDestroyOnLoad` object or save file is needed for this feature.
- The tape ownership must live on `Akshat_Player`, because the Player remains active while the kitchen and warehouse roots are toggled.

Do not store `hasTape` on the kitchen pickup or kitchen room. That state would be owned by the wrong object.

## Keep it small

Create only three new scripts:

1. `PlayerKeyItems` — stores whether the player currently carries black tape.
2. `BlackTapePickup` — gives tape to the player and hides the pickup.
3. `WireRepairInteractable` — checks/consumes tape and repairs the warehouse wire.

Reuse `RoomElectricity`, `LightSwitchInteractable`, and `PlayerMessageUI` from the earlier light-system plan. If those have not been implemented yet, implement their basic versions first; do not create duplicate power or message systems inside this feature.

## 1. `PlayerKeyItems.cs`

Location:

`Assets/Akshat/Scripts/Interaction/PlayerKeyItems.cs`

Add it to `Akshat_Player.prefab` so it remains active during all room transitions.

Keep it specific and simple:

- Private runtime `bool hasBlackTape`.
- `public bool HasBlackTape { get; }`.
- `public bool AddBlackTape()` returns false if already owned.
- `public bool TryConsumeBlackTape()` returns false if not owned; otherwise clears the flag and returns true.

Do not build slots, quantities, icons, item databases, or ScriptableObjects for one puzzle item. This inventory resets when the scene reloads.

## 2. `BlackTapePickup.cs`

Location:

`Assets/Akshat/Scripts/Interaction/BlackTapePickup.cs`

Implement the existing `IInteractable` interface.

Serialized references:

- `PlayerKeyItems playerKeyItems`
- `PlayerMessageUI messageUI`
- `pickupMessage = "Picked up black tape."`
- Optional pickup sound

Behavior:

- Prompt: `Pick Up Black Tape`.
- On interaction, call `playerKeyItems.AddBlackTape()`.
- If successful, show the message, play optional audio, and disable the pickup GameObject.
- `CanInteract()` returns false after pickup.
- If references are not assigned, it may find them once in `Awake`; do not search every frame.
- Do not destroy the pickup. `SetActive(false)` preserves its collected state while rooms are toggled.

## 3. `WireRepairInteractable.cs`

Location:

`Assets/Akshat/Scripts/Interaction/WireRepairInteractable.cs`

Implement `IInteractable`.

Serialized references:

- `PlayerKeyItems playerKeyItems`
- `PlayerMessageUI messageUI`
- `GameObject repairedTapeVisual`
- `RoomElectricity warehouseElectricity`
- Optional repair sound
- `missingTapeMessage = "The wire is damaged. I need something to insulate it."`
- `repairSuccessMessage = "That should hold. The power is back on."`

Runtime state:

- Private `bool isRepaired`.

Behavior:

- Dynamic prompt:
  - Without tape: `Inspect Damaged Wire`
  - With tape: `Repair Wire`
- On interaction without tape, show the missing-tape message and change nothing.
- On interaction with tape:
  1. Call `TryConsumeBlackTape()`.
  2. Set `isRepaired = true` immediately so repeated input cannot repair twice.
  3. Enable `repairedTapeVisual`.
  4. Call `warehouseElectricity.RestoreElectricity()`.
  5. Play optional sound and show the success message.
- After repair, `CanInteract()` returns false so the prompt disappears.
- In `Awake`, force the repair visual off only when `isRepaired` is false. Do not continually reset it in `OnEnable`, because the warehouse is disabled and re-enabled during travel.

The saved scene already contains a disabled `BlackTape` child under `Wire`; assign that object to `repairedTapeVisual` instead of creating another repair visual.

## Turning the warehouse light on immediately

Use the earlier electricity system as follows:

- Warehouse `RoomElectricity.startsWithElectricity = false`.
- Warehouse `LightSwitchInteractable.startsOn = true`.
- Its controlled light objects begin visually off because the room has no electricity.
- When wire repair calls `RestoreElectricity()`, the switch receives `ElectricityChanged` and reapplies its state. Since `isOn` started true, the warehouse lights come on immediately.
- Afterward, the player can use the switch normally to turn them off and on.

This keeps the switch and visible light state synchronized. Do not separately call `lightGameObject.SetActive(true)` from the repair script if that same light is controlled by a switch.

If the light-system plan is intentionally not being used, allow one fallback `GameObject objectToEnableAfterRepair` field in `WireRepairInteractable`. Use either the electricity reference or this fallback, not both.

## Scene wiring

### Kitchen tape

1. Save the intended kitchen shelf setup first. The current serialized scene does not yet contain a separate tape pickup.
2. Create a visible `BlackTapePickup` GameObject on/in the kitchen shelf, using the tape sprite.
3. Add a trigger `Collider2D`.
4. Put it on the existing `Interactable` layer.
5. Add `BlackTapePickup` and assign the Player and shared message UI.

### Warehouse wire

1. Select the existing `Wire` GameObject in `Room_Warehouse`.
2. Keep its existing `BlackTape` child disabled at scene start.
3. Add a trigger `Collider2D` sized around the damaged section.
4. Put `Wire` on the existing `Interactable` layer.
5. Add `WireRepairInteractable`.
6. Assign the existing disabled child to `repairedTapeVisual`.
7. Assign the warehouse's `RoomElectricity` and the shared message UI.

### Persistent player state

1. Add `PlayerKeyItems` to `Assets/Akshat/Prefabs/Akshat_Player.prefab`.
2. Confirm the Player is not parented under a `RoomZone`.
3. Do not add `PlayerKeyItems` separately to each room.

## Implementation order for Antigravity

1. Confirm or implement the minimal `RoomElectricity`, `LightSwitchInteractable`, and `PlayerMessageUI` from the earlier plan.
2. Add `PlayerKeyItems` to the Player prefab.
3. Create and test the kitchen tape pickup.
4. Create the wire repair interaction using the existing disabled tape child.
5. Connect wire repair to warehouse electricity.
6. Test the entire kitchen → room transition → warehouse sequence.
7. Review `Final.unity` and prefab diffs for unrelated changes.

## Validation checklist

- Interacting with the wire before collecting tape shows the missing-item message and does not restore power.
- Picking up the tape shows feedback, hides the pickup, and records it on the Player.
- Leaving and returning to the kitchen does not respawn the tape.
- Tape ownership survives kitchen deactivation and warehouse activation.
- The wire prompt changes to `Repair Wire` when the player owns tape.
- Repair consumes the tape exactly once.
- The existing disabled `BlackTape` child becomes visible.
- Warehouse electricity is restored and its light comes on.
- The warehouse light switch remains synchronized and works afterward.
- Leaving and returning to the warehouse keeps the wire repaired and power restored.
- Rapid repeated E presses cannot duplicate the repair or messages.
- Scene reload resets the puzzle, which is intentional for now.
- No additional scene loading, static global inventory, or new input binding is introduced.

## Optional polish only if time remains

- Tape pickup sound.
- Short electrical spark before repair and a soft power-up sound afterward.
- Swap the broken wire sprite for a repaired version in addition to enabling the tape child.
- A one-line message when examining the already repaired wire—but only if it remains interactable.

Implement the pickup, persistent carried flag, repair visual, and electricity restoration first.
