// MainMenuSequencer.cs
// Central state machine for the MainMenu scene.
// Drives: Dialogue → Cinematic Audio → Video → Fade-to-black → PNG Background → Main Menu.
// Attach to: a persistent "MainMenuSequencer" GameObject in the scene.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

#pragma warning disable CS0618

namespace Shaurya.MainMenu
{
    // ────────────────────────────────────────────────────────────────────────────
    // Serializable dialogue line
    // ────────────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class DialogueLine
    {
        [TextArea(1, 4)]
        public string text;

        [Tooltip("Audio clip for this dialogue line (supports .flac, .mp3, .wav, etc.). Played when this line begins typing.")]
        public AudioClip audioClip;

        [Tooltip("Pause in seconds AFTER this line finishes typing / speaking, before the next line.")]
        public float pauseAfter = 1.2f;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Main Sequencer
    // ────────────────────────────────────────────────────────────────────────────

    public class MainMenuSequencer : MonoBehaviour
    {
        // ── Inspector — Dialogue ─────────────────────────────────────────────

        [Header("Dialogue")]
        [Tooltip("The existing TypewriterDialogue component in the scene.")]
        [SerializeField] private TypewriterDialogue typewriterDialogue;

        [Tooltip("The TMP_Text used for dialogue lines (DialogueText).")]
        [SerializeField] private TMP_Text dialogueText;

        [Tooltip("The dialogue lines to display before the cinematic.")]
        [SerializeField] private DialogueLine[] dialogueLines = new DialogueLine[]
        {
            new DialogueLine { text = "I remember the rain.", pauseAfter = 1.2f },
            new DialogueLine { text = "I remember the house.", pauseAfter = 1.2f },
            new DialogueLine { text = "But I don't remember...", pauseAfter = 1.8f },
            new DialogueLine { text = "His face.", pauseAfter = 2.0f },
        };

        [Tooltip("Pause before the first dialogue line starts.")]
        [SerializeField] private float dialogueStartDelay = 0.8f;

        // ── Inspector — Voice ────────────────────────────────────────────────

        [Header("Dialogue Voice")]
        [Tooltip("AudioSource that plays dialogue line audio clips. If unassigned, one is created automatically on this GameObject.")]
        [SerializeField] private AudioSource voiceSource;

        [Range(0f, 1f)]
        [Tooltip("Volume for dialogue voice clips.")]
        [SerializeField] private float dialogueVoiceVolume = 1.0f;

        [Tooltip("Optional standalone voice clip to play after all dialogue ends.")]
        [SerializeField] private AudioClip voiceClip;

        [Tooltip("Seconds to wait after voice clip finishes before starting the video.")]
        [SerializeField] private float voiceToVideoDelay = 0.5f;

        // ── Inspector — Cinematic Audio ──────────────────────────────────────

        [Header("Cinematic Audio")]
        [Tooltip("Parent or AudioSources that form the cinematic audio. Faded in when video starts.")]
        [SerializeField] private AudioSource[] cinematicAudioSources;

        [Tooltip("Target volumes for each cinematic audio source (matched by index).")]
        [SerializeField] private float[] cinematicAudioTargetVolumes;

        [Tooltip("Duration to fade cinematic audio in.")]
        [SerializeField] private float cinematicFadeInDuration = 1.0f;

        [Tooltip("Duration to fade cinematic audio out when transitioning to main menu.")]
        [SerializeField] private float cinematicFadeOutDuration = 1.5f;

        // ── Inspector — Video ────────────────────────────────────────────────

        [Header("Video")]
        [Tooltip("The existing VideoPlayer in the scene.")]
        [SerializeField] private VideoPlayer videoPlayer;

        // ── Inspector — Post-Video Transition ───────────────────────────────

        [Header("Post-Video Transition")]
        [Tooltip("CanvasGroup on the FadePanel — used to fade to/from black after the video.")]
        [SerializeField] private CanvasGroup fadePanelGroup;

        [Tooltip("Duration to fade to black after the video ends.")]
        [SerializeField] private float videoFadeOutDuration = 0.8f;

        [Tooltip("The VideoImage GameObject — hidden after fading to black.")]
        [SerializeField] private GameObject videoImageObject;

        [Tooltip("The GameObject (UI Image or RawImage) showing the PNG menu background. Disabled at start; enabled after fade.")]
        [SerializeField] private GameObject menuBackgroundObject;

        [Tooltip("Duration to fade back in from black to reveal the PNG background.")]
        [SerializeField] private float videoFadeInDuration = 1.0f;

        // ── Inspector — Main Menu ────────────────────────────────────────────

        [Header("Main Menu")]
        [Tooltip("MenuRoot GameObject — disabled until the menu activates.")]
        [SerializeField] private GameObject menuRoot;

        [Tooltip("Main menu audio sources (faded in when menu activates).")]
        [SerializeField] private AudioSource[] mainMenuAudioSources;

        [Tooltip("Target volumes for each main menu audio source (matched by index).")]
        [SerializeField] private float[] mainMenuAudioTargetVolumes;

        [Tooltip("Duration to fade main menu audio in.")]
        [SerializeField] private float menuAudioFadeInDuration = 1.5f;

        [Tooltip("The MenuLookController — enabled when menu activates.")]
        [SerializeField] private MenuLookController lookController;

        [Header("Main Menu Thunder")]
        [Tooltip("Play periodic thunder strikes in the main menu as well.")]
        [SerializeField] private bool playThunderInMenu = true;

        [Tooltip("Thunder AudioSource for main menu. If empty, uses BlackScreenController's ThunderSource.")]
        [SerializeField] private AudioSource menuThunderSource;

        [Tooltip("Thunder clips for main menu. If empty, uses BlackScreenController's ThunderClips.")]
        [SerializeField] private AudioClip[] menuThunderClips;

        [SerializeField] private float menuThunderMinInterval = 8f;
        [SerializeField] private float menuThunderMaxInterval = 25f;

        private Coroutine menuThunderRoutine;

        // ── Inspector — Dialogue Text Fade ───────────────────────────────────

        [Header("Dialogue Text Fade")]
        [Tooltip("Duration to fade out dialogue text after all lines are done.")]
        [SerializeField] private float dialogueFadeOutDuration = 0.8f;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            // Ensure menu is disabled at start
            if (menuRoot != null) menuRoot.SetActive(false);

            // Hide PNG background until we need it
            if (menuBackgroundObject != null) menuBackgroundObject.SetActive(false);

            // Hide Video image until video actually plays (avoids showing paused 1st frame)
            if (videoImageObject != null) videoImageObject.SetActive(false);

            // Disable head look movement
            if (lookController != null) lookController.enabled = false;

            // FadePanel setup: ensure it exists, starts fully transparent, and is solid black
            if (fadePanelGroup == null)
            {
                GameObject fp = GameObject.Find("FadePanel");
                if (fp != null) fadePanelGroup = fp.GetComponent<CanvasGroup>();
            }

            if (fadePanelGroup != null)
            {
                fadePanelGroup.alpha = 0f;
                fadePanelGroup.blocksRaycasts = false;

                Image fadeImg = fadePanelGroup.GetComponent<Image>();
                if (fadeImg != null)
                {
                    fadeImg.sprite = null;
                    fadeImg.color = Color.black; // Ensure it's 100% solid black!
                }
            }

            // Ensure BlackScreenController is active at runtime even if hidden in editor while arranging UI
            if (blackScreenController == null)
            {
                blackScreenController = FindObjectOfType<BlackScreenController>(true);
            }
            if (blackScreenController != null)
            {
                blackScreenController.gameObject.SetActive(true);
            }

            // Ensure main menu audio sources are silent at start
            // CRITICAL: Do NOT mute audio sources belonging to BlackScreenController (like rain or thunder)!
            if (mainMenuAudioSources != null)
            {
                foreach (var src in mainMenuAudioSources)
                {
                    if (src == null) continue;
                    if (blackScreenController != null && blackScreenController.IsBlackScreenAudioSource(src))
                        continue; // Leave rain and thunder untouched for the start prompt!

                    src.volume = 0f;
                }
            }

            // Ensure cinematic audio sources are silent at start
            if (cinematicAudioSources != null)
            {
                foreach (var src in cinematicAudioSources)
                {
                    if (src == null) continue;
                    if (blackScreenController != null && blackScreenController.IsBlackScreenAudioSource(src))
                        continue;

                    src.volume = 0f;
                }
            }

            // Clear dialogue text
            if (dialogueText != null) dialogueText.text = "";

            // Configure video player audio to Direct mode so video sound plays clearly to speakers
            if (videoPlayer != null)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                videoPlayer.EnableAudioTrack(0, true);
                videoPlayer.SetDirectAudioMute(0, false);
                videoPlayer.SetDirectAudioVolume(0, 1f);
            }

            // Setup voice AudioSource for dialogue lines
            if (voiceSource == null)
            {
                voiceSource = GetComponent<AudioSource>();
                if (voiceSource == null)
                {
                    voiceSource = gameObject.AddComponent<AudioSource>();
                }
            }
            if (voiceSource != null)
            {
                voiceSource.playOnAwake = false;
                voiceSource.loop = false;
                voiceSource.spatialBlend = 0f; // 2D flat stereo sound so dialogue audio is clearly audible
                voiceSource.volume = dialogueVoiceVolume;
            }
        }

