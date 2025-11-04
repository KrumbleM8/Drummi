using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIEntranceAnimator : MonoBehaviour
{
    [System.Serializable]
    public class AnimatedElement
    {
        public RectTransform element;
        [HideInInspector] public Vector2 originalPosition;
        [HideInInspector] public float originalRotation;
        [HideInInspector] public float delay;
    }

    [Header("Animation Elements")]
    [SerializeField] private List<AnimatedElement> elementsToAnimate = new List<AnimatedElement>();
    [SerializeField] private bool autoDetectChildren = true;

    [Header("Start Position")]
    [SerializeField] private RectTransform centerAnchor;
    [SerializeField] private Vector2 centerOffset = Vector2.zero;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private float zRotationOffset = -90f;
    [SerializeField] private AnimationCurve easingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Stagger Settings")]
    [SerializeField] private bool staggerAnimation = true;
    [SerializeField] private float staggerDelay = 0.05f;

    [Header("Auto-play")]
    [SerializeField] private bool playOnEnable = false;
    [SerializeField] private float playOnEnableDelay = 0f;
    [SerializeField] private bool hideElementsOnStart = true;

    [Header("Capture Options")]
    [Tooltip("If true, start point is cached in parent-local space so it follows parent motion but ignores anchor drift.")]
    [SerializeField] private bool lockStartToParent = true;
    [Tooltip("Extra frames to wait after activation/layout before capturing the fixed start. Use >0 if a menu manager repositions this object on enable.")]
    [SerializeField][Min(0)] private int captureStartExtraFrames = 1;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = false;

    private Coroutine activeAnimation;
    private bool isAnimating = false;
    private bool isInitialized = false;

    private Vector3 cachedCenterLocalPosition; // used when lockStartToParent = true
    private Vector3 cachedCenterWorldPosition; // used when lockStartToParent = false
    private bool hasCachedCenter = false;

    private RectTransform parentRect;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (hideElementsOnStart)
        {
            StartCoroutine(SetupAndPlay());
        }
        else if (playOnEnable)
        {
            StartCoroutine(DelayedPlay(playOnEnableDelay));
        }
    }

    private IEnumerator SetupAndPlay()
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        ResetToStart();

        if (playOnEnable)
        {
            if (playOnEnableDelay > 0) yield return new WaitForSeconds(playOnEnableDelay);
            PlayEntranceAnimation();
        }
    }

    private IEnumerator DelayedPlay(float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        PlayEntranceAnimation();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        parentRect = GetComponent<RectTransform>();

        if (autoDetectChildren) DetectChildren();

        if (centerAnchor == null)
        {
            centerAnchor = parentRect;
            Debug.LogWarning("No Center Anchor assigned. Using parent GameObject as center.");
        }

        CacheOriginalTransforms();
        isInitialized = true;
    }

    private void DetectChildren()
    {
        elementsToAnimate.Clear();

        if (parentRect != null)
        {
            foreach (RectTransform child in parentRect)
            {
                if (child.gameObject.activeSelf)
                {
                    elementsToAnimate.Add(new AnimatedElement { element = child });
                }
            }
        }
    }

    private void CacheOriginalTransforms()
    {
        for (int i = 0; i < elementsToAnimate.Count; i++)
        {
            if (elementsToAnimate[i].element != null)
            {
                RectTransform rt = elementsToAnimate[i].element;
                elementsToAnimate[i].originalPosition = rt.anchoredPosition;
                elementsToAnimate[i].originalRotation = rt.localEulerAngles.z;
                elementsToAnimate[i].delay = staggerAnimation ? i * staggerDelay : 0f;
            }
        }
    }

    public void PlayEntranceAnimation()
    {
        if (!isInitialized) Initialize();
        if (isAnimating) StopAnimation();
        activeAnimation = StartCoroutine(AnimateEntrance());
    }

    public void StopAnimation()
    {
        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
            activeAnimation = null;
        }
        isAnimating = false;
        hasCachedCenter = false;
    }

    public void ResetToStart()
    {
        if (!isInitialized) Initialize();

        Canvas.ForceUpdateCanvases();
        Vector3 centerWorldPos = GetCenterWorldPosition();

        foreach (var element in elementsToAnimate)
        {
            if (element.element != null)
            {
                SetWorldPosition(element.element, centerWorldPos);
                element.element.localEulerAngles = new Vector3(0, 0, element.originalRotation + zRotationOffset);
            }
        }
    }

    public void ResetToOriginal()
    {
        if (!isInitialized) Initialize();

        foreach (var element in elementsToAnimate)
        {
            if (element.element != null)
            {
                element.element.anchoredPosition = element.originalPosition;
                element.element.localEulerAngles = new Vector3(0, 0, element.originalRotation);
            }
        }
    }

    private IEnumerator AnimateEntrance()
    {
        isAnimating = true;

        // Ensure object and parent are active before capturing start.
        while (!gameObject.activeInHierarchy || !parentRect.gameObject.activeInHierarchy)
            yield return null;

        // Give the menu manager a chance to move this object on enable.
        for (int i = 0; i < captureStartExtraFrames; i++)
        {
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
        }

        Canvas.ForceUpdateCanvases();

        // Capture fixed start point now.
        Vector3 startCenterWorld = GetCenterWorldPosition();
        if (lockStartToParent)
        {
            cachedCenterLocalPosition = parentRect.InverseTransformPoint(startCenterWorld);
        }
        else
        {
            cachedCenterWorldPosition = startCenterWorld;
        }
        hasCachedCenter = true;

        // Place all elements at the cached start world position.
        Vector3 cachedStartWorld = GetCachedCenterWorldPosition();
        foreach (var element in elementsToAnimate)
        {
            if (element.element != null)
            {
                SetWorldPosition(element.element, cachedStartWorld);
                element.element.localEulerAngles = new Vector3(0, 0, element.originalRotation + zRotationOffset);
            }
        }

        yield return null;

        float maxDelay = staggerAnimation ? (elementsToAnimate.Count - 1) * staggerDelay : 0f;
        float totalDuration = animationDuration + maxDelay;
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            // Recompute start from cached parent-local if needed.
            Vector3 startWorldNow = GetCachedCenterWorldPosition();

            for (int i = 0; i < elementsToAnimate.Count; i++)
            {
                var element = elementsToAnimate[i];
                if (element.element == null) continue;

                float elementElapsed = elapsed - element.delay;

                if (elementElapsed > 0f)
                {
                    float t = Mathf.Clamp01(elementElapsed / animationDuration);
                    float curvedT = easingCurve.Evaluate(t);

                    Vector3 targetWorldPos = GetWorldPositionFromAnchoredPosition(element.element, element.originalPosition);

                    Vector3 currentWorldPos = Vector3.Lerp(startWorldNow, targetWorldPos, curvedT);
                    SetWorldPosition(element.element, currentWorldPos);

                    float currentRotation = Mathf.LerpAngle(
                        element.originalRotation + zRotationOffset,
                        element.originalRotation,
                        curvedT
                    );
                    element.element.localEulerAngles = new Vector3(0, 0, currentRotation);
                }
            }

            yield return null;
        }

        foreach (var element in elementsToAnimate)
        {
            if (element.element != null)
            {
                element.element.anchoredPosition = element.originalPosition;
                element.element.localEulerAngles = new Vector3(0, 0, element.originalRotation);
            }
        }

        isAnimating = false;
        hasCachedCenter = false;
        activeAnimation = null;
    }

    private Vector3 GetCenterWorldPosition()
    {
        if (centerAnchor == null)
        {
            return parentRect.TransformPoint(centerOffset);
        }

        Vector3[] corners = new Vector3[4];
        centerAnchor.GetWorldCorners(corners);
        Vector3 centerWorld = (corners[0] + corners[2]) * 0.5f;

        if (centerOffset != Vector2.zero)
        {
            centerWorld += parentRect.TransformVector(centerOffset);
        }

        return centerWorld;
    }

    private Vector3 GetCachedCenterWorldPosition()
    {
        if (!hasCachedCenter) return GetCenterWorldPosition();
        return lockStartToParent
            ? parentRect.TransformPoint(cachedCenterLocalPosition)
            : cachedCenterWorldPosition;
    }

    private void SetWorldPosition(RectTransform rectTransform, Vector3 worldPosition)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Vector3 currentCenter = (corners[0] + corners[2]) * 0.5f;
        Vector3 offset = worldPosition - currentCenter;
        rectTransform.position += offset;
    }

    private Vector3 GetWorldPositionFromAnchoredPosition(RectTransform rectTransform, Vector2 targetAnchoredPosition)
    {
        Vector2 originalAnchoredPos = rectTransform.anchoredPosition;
        Vector3 originalRotation = rectTransform.localEulerAngles;

        rectTransform.localEulerAngles = Vector3.zero;
        rectTransform.anchoredPosition = targetAnchoredPosition;

        Canvas.ForceUpdateCanvases();

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Vector3 worldPos = (corners[0] + corners[2]) * 0.5f;

        rectTransform.anchoredPosition = originalAnchoredPos;
        rectTransform.localEulerAngles = originalRotation;

        return worldPos;
    }

    public bool IsAnimating() => isAnimating;
    public void SetAnimationDuration(float duration) => animationDuration = Mathf.Max(0.01f, duration);
    public void SetRotationOffset(float offset) => zRotationOffset = offset;
    public void SetStaggerDelay(float delay) => staggerDelay = Mathf.Max(0f, delay);

    public void RefreshElements()
    {
        isInitialized = false;
        Initialize();
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || (!isInitialized && !Application.isPlaying)) return;
        if (parentRect == null) return;

        Vector3 centerWorld = hasCachedCenter ? GetCachedCenterWorldPosition() : GetCenterWorldPosition();

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(centerWorld, 10f);

        Gizmos.color = Color.yellow;
        foreach (var element in elementsToAnimate)
        {
            if (element.element != null)
            {
                Vector3[] corners = new Vector3[4];
                element.element.GetWorldCorners(corners);
                Vector3 elementCenter = (corners[0] + corners[2]) * 0.5f;
                Gizmos.DrawLine(centerWorld, elementCenter);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        animationDuration = Mathf.Max(0.01f, animationDuration);
        staggerDelay = Mathf.Max(0f, staggerDelay);
        captureStartExtraFrames = Mathf.Max(0, captureStartExtraFrames);
    }
#endif
}
