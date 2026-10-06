// PlayerMessageUI.cs
// Displays short character thoughts/narrative feedback messages to the player.

using System.Collections;
using UnityEngine;
using TMPro;

namespace Akshat.Interaction
{
    /// <summary>
    /// Displays temporary on-screen messages/thoughts for the player (e.g. 'Picked up black tape').
    /// </summary>
    public class PlayerMessageUI : MonoBehaviour
    {
        public static PlayerMessageUI Instance { get; private set; }

        [Header("UI References")]
        [Tooltip("The TMP_Text component displaying the message.")]
        [SerializeField] private TMP_Text messageText;

        [Tooltip("The CanvasGroup controlling panel visibility/alpha.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("Optional panel GameObject to toggle.")]
        [SerializeField] private GameObject messagePanel;

        [Header("Animation Settings")]
        [Tooltip("Fade in/out duration in seconds.")]
        [SerializeField] private float fadeDuration = 0.2f;

        [Tooltip("Default message display duration before fading out.")]
        [SerializeField] private float defaultDuration = 2.5f;

        private Coroutine activeMessageRoutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (messageText == null)
            {
                messageText = GetComponentInChildren<TMP_Text>(true);
            }

            if (messageText != null)
            {
                messageText.fontSize = Mathf.Max(messageText.fontSize, baseFontSize);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (messagePanel != null)
            {
                messagePanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Displays a message for the specified duration.
        /// </summary>
        public void ShowMessage(string message, float duration = -1f)
        {
            if (string.IsNullOrEmpty(message)) return;

            float displayTime = duration > 0f ? duration : defaultDuration;

            if (activeMessageRoutine != null)
            {
                StopCoroutine(activeMessageRoutine);
            }

            activeMessageRoutine = StartCoroutine(DisplayMessageRoutine(message, displayTime));
        }

        [Header("Display Settings")]
        [Tooltip("Font size for the bottom screen feedback message.")]
        [SerializeField] private int baseFontSize = 26;

        [Tooltip("Vertical distance from bottom edge of the screen.")]
        [SerializeField] private float bottomOffset = 80f;

        private string guiMessage = string.Empty;
        private float guiAlpha = 0f;
        private GUIStyle guiStyle;

        private void OnGUI()
        {
            if (messageText != null || string.IsNullOrEmpty(guiMessage) || guiAlpha <= 0.01f) return;

            int effectiveFontSize = Mathf.Max(baseFontSize, Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.025f), 22, 36));

            if (guiStyle == null)
            {
                guiStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                guiStyle.normal.textColor = Color.white;
            }
            guiStyle.fontSize = effectiveFontSize;

            int width = Mathf.Min(780, Screen.width - 60);
            int height = Mathf.Max(66, effectiveFontSize + 38);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - height - bottomOffset;

            // Semi-transparent dark background card
            GUI.color = new Color(0.08f, 0.1f, 0.14f, guiAlpha * 0.92f);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, guiStyle);

            // High-contrast crisp white message text
            GUI.color = new Color(1f, 1f, 1f, guiAlpha);
            GUI.Label(new Rect(x + 18, y + 8, width - 36, height - 16), guiMessage, guiStyle);
        }

        private IEnumerator DisplayMessageRoutine(string message, float displayTime)
        {
            guiMessage = message;
            if (messagePanel != null) messagePanel.SetActive(true);
            if (messageText != null) messageText.text = message;

            // Fade In
            float elapsed = 0f;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : guiAlpha;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = fadeDuration > 0.001f ? elapsed / fadeDuration : 1f;
                float currentAlpha = Mathf.Lerp(startAlpha, 1f, t);
                if (canvasGroup != null) canvasGroup.alpha = currentAlpha;
                guiAlpha = currentAlpha;
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            guiAlpha = 1f;

            // Hold
            yield return new WaitForSecondsRealtime(displayTime);

            // Fade Out
            elapsed = 0f;
            startAlpha = canvasGroup != null ? canvasGroup.alpha : guiAlpha;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = fadeDuration > 0.001f ? elapsed / fadeDuration : 1f;
                float currentAlpha = Mathf.Lerp(startAlpha, 0f, t);
                if (canvasGroup != null) canvasGroup.alpha = currentAlpha;
                guiAlpha = currentAlpha;
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            guiAlpha = 0f;
            guiMessage = string.Empty;

            if (messagePanel != null) messagePanel.SetActive(false);
            activeMessageRoutine = null;
        }

        /// <summary>
        /// Static convenience method to show a message from anywhere without a direct reference.
        /// </summary>
        public static void Show(string message, float duration = 2.5f)
        {
            if (Instance == null)
            {
                Instance = FindAnyObjectByType<PlayerMessageUI>();
            }

            if (Instance == null)
            {
                GameObject host = GameObject.Find("Akshat_Player");
                if (host == null)
                {
                    host = new GameObject("PlayerMessageUI_Host");
                }
                Instance = host.AddComponent<PlayerMessageUI>();
            }

            Instance.ShowMessage(message, duration);
        }
    }
}
