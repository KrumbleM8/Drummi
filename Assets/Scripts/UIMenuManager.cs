using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIMenuManager : MonoBehaviour
{
    [System.Serializable]
    public class MenuPage
    {
        public string pageName;
        public RectTransform pageTransform;
        [HideInInspector] public Vector2 onScreenPosition;
        [HideInInspector] public Vector2 offScreenLeftPosition;
        [HideInInspector] public Vector2 offScreenRightPosition;
    }

    [Header("Menu Pages")]
    [SerializeField] private List<MenuPage> menuPages = new List<MenuPage>();
    [SerializeField] private string startingPageName;

    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.3f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Screen Transitioner")]
    [SerializeField] private ScreenTransition screenTransitioner;

    [Header("Screen Dimensions")]
    [SerializeField] private RectTransform canvasRectTransform;

    [Header("Offscreen Margin (Dynamic)")]
    [Tooltip("Percent of canvas width used as base offscreen margin.")]
    [Range(0f, 5f)]
    [SerializeField] private float offscreenMarginPercentOfWidth = .42f; // 0.2 = 20%
    [Tooltip("Minimum pixel margin to guarantee hidden content on very small screens.")]
    [SerializeField] private float minOffscreenMarginPixels = 64f;
    [Tooltip("Include left/right safe-area gutters (notch/rounded corners) in the margin calculation.")]
    [SerializeField] private bool includeSafeAreaGutter = true;

    private MenuPage currentPage;
    private Coroutine activeTransition;
    private Dictionary<string, MenuPage> pageDict;
    private bool isTransitioning = false;
    private bool pagesInitialized = false;

    private void Awake()
    {

    }

    private void Start()
    {
        InitializePages();

        if (!string.IsNullOrEmpty(startingPageName))
        {
            ShowPageImmediate(startingPageName);
        }
        else if (menuPages.Count > 0)
        {
            ShowPageImmediate(menuPages[0].pageName);
        }
    }

    private void InitializePages()
    {
        if (pagesInitialized) return;

        pageDict = new Dictionary<string, MenuPage>(menuPages.Count);

        if (canvasRectTransform == null)
        {
            var parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                Debug.LogError("No parent Canvas found for UIMenuManager. Cannot initialize pages.");
                return;
            }
            canvasRectTransform = parentCanvas.GetComponent<RectTransform>();
        }

        Canvas.ForceUpdateCanvases();

        foreach (var page in menuPages)
        {
            if (page.pageTransform == null)
            {
                Debug.LogError($"Page '{page.pageName}' has no RectTransform assigned!");
                continue;
            }

            pageDict[page.pageName] = page;

            // Preserve designer on-screen position
            page.onScreenPosition = page.pageTransform.anchoredPosition;

            // Deactivate initially
            page.pageTransform.gameObject.SetActive(false);
        }

        // Build offscreen positions using dynamic margin
        RebuildOffscreenPositions();

        pagesInitialized = true;
    }

    /// <summary>
    /// Recompute off-screen positions for all pages using the current canvas size and dynamic margin.
    /// </summary>
    private void RebuildOffscreenPositions()
    {
        if (canvasRectTransform == null) return;

        Canvas.ForceUpdateCanvases();

        float canvasWidth = canvasRectTransform.rect.width;
        float margin = ComputeOffscreenMargin(canvasWidth);

        foreach (var page in menuPages)
        {
            if (page.pageTransform == null) continue;

            // Use page width so the page is fully outside even if it's wider than the canvas.
            float pageWidth = page.pageTransform.rect.width;

            // Distance to move from on-screen to fully off-screen on either side.
            float offDistance = (canvasWidth * 0.5f) + (pageWidth * 0.5f) + margin;

            Vector2 on = page.onScreenPosition;
            page.offScreenLeftPosition = new Vector2(on.x - offDistance, on.y);
            page.offScreenRightPosition = new Vector2(on.x + offDistance, on.y);
        }

        // Keep current page where it is if active; do not force-move during resize.
        if (currentPage != null && currentPage.pageTransform != null && currentPage.pageTransform.gameObject.activeSelf)
        {
            currentPage.pageTransform.anchoredPosition = currentPage.onScreenPosition;
        }
    }

    /// <summary>
    /// Computes a responsive margin in canvas units.
    /// </summary>
    private float ComputeOffscreenMargin(float canvasWidth)
    {
        // Base margin: fraction of canvas width, with a floor in pixels.
        float baseMarginCanvas = Mathf.Max(minOffscreenMarginPixels * (canvasWidth / Mathf.Max(1f, (float)Screen.width)),
                                           canvasWidth * offscreenMarginPercentOfWidth);

        if (!includeSafeAreaGutter) return baseMarginCanvas;

        // Add safe-area gutters (convert from screen pixels to canvas units).
        Rect sa = Screen.safeArea;
        float leftGutterPx = sa.xMin;
        float rightGutterPx = Screen.width - sa.xMax;
        float widestGutterPx = Mathf.Max(leftGutterPx, rightGutterPx);
        float gutterCanvas = widestGutterPx * (canvasWidth / Mathf.Max(1f, (float)Screen.width));

        return baseMarginCanvas + gutterCanvas;
    }

    public void ShowPage(string pageName, bool swipeLeft = true)
    {
        if (isTransitioning) return;

        if (!pageDict.TryGetValue(pageName, out MenuPage targetPage))
        {
            Debug.LogError($"Page '{pageName}' not found!");
            return;
        }

        if (currentPage == targetPage) return;

        if (activeTransition != null) StopCoroutine(activeTransition);
        activeTransition = StartCoroutine(TransitionToPage(targetPage, swipeLeft));
    }

    public void ShowPageByIndex(int index, bool swipeLeft = true)
    {
        if (index < 0 || index >= menuPages.Count)
        {
            Debug.LogError($"Invalid page index: {index}");
            return;
        }
        ShowPage(menuPages[index].pageName, swipeLeft);
    }

    private void ShowPageImmediate(string pageName)
    {
        if (!pageDict.TryGetValue(pageName, out MenuPage targetPage))
        {
            Debug.LogError($"Page '{pageName}' not found!");
            return;
        }

        if (currentPage != null) currentPage.pageTransform.gameObject.SetActive(false);

        currentPage = targetPage;
        currentPage.pageTransform.anchoredPosition = currentPage.onScreenPosition;
        currentPage.pageTransform.gameObject.SetActive(true);
    }

    private IEnumerator TransitionToPage(MenuPage targetPage, bool swipeLeft)
    {
        isTransitioning = true;

        MenuPage previousPage = currentPage;

        Vector2 targetStartPos = swipeLeft ? targetPage.offScreenRightPosition : targetPage.offScreenLeftPosition;
        Vector2 previousEndPos = previousPage != null
            ? (swipeLeft ? previousPage.offScreenLeftPosition : previousPage.offScreenRightPosition)
            : Vector2.zero;

        targetPage.pageTransform.anchoredPosition = targetStartPos;
        targetPage.pageTransform.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float curvedT = transitionCurve.Evaluate(t);

            targetPage.pageTransform.anchoredPosition = Vector2.Lerp(targetStartPos, targetPage.onScreenPosition, curvedT);

            if (previousPage != null)
            {
                previousPage.pageTransform.anchoredPosition = Vector2.Lerp(previousPage.onScreenPosition, previousEndPos, curvedT);
            }

            yield return null;
        }

        targetPage.pageTransform.anchoredPosition = targetPage.onScreenPosition;

        if (previousPage != null)
        {
            previousPage.pageTransform.anchoredPosition = previousEndPos;
            previousPage.pageTransform.gameObject.SetActive(false);
        }

        currentPage = targetPage;
        isTransitioning = false;
        activeTransition = null;
    }

    public void TransitionToScene(string sceneName, bool swipeLeft = true, Action onTransitionComplete = null)
    {
        if (isTransitioning) return;

        if (activeTransition != null) StopCoroutine(activeTransition);
        activeTransition = StartCoroutine(TransitionToSceneCoroutine(sceneName, swipeLeft, onTransitionComplete));
    }

    private IEnumerator TransitionToSceneCoroutine(string sceneName, bool swipeLeft, Action onTransitionComplete)
    {
        isTransitioning = true;
        screenTransitioner.StartCover();
        yield return new WaitForSeconds(screenTransitioner.transitionDuration *= 1.13f);
        onTransitionComplete?.Invoke();
        SceneManager.LoadScene(sceneName);
    }

    public bool IsTransitioning() => isTransitioning;

    public string GetCurrentPageName() => currentPage?.pageName ?? string.Empty;

    public void SetTransitionDuration(float duration) => transitionDuration = Mathf.Max(0.01f, duration);

    // Button Interface Methods
    public void ShowPageLeft(string pageName) => ShowPage(pageName, true);
    public void ShowPageRight(string pageName) => ShowPage(pageName, false);
    public void GoToAcademyScene() => TransitionToScene("Academy", swipeLeft: true);
    public void GoToGardenScene() => TransitionToScene("Garden", swipeLeft: true);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (canvasRectTransform == null)
        {
            canvasRectTransform = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        }
        // Keep offscreen math previewable in editor when values change.
        if (menuPages != null && menuPages.Count > 0) RebuildOffscreenPositions();
    }
#endif

    private void OnRectTransformDimensionsChange()
    {
        // Handles orientation or window size changes at runtime.
        if (!pagesInitialized || canvasRectTransform == null) return;
        RebuildOffscreenPositions();
    }
}
