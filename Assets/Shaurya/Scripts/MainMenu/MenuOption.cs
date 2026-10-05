// MenuOption.cs
// 100% Invisible Clickable Button Hitbox for the Main Menu.
// Place over any button area on your PNG background.
// Clicks smoothly fade to black and load scene "Final" (or quit the application).

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#pragma warning disable CS0618

namespace Shaurya.MainMenu
{
    public class MenuOption : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public enum ActionType
        {
            OpenFinalScene,
            ExitGame,
            LoadCustomScene
        }

        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Button Action")]
        [Tooltip("What happens when this button is clicked.")]
        [SerializeField] private ActionType action = ActionType.OpenFinalScene;

        [Tooltip("Custom scene name if Action is set to LoadCustomScene.")]
        [SerializeField] private string customSceneName = "Final";

        [Header("Audio (Optional)")]
        [Tooltip("Sound played when this button is hovered.")]
        [SerializeField] private AudioClip hoverSound;

        [Tooltip("Sound played when this button is clicked.")]
        [SerializeField] private AudioClip clickSound;

        [Tooltip("AudioSource to play UI sounds (if null, will look for one).")]
        [SerializeField] private AudioSource uiAudioSource;

        [Header("Scene Transition Fade")]
        [Tooltip("FadePanel CanvasGroup — faded to black before loading the scene or exiting.")]
        [SerializeField] private CanvasGroup fadePanelGroup;

        [Tooltip("Duration of the fade-to-black before scene load.")]
        [SerializeField] private float sceneFadeDuration = 0.8f;

        [Header("Audio Fade on Activate")]
        [Tooltip("Main menu audio sources to fade out when activated.")]
        [SerializeField] private AudioSource[] menuAudioSources;

        [Tooltip("Duration to fade out menu audio before scene load.")]
        [SerializeField] private float audioFadeOutDuration = 0.8f;

        // ── State ────────────────────────────────────────────────────────────

        private static bool isTransitioning = false;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            // 1. Ensure the clickable Image is 100% transparent (no white sprite, no white borders)
            Image img = GetComponent<Image>();
            if (img == null)
            {
                img = gameObject.AddComponent<Image>();
            }
            img.sprite = null;
            img.color = new Color(0f, 0f, 0f, 0f); // Zero alpha = completely invisible
            img.raycastTarget = true;              // Catches mouse clicks

            // 2. Disable any old text components so no letters/white boundaries appear
            TMP_Text tmp = GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.text = "";
                tmp.enabled = false;
            }

            // 3. If a standard Button component exists, disable color tinting so it never turns white
            Button btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.transition = Selectable.Transition.None;
            }

            // 4. Auto-locate FadePanel if not assigned
            if (fadePanelGroup == null)
            {
                GameObject fadePanelGO = GameObject.Find("FadePanel");
                if (fadePanelGO != null)
                    fadePanelGroup = fadePanelGO.GetComponent<CanvasGroup>();
            }

            // 5. Auto-locate AudioSource if needed
            if (uiAudioSource == null)
            {
                uiAudioSource = GetComponent<AudioSource>();
                if (uiAudioSource == null)
                    uiAudioSource = FindObjectOfType<AudioSource>();
            }

            // 6. Auto-detect exit button from GameObject name
            if (gameObject.name.ToLower().Contains("exit") || gameObject.name.ToLower().Contains("quit"))
            {
                action = ActionType.ExitGame;
            }
        }

        private void OnEnable()
        {
            isTransitioning = false;
        }

        // ── Public API (Can also be used in Button OnClick) ───────────────────

        public void PlayGame()
        {
            action = ActionType.OpenFinalScene;
            Activate();
        }

        public void QuitGame()
        {
            action = ActionType.ExitGame;
            Activate();
        }

        public void SetSelected(bool selected)
        {
            // Kept for backward compatibility with MenuLookController
        }

        // ── Mouse / EventSystem Handlers ─────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            Activate();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PlaySound(hoverSound);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
        }

        public void Activate()
        {
            if (isTransitioning) return;

            Debug.Log($"<color=cyan>[MainMenu Button]</color> CLICKED: <b>{gameObject.name}</b> (Action: {action})");
            PlaySound(clickSound);

            switch (action)
            {
                case ActionType.ExitGame:
                    StartCoroutine(FadeAndQuit());
                    break;

                case ActionType.OpenFinalScene:
                    StartCoroutine(FadeAndLoadScene("Final"));
                    break;

                case ActionType.LoadCustomScene:
                    StartCoroutine(FadeAndLoadScene(customSceneName));
                    break;
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && uiAudioSource != null)
            {
                uiAudioSource.PlayOneShot(clip);
            }
        }

        // ── Scene / Quit Routines ─────────────────────────────────────────────

        private IEnumerator FadeAndLoadScene(string sceneName)
        {
            isTransitioning = true;

            StartCoroutine(FadeOutMenuAudio());

            if (fadePanelGroup != null)
            {
                fadePanelGroup.blocksRaycasts = true;
                float elapsed = 0f;
                while (elapsed < sceneFadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / sceneFadeDuration);
                    fadePanelGroup.alpha = t * t * (3f - 2f * t);
                    yield return null;
                }
                fadePanelGroup.alpha = 1f;
            }
            else
            {
                yield return new WaitForSecondsRealtime(sceneFadeDuration);
            }

            if (!string.IsNullOrEmpty(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogWarning($"[MenuOption] '{gameObject.name}' has no target scene assigned.");
            }
        }

        private IEnumerator FadeAndQuit()
        {
            isTransitioning = true;

            StartCoroutine(FadeOutMenuAudio());

            if (fadePanelGroup != null)
            {
                fadePanelGroup.blocksRaycasts = true;
                float elapsed = 0f;
                while (elapsed < sceneFadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    fadePanelGroup.alpha = Mathf.Clamp01(elapsed / sceneFadeDuration);
                    yield return null;
                }
                fadePanelGroup.alpha = 1f;
            }
            else
            {
                yield return new WaitForSecondsRealtime(sceneFadeDuration);
            }

            Debug.Log("[MenuOption] Quitting application.");
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private IEnumerator FadeOutMenuAudio()
        {
            if (menuAudioSources == null || menuAudioSources.Length == 0)
            {
                menuAudioSources = FindObjectsOfType<AudioSource>();
            }

            if (menuAudioSources == null) yield break;

            float elapsed = 0f;
            float[] startVols = new float[menuAudioSources.Length];
            for (int i = 0; i < menuAudioSources.Length; i++)
                startVols[i] = menuAudioSources[i] != null ? menuAudioSources[i].volume : 0f;

            while (elapsed < audioFadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / audioFadeOutDuration);
                float smooth = t * t * (3f - 2f * t);
                for (int i = 0; i < menuAudioSources.Length; i++)
                {
                    if (menuAudioSources[i] != null)
                        menuAudioSources[i].volume = Mathf.Lerp(startVols[i], 0f, smooth);
                }
                yield return null;
            }

            foreach (var src in menuAudioSources)
                if (src != null) { src.volume = 0f; src.Stop(); }
        }
    }
}
