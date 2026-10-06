// MenuLookController.cs
// Drives subtle "head look" movement by translating mouse delta into ViewRoot offset.
// The player navigates the menu by moving the mouse; menu items closest to
// the exact screen centre are selected (no normal cursor hover used).
// Attach to: MenuLookController GameObject (or any persistent object in the scene).

using UnityEngine;
using UnityEngine.InputSystem;

namespace Shaurya.MainMenu
{
    public class MenuLookController : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("ViewRoot RectTransform — moved by mouse to simulate head movement.")]
        [SerializeField] private RectTransform viewRoot;

        [Tooltip("Array of all MenuOption components in the menu.")]
        [SerializeField] private MenuOption[] menuOptions;

        [Header("Movement")]
        [Tooltip("1.10 keeps video edges out of frame during head movement.")]
#pragma warning disable CS0414
        [SerializeField] private float viewRootScale = 1.10f;
#pragma warning restore CS0414

        [Tooltip("Maximum horizontal offset (pixels) ViewRoot can travel.")]
        [SerializeField] private float horizontalLimit = 70f;

        [Tooltip("Maximum vertical offset (pixels) ViewRoot can travel.")]
        [SerializeField] private float verticalLimit = 45f;

        [Tooltip("Mouse sensitivity. Higher = more movement per mouse delta.")]
        [SerializeField] private float sensitivity = 0.08f;

        [Tooltip("Smoothing speed. Higher = snappier response.")]
        [SerializeField] private float smoothSpeed = 7f;

        [Header("Selection")]
        [Tooltip("Pixels from screen centre within which an option is considered selected.")]
        [SerializeField] private float selectionRadius = 100f;

        // ── State ────────────────────────────────────────────────────────────

        private bool isActive = false;
        private Vector2 rawOffset = Vector2.zero;
        private Vector2 currentOffset = Vector2.zero;
        private MenuOption currentlySelected = null;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            // Keep ViewRoot at standard 1.0 scale
            if (viewRoot != null)
                viewRoot.localScale = Vector3.one;

            // Disable self
            enabled = false;
        }

        private void Update()
        {
            if (!isActive) return;

            HandleMouseMovement();
            UpdateViewRootPosition();
            UpdateMenuSelection();
            HandleMenuInput();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by MainMenuSequencer when the main menu becomes active.</summary>
        public void Activate()
        {
            isActive = true;
            enabled = true;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined;
        }

        /// <summary>Disables look control and restores cursor.</summary>
        public void Deactivate()
        {
            isActive = false;
            enabled = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // ── Mouse Movement ────────────────────────────────────────────────────

        private void HandleMouseMovement()
        {
            if (Mouse.current == null) return;

            Vector2 delta = Mouse.current.delta.ReadValue();
            rawOffset += delta * sensitivity;

            // Clamp to limits
            rawOffset.x = Mathf.Clamp(rawOffset.x, -horizontalLimit, horizontalLimit);
            rawOffset.y = Mathf.Clamp(rawOffset.y, -verticalLimit, verticalLimit);
        }

        private void UpdateViewRootPosition()
        {
            currentOffset = Vector2.Lerp(currentOffset, rawOffset, Time.unscaledDeltaTime * smoothSpeed);

            if (viewRoot != null)
                viewRoot.anchoredPosition = currentOffset;
        }

        // ── Centre-Screen Selection ───────────────────────────────────────────

        private void UpdateMenuSelection()
        {
            if (menuOptions == null || menuOptions.Length == 0) return;

            Vector2 screenCentre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            MenuOption closest = null;
            float closestDist = float.MaxValue;

            foreach (MenuOption option in menuOptions)
            {
                if (option == null || !option.gameObject.activeInHierarchy) continue;

                // Convert the option's world position to screen position
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, option.transform.position);
                float dist = Vector2.Distance(screenPos, screenCentre);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = option;
                }
            }

            // Check if the closest option is within the selection radius
            if (closest != null && closestDist <= selectionRadius)
            {
                if (currentlySelected != closest)
                {
                    // Deselect old
                    if (currentlySelected != null) currentlySelected.SetSelected(false);
                    // Select new
                    closest.SetSelected(true);
                    currentlySelected = closest;
                }
            }
            else
            {
                // Nothing in range
                if (currentlySelected != null) currentlySelected.SetSelected(false);
                currentlySelected = null;
            }
        }

        // ── Menu Input ────────────────────────────────────────────────────────

        private void HandleMenuInput()
        {
            if (currentlySelected == null) return;

            bool activate = false;

            // Enter key
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                activate = true;

            // Right mouse button
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                activate = true;

            if (activate)
            {
                // Disable further input while scene loads
                Deactivate();
                currentlySelected.Activate();
            }
        }
    }
}
