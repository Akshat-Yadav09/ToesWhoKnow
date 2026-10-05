# Bookshelf Book-Selection Puzzle — Short Implementation Plan

## Player flow

1. The player approaches the bookshelf and sees `[E] Search Bookshelf` through the existing interaction prompt.
2. Interacting opens the existing inspection screen, which already locks movement and normal interactions.
3. The screen shows:
   - One text field labelled `Book Initials`.
   - A short hint such as `Enter the row and column letters`.
   - A `Search` button.
   - A closed-book image.
   - A small character-thought/message text area.
4. The player types two letters, such as `AY`, and presses Search or Enter.
5. Any entry except `AY` displays: `You searched through the book but found nothing.` The player can immediately try again.
6. Entering `AY` swaps the closed-book image to the open-book sprite and displays the success line, for example: `This book was marked for a reason... there is a code inside.`
7. The puzzle remains solved for the rest of the current scene. Reopening it shows the open book and the success message again.
8. Escape/E closes the inspection as it already does elsewhere.

## Keep the implementation small

The current project already has everything needed:

- `IInteractable` for detecting and interacting with the bookshelf.
- `IInspectable` and `InspectionManager` for the modal view and locking the player.
- `IInspectionCustomView` for binding an interactive UI prefab to an inspected object.

Do not add a new manager, dialogue framework, ScriptableObject database, input action map, dropdowns, or hundreds of separate book buttons. A single two-character text field is the quickest implementation.

## Files to create

### 1. `BookshelfSearchInteractable.cs`

Location:

`Assets/Akshat/Scripts/Interaction/BookshelfSearchInteractable.cs`

Implement `IInteractable` and `IInspectable`, following the same structure as `PasscodeLockInteractable`.

Serialized fields:

- `interactionPrompt = "Search Bookshelf"`
- `correctBookInitials = "AY"`
- `closedBookSprite`
- `openBookSprite`
- `successMessage`
- `failureMessage = "You searched through the book but found nothing."`
- `bookSelectionViewPrefab`
- Normal inspection title/config fields
- Optional `UnityEvent onCorrectBookFound` for a later clue, door, or puzzle connection

Runtime state:

- One private `bool hasFoundCorrectBook`.
- A read-only `HasFoundCorrectBook` property for the UI.

Required methods:

- `Interact()` calls `InspectionManager.Instance.StartInspection(this)`.
- `CanInteract()` returns true so the player can review the discovered book later.
- `bool SearchBook(string initials)` trims whitespace, converts the value to uppercase, and compares it with `AY`.
- On the first correct result, set `hasFoundCorrectBook = true` and invoke `onCorrectBookFound` once.
- Expose the two sprites and success/failure messages through read-only properties for the custom view.

Store the correct value as a two-character uppercase string. The input must be exactly two letters after trimming; therefore `ay`, `Ay`, and ` AY ` are accepted as `AY`, while incomplete or longer entries fail normally.

### 2. `BookSelectionView.cs`

Location:

`Assets/Akshat/Scripts/Inspection/BookSelectionView.cs`

Implement the existing `IInspectionCustomView` interface.

UI references:

- `TMP_InputField initialsInput`
- `Button searchButton`
- `Button closeButton`
- `Image bookImage`
- `TMP_Text messageText`

Behavior:

- In `Bind`, confirm the inspectable is a `BookshelfSearchInteractable`.
- Configure the input field with a character limit of `2` and letter-only validation.
- Automatically convert typed letters to uppercase for display.
- Select and activate the input field when the panel opens so the player can type immediately.
- Register the Search and Close button listeners once.
- Start with the closed-book sprite and blank message when unsolved.
- If already solved, start with the open-book sprite and success message.
- On Search or input-field Submit/Enter, call `SearchBook(initialsInput.text)`.
- Wrong result: keep the closed sprite, show the failure message, clear/refocus the input field, and allow another attempt.
- Correct result: swap to the open-book sprite, show the success message, and disable the input field and Search button.
- `Unbind` removes only listeners this view registered; avoid `RemoveAllListeners()` because prefab-authored listeners may exist.
- Close calls `InspectionManager.ExitInspection()`.

The text inside this panel acts as the character's thought/dialogue. The project currently has no dialogue system, so building one just for two sentences would cost unnecessary time.

### 3. `BookSelectionPanel.prefab`

Location:

`Assets/Akshat/Prefabs/BookSelectionPanel.prefab`

Build a simple uGUI/TMP prefab containing:

- A title such as `Choose a book`.
- One `Book Initials` TMP input field with a two-character limit.
- Search and Close buttons.
- One Image for the book sprite.
- One TMP text box for the character message.
- `BookSelectionView` on the prefab root.

Keep it visually consistent with the existing passcode keypad. It will be assigned as the bookshelf inspectable's `CustomVisualPrefab` and instantiated by `InspectionManager`.

## Scene wiring

1. Select the existing `BookShelf` GameObject in `Assets/Final/Final.unity`.
2. Ensure it has a trigger `Collider2D` and uses the existing `Interactable` layer.
3. Add `BookshelfSearchInteractable`.
4. Do not also add the generic `InspectableObject`; both would implement `IInteractable` and could conflict.
5. Assign the closed/open book sprites and `BookSelectionPanel.prefab`.
6. Set `correctBookInitials` to `AY`.
7. Enter the two message strings in the Inspector so wording can be changed without editing code.
8. If this is the same GameObject that has `SlidingBookshelf`, leave that component alone. The search component controls only the modal puzzle; `SlidingBookshelf` controls only the world transform.
9. If finding A/Y should later unlock something, connect `onCorrectBookFound` through the Inspector. Leave it empty for now if the only reward is seeing the code.

## Suggested success text

Use a short line so the player understands this is important:

`This one is different... there is a code written inside.`

If the open-book sprite visibly contains the code, do not repeat the actual code in the message.

## Validation checklist

- The bookshelf prompt appears only within the normal interaction range.
- Opening it locks player movement and hides the world interaction prompt.
- The input field accepts no more than two alphabetic characters and displays them in uppercase.
- `AX`, `BY`, incomplete input, and other wrong combinations show the failure line and do not open the book.
- `AY`, `ay`, and `Ay` change to the open-book sprite and show the success line.
- The success event fires only once even if Search is pressed repeatedly.
- Closing and reopening after success still shows the open book.
- Closing while unsolved and reopening starts cleanly.
- Ordinary inspections and the passcode keypad still work.
- There are no duplicate button listeners or new Console errors.

## Optional polish only if time remains

- Play a short page-opening sound on success.
- Fade between the closed and open sprites over 0.15 seconds.
- Briefly shake the book image after a wrong choice.

These are optional. Implement the basic selection, sprite swap, and messages first.
