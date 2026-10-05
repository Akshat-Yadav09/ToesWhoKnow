using UnityEngine;
using System.Collections;
using UnityEngine.Events;

namespace Akshat.Interaction
{
    public class SlidingBookshelf : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private Vector3 openLocalOffset = new Vector3(2f, 0f, 0f);
        [SerializeField] private float duration = 1.5f;
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Audio (Optional)")]
        [SerializeField] private AudioClip slideSound;
        [SerializeField] private AudioSource audioSource;
        
        [Header("Events")]
        public UnityEvent OnOpened;

        private Vector3 closedLocalPosition;
        private Vector3 openLocalPosition;
        private bool isOpening = false;
        private bool isOpen = false;
        
        private Coroutine slideRoutine;

        private void Awake()
        {
            closedLocalPosition = transform.localPosition;
            openLocalPosition = closedLocalPosition + openLocalOffset;
        }

        public void Open()
        {
            if (isOpen || isOpening) return;

            if (!gameObject.activeInHierarchy)
            {
                // If asked to open while disabled, just snap to it.
                transform.localPosition = openLocalPosition;
                isOpen = true;
                OnOpened?.Invoke();
                return;
            }

            isOpening = true;
            if (slideRoutine != null) StopCoroutine(slideRoutine);
            slideRoutine = StartCoroutine(SlideRoutine());
        }

        private IEnumerator SlideRoutine()
        {
            if (slideSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(slideSound);
            }

            Vector3 startPos = transform.localPosition;
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float curveT = curve.Evaluate(t);
                
                transform.localPosition = Vector3.Lerp(startPos, openLocalPosition, curveT);
                yield return null;
            }

            transform.localPosition = openLocalPosition;
            isOpen = true;
            isOpening = false;
            slideRoutine = null;
            
            OnOpened?.Invoke();
        }

        private void OnDisable()
        {
            if (isOpening)
            {
                if (slideRoutine != null)
                {
                    StopCoroutine(slideRoutine);
                    slideRoutine = null;
                }
                transform.localPosition = openLocalPosition;
                isOpen = true;
                isOpening = false;
                OnOpened?.Invoke();
            }
        }
    }
}