        private BlackScreenController blackScreenController;

        private enum SequencePhase
        {
            Idle,
            Dialogue,
            Voice,
            Video,
            TransitionToMenu,
            MenuReady
        }

        private SequencePhase currentPhase = SequencePhase.Idle;
        private bool skipRequested = false;
        private Coroutine activeLineCoroutine;

        // ── Input Detection ───────────────────────────────────────────────────

        private void Update()
        {
            if (IsEscapePressed())
            {
                HandleEscapePress();
            }
        }

        private bool IsEscapePressed()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;

#pragma warning disable CS0618
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    return true;
            }
            catch { }
#pragma warning restore CS0618

            return false;
        }

        private void HandleEscapePress()
        {
            if (currentPhase == SequencePhase.Dialogue || currentPhase == SequencePhase.Voice)
            {
                // Skip all dialogue and voice immediately -> advance directly to video
                skipRequested = true;

                if (typewriterDialogue != null)
                {
                    typewriterDialogue.StopAllCoroutines();
                    typewriterDialogue.StopTypingAudio();
                }

                if (dialogueText != null)
                    dialogueText.text = "";

                if (voiceSource != null && voiceSource.isPlaying)
                    voiceSource.Stop();
            }
            else if (currentPhase == SequencePhase.Video)
            {
                // Skip video immediately -> advance directly to main menu
                skipRequested = true;

                if (videoPlayer != null && videoPlayer.isPlaying)
                    videoPlayer.Stop();
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Called by BlackScreenController after the prompt text and rain audio have faded out.</summary>
        public void BeginDialogueSequence(BlackScreenController bsc = null)
        {
            if (bsc != null) blackScreenController = bsc;
            else if (blackScreenController == null) blackScreenController = FindAnyObjectByType<BlackScreenController>();

            StartCoroutine(DialogueThenCinematicRoutine());
        }

        // ── Coroutines ────────────────────────────────────────────────────────

        private IEnumerator DialogueThenCinematicRoutine()
        {
            // ── DIALOGUE PHASE ───────────────────────────────────────────────
            currentPhase = SequencePhase.Dialogue;
            skipRequested = false;

            bool hasActiveDialogue = typewriterDialogue != null 
                                     && typewriterDialogue.gameObject.activeInHierarchy 
                                     && dialogueLines != null 
                                     && dialogueLines.Length > 0;

            if (hasActiveDialogue)
            {
                // Brief pause before dialogue starts
                float delayTimer = 0f;
                while (delayTimer < dialogueStartDelay && !skipRequested)
                {
                    delayTimer += Time.unscaledDeltaTime;
                    yield return null;
                }

                foreach (DialogueLine line in dialogueLines)
                {
                    if (skipRequested) break;
                    if (string.IsNullOrEmpty(line.text)) continue;

                    // Play this line's audio clip (supports .flac, .mp3, .wav, etc.)
                    if (line.audioClip != null && voiceSource != null)
                    {
                        voiceSource.clip = line.audioClip;
                        voiceSource.volume = dialogueVoiceVolume;
                        voiceSource.Play();
                    }

                    bool lineDone = false;
                    IEnumerator LineWrapper()
                    {
                        yield return typewriterDialogue.PlayLine(line.text);
                        lineDone = true;
                    }

                    activeLineCoroutine = StartCoroutine(LineWrapper());
                    while (!lineDone && !skipRequested)
                    {
                        yield return null;
                    }

                    if (activeLineCoroutine != null)
                    {
                        StopCoroutine(activeLineCoroutine);
                        activeLineCoroutine = null;
                    }

                    if (skipRequested) break;

                    // If dialogue audio is still playing after typing finishes, wait for it to complete
                    while (voiceSource != null && voiceSource.isPlaying && !skipRequested)
                    {
                        yield return null;
                    }

                    if (skipRequested) break;

                    // Pause after line
                    float pauseTimer = 0f;
                    while (pauseTimer < line.pauseAfter && !skipRequested)
                    {
                        pauseTimer += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    // Clear text between lines
                    if (dialogueText != null) dialogueText.text = "";
                }

                // Fade out dialogue text if not skipped
                if (!skipRequested && dialogueText != null && dialogueText.gameObject.activeInHierarchy)
                {
                    yield return StartCoroutine(FadeDialogueText(0f, dialogueFadeOutDuration));
                }
                else if (dialogueText != null)
                {
                    dialogueText.text = "";
                }
            }

            // ── VOICE CLIP PHASE ─────────────────────────────────────────────
            currentPhase = SequencePhase.Voice;

            if (!skipRequested && voiceSource != null && voiceClip != null)
            {
                voiceSource.clip = voiceClip;
                voiceSource.Play();

                float voiceDuration = voiceClip.length + voiceToVideoDelay;
                float voiceTimer = 0f;
                while (voiceTimer < voiceDuration && !skipRequested)
                {
                    voiceTimer += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (skipRequested && voiceSource.isPlaying)
                {
                    voiceSource.Stop();
                }
            }

            // ── CINEMATIC AUDIO START ─────────────────────────────────────────
            StartCinematicAudio();

            // ── VIDEO START ──────────────────────────────────────────────────
            currentPhase = SequencePhase.Video;
            skipRequested = false; // Reset so player can press Esc again to skip the video!

            // Reveal video image and smoothly fade out the black screen panel
            if (videoImageObject != null) videoImageObject.SetActive(true);

            if (blackScreenController != null)
            {
                yield return StartCoroutine(blackScreenController.FadeOutBlackPanel(0.3f));
            }

            if (videoPlayer != null)
            {
                // Ensure audio is enabled and plays directly to speakers without buffer overflow
                videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                ushort trackCount = videoPlayer.audioTrackCount;
                if (trackCount > 0)
                {
                    for (ushort i = 0; i < trackCount; i++)
                    {
                        videoPlayer.EnableAudioTrack(i, true);
                        videoPlayer.SetDirectAudioMute(i, false);
                        videoPlayer.SetDirectAudioVolume(i, 1f);
                    }
                }
                else
                {
                    videoPlayer.EnableAudioTrack(0, true);
                    videoPlayer.SetDirectAudioMute(0, false);
                    videoPlayer.SetDirectAudioVolume(0, 1f);
                }

                bool videoEnded = false;
                UnityEngine.Video.VideoPlayer.EventHandler onLoopPoint = vp => videoEnded = true;
                videoPlayer.loopPointReached += onLoopPoint;

                videoPlayer.Play();

                // Wait until playback actually starts
                float startWait = 0f;
                while (!videoPlayer.isPlaying && videoPlayer.frame <= 0 && startWait < 3f && !skipRequested)
                {
                    startWait += Time.unscaledDeltaTime;
                    yield return null;
                }

                double videoLength = videoPlayer.length > 0 ? videoPlayer.length : (videoPlayer.clip != null ? videoPlayer.clip.length : 0);
                long totalFrames = (long)videoPlayer.frameCount;
                float playbackTimer = 0f;
                float maxAllowedTime = videoLength > 0 ? (float)videoLength + 0.6f : 300f;

                // Monitor playback until end is reached or skipped
                while (!videoEnded && !skipRequested && playbackTimer < maxAllowedTime)
                {
                    playbackTimer += Time.unscaledDeltaTime;

                    // Condition 1: reached last frames
                    if (totalFrames > 0 && videoPlayer.frame >= totalFrames - 2)
                    {
                        videoEnded = true;
                        break;
                    }

                    // Condition 2: time reached end
                    if (videoLength > 0 && videoPlayer.time >= videoLength - 0.15)
                    {
                        videoEnded = true;
                        break;
                    }

                    // Condition 3: playback paused/stopped on last frame
                    if (playbackTimer > 0.5f && !videoPlayer.isPlaying)
                    {
                        videoEnded = true;
                        break;
                    }

                    yield return null;
                }

                // Unsubscribe and stop
                videoPlayer.loopPointReached -= onLoopPoint;
                if (videoPlayer.isPlaying) videoPlayer.Stop();
            }

            // ── VIDEO ENDED → FADE TO BLACK ───────────────────────────────────
            // ── 1. VIDEO ENDED → FADE TO SOLID BLACK ─────────────────────────
            currentPhase = SequencePhase.TransitionToMenu;

            // Fade screen smoothly to solid black
            float fadeOutDuration = skipRequested ? 0.3f : videoFadeOutDuration;
            yield return StartCoroutine(FadePanel(0f, 1f, fadeOutDuration));

            // ── 2. WHILE SCREEN IS 100% BLACK: REVEAL MENU & CLEAN UP ────────
            if (videoImageObject != null) videoImageObject.SetActive(false);
            if (videoPlayer != null)     videoPlayer.Stop();

            // Reveal both PNG background and MenuRoot while hidden behind black screen
            if (menuBackgroundObject != null) menuBackgroundObject.SetActive(true);
            if (menuRoot != null)             menuRoot.SetActive(true);

            // Ensure cursor is free and visible for the player
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Ensure any overlay panels (DialoguePanel, StartPrompt) are disabled
            GameObject dialoguePanel = GameObject.Find("DialoguePanel");
            if (dialoguePanel != null) dialoguePanel.SetActive(false);

            GameObject startPrompt = GameObject.Find("StartPrompt");
            if (startPrompt != null) startPrompt.SetActive(false);

            // Crossfade audio to main menu track
            StartCoroutine(FadeOutCinematicAudio());
            StartCoroutine(FadeInMainMenuAudio());

            // Brief 0.2s pause in solid blackness for a dramatic, cinematic transition
            yield return new WaitForSecondsRealtime(0.2f);

            // ── 3. FADE OUT FROM BLACK → REVEAL MAIN MENU ─────────────────────
            float fadeInDuration = skipRequested ? 0.35f : videoFadeInDuration;
            yield return StartCoroutine(FadePanel(1f, 0f, fadeInDuration));

            currentPhase = SequencePhase.MenuReady;
        }

        // ── Cinematic Audio ───────────────────────────────────────────────────

        private void StartCinematicAudio()
        {
            if (cinematicAudioSources == null) return;

            for (int i = 0; i < cinematicAudioSources.Length; i++)
            {
                AudioSource src = cinematicAudioSources[i];
                if (src == null) continue;

                float targetVol = (cinematicAudioTargetVolumes != null && i < cinematicAudioTargetVolumes.Length)
                    ? cinematicAudioTargetVolumes[i]
                    : 1f;

                src.volume = 0f;
                src.Play();
                StartCoroutine(FadeAudioSource(src, 0f, targetVol, cinematicFadeInDuration));
            }
        }

        private IEnumerator FadeOutCinematicAudio()
        {
            if (cinematicAudioSources == null) yield break;

            float elapsed = 0f;
            float[] startVols = new float[cinematicAudioSources.Length];
            for (int i = 0; i < cinematicAudioSources.Length; i++)
                startVols[i] = cinematicAudioSources[i] != null ? cinematicAudioSources[i].volume : 0f;

            while (elapsed < cinematicFadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / cinematicFadeOutDuration);
                float smooth = t * t * (3f - 2f * t);
                for (int i = 0; i < cinematicAudioSources.Length; i++)
                {
                    if (cinematicAudioSources[i] != null)
                        cinematicAudioSources[i].volume = Mathf.Lerp(startVols[i], 0f, smooth);
                }
                yield return null;
            }

            foreach (var src in cinematicAudioSources)
                if (src != null) { src.volume = 0f; src.Stop(); }
        }

        // ── Main Menu Audio ───────────────────────────────────────────────────

        private IEnumerator FadeInMainMenuAudio()
        {
            if (mainMenuAudioSources != null)
            {
                for (int i = 0; i < mainMenuAudioSources.Length; i++)
                {
                    AudioSource src = mainMenuAudioSources[i];
                    if (src == null) continue;

                    // If thunder source was placed in mainMenuAudioSources, handle it via thunder loop instead of continuous loop
                    if ((blackScreenController != null && src == blackScreenController.ThunderSource) || src == menuThunderSource)
                    {
                        continue;
                    }

                    float targetVol = (mainMenuAudioTargetVolumes != null && i < mainMenuAudioTargetVolumes.Length)
                        ? mainMenuAudioTargetVolumes[i]
                        : 1f;

                    src.volume = 0f;
                    src.loop = true;
                    src.Play();
                    StartCoroutine(FadeAudioSource(src, 0f, targetVol, menuAudioFadeInDuration));
                }
            }

            if (playThunderInMenu)
            {
                StartMenuThunder();
            }

            yield return new WaitForSecondsRealtime(menuAudioFadeInDuration);
        }

        private void StartMenuThunder()
        {
            if (menuThunderRoutine != null) StopCoroutine(menuThunderRoutine);

            AudioSource targetSrc = menuThunderSource;
            if (targetSrc == null && blackScreenController != null)
                targetSrc = blackScreenController.ThunderSource;

            if (targetSrc != null)
            {
                menuThunderRoutine = StartCoroutine(MenuThunderLoop(targetSrc));
            }
        }

        private IEnumerator MenuThunderLoop(AudioSource src)
        {
            while (true)
            {
                float waitTime = Random.Range(menuThunderMinInterval, menuThunderMaxInterval);
                yield return new WaitForSecondsRealtime(waitTime);

                AudioClip clip = null;
                if (menuThunderClips != null && menuThunderClips.Length > 0)
                    clip = menuThunderClips[Random.Range(0, menuThunderClips.Length)];
                else if (blackScreenController != null && blackScreenController.ThunderClips != null && blackScreenController.ThunderClips.Length > 0)
                    clip = blackScreenController.ThunderClips[Random.Range(0, blackScreenController.ThunderClips.Length)];
                else if (src.clip != null)
                    clip = src.clip;

                if (clip != null && src != null)
                {
                    float vol = (blackScreenController != null) ? blackScreenController.ThunderVolume : 1f;
                    src.PlayOneShot(clip, vol);
                }
            }
        }

        private void OnDestroy()
        {
            if (menuThunderRoutine != null) StopCoroutine(menuThunderRoutine);
        }

        // ── Activate Menu ─────────────────────────────────────────────────────

        private void ActivateMainMenu()
        {
            // Ensure mouse cursor is visible and free for standard clicking
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Ensure any overlay panels (DialoguePanel, StartPrompt) are disabled so they never block clicks
            GameObject dialoguePanel = GameObject.Find("DialoguePanel");
            if (dialoguePanel != null) dialoguePanel.SetActive(false);

            GameObject startPrompt = GameObject.Find("StartPrompt");
            if (startPrompt != null) startPrompt.SetActive(false);

            if (fadePanelGroup != null)
            {
                fadePanelGroup.alpha = 0f;
                fadePanelGroup.blocksRaycasts = false;
            }

            if (menuRoot != null)
            {
                menuRoot.SetActive(true);
                // Ensure MenuRoot renders in front of all other UI in ViewRoot
                menuRoot.transform.SetAsLastSibling();
            }
        }

        // ── Fade Panel ────────────────────────────────────────────────────────

        private IEnumerator FadePanel(float from, float to, float duration)
        {
            if (fadePanelGroup == null) yield break;

            Image fadeImg = fadePanelGroup.GetComponent<Image>();
            if (fadeImg != null)
            {
                fadeImg.sprite = null;
                fadeImg.color = Color.black;
            }

            fadePanelGroup.blocksRaycasts = true;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fadePanelGroup.alpha = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
                yield return null;
            }

            fadePanelGroup.alpha = to;

            // Stop blocking raycasts when fully transparent
            if (to < 0.01f) fadePanelGroup.blocksRaycasts = false;
        }

        // ── Dialogue Text Fade ────────────────────────────────────────────────

        private IEnumerator FadeDialogueText(float targetAlpha, float duration)
        {
            if (dialogueText == null) yield break;
            Color c = dialogueText.color;
            float startAlpha = c.a;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = Mathf.Lerp(startAlpha, targetAlpha, t * t * (3f - 2f * t));
                dialogueText.color = c;
                yield return null;
            }
            c.a = targetAlpha;
            dialogueText.color = c;
        }

        // ── Generic Audio Fade ────────────────────────────────────────────────

        private IEnumerator FadeAudioSource(AudioSource src, float from, float to, float duration)
        {
            if (src == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                src.volume = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
                yield return null;
            }
            src.volume = to;
        }

        // ── Built-in Button Events (Use in Unity Button OnClick) ─────────────

        /// <summary>Hook this into a standard Unity Button OnClick() to load "Final"</summary>
        public void OpenFinalScene()
        {
            StartCoroutine(FadeAndLoad("Final"));
        }

        /// <summary>Hook this into a standard Unity Button OnClick() to quit the game</summary>
        public void QuitGame()
        {
            StartCoroutine(FadeAndQuit());
        }

        /// <summary>Hook this into a standard Unity Button OnClick() with any scene name</summary>
        public void LoadScene(string sceneName)
        {
            StartCoroutine(FadeAndLoad(sceneName));
        }

        private IEnumerator FadeAndLoad(string sceneName)
        {
            yield return StartCoroutine(FadePanel(0f, 1f, 0.6f));
            SceneManager.LoadScene(sceneName);
        }

        private IEnumerator FadeAndQuit()
        {
            yield return StartCoroutine(FadePanel(0f, 1f, 0.6f));
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
