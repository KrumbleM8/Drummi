using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreScreen : MonoBehaviour
{
    [Header("Result Title")]
    [Tooltip("Optional. Shown when a dungeon run is won ('Run Complete!'), hidden otherwise. Placeholder until a dedicated victory screen exists.")]
    public TMP_Text titleText;

    [Header("Score Display")]
    public TMP_Text finalScoreText;
    public TMP_Text perfectHitsText;
    public TMP_Text highScoreText;
    public TMP_Text roomsClearedText;

    [Header("Optional Grade Display")]
    public TMP_Text gradeText;
    public Image gradeImage;
    public Sprite sRankSprite;
    public Sprite aRankSprite;
    public Sprite bRankSprite;
    public Sprite cRankSprite;
    public Sprite dRankSprite;

    [Header("Animation Settings")]
    public bool animateScore = true;
    public float scoreAnimationDuration = 1.5f;

    [Header("Bongo Report")]
    [Tooltip("Optional. The crumpled-paper run report shown after a Bongo run. Leave empty and one is " +
             "built under this page at runtime the first time a Bongo run ends (see BongoResultsReport).")]
    public BongoResultsReport bongoReport;

    private int targetScore;
    private int displayedScore;
    private bool isAnimating;

    // Last DisplayScore call, re-used by ShowBongoReport so the paper can carry
    // the score / grade / high score instead of the separate on-screen texts.
    private int _lastScore;
    private bool _lastIsNewHighScore;
    private string _lastHighScoreKey;

    private void OnEnable()
    {
        if (!animateScore && targetScore > 0)
        {
            SetScoreImmediate();
        }
    }

    /// <param name="highScoreKey">
    /// PlayerPrefs key of the board to show when the run is not a new record.
    /// Null falls back to the legacy shared Bongo board.
    /// </param>
    public void DisplayScore(int score, int perfectHits, bool isNewHighScore = false, int roomsCleared = 0,
                             string highScoreKey = null)
    {
        targetScore = score;
        _lastScore = score;
        _lastIsNewHighScore = isNewHighScore;
        _lastHighScoreKey = highScoreKey;

        if (animateScore)
        {
            displayedScore = 0;
            isAnimating = true;
            StartCoroutine(AnimateScoreRoutine());
        }
        else
        {
            displayedScore = score;
            SetScoreImmediate();
        }

        if (perfectHitsText != null)
        {
            perfectHitsText.text = $"Perfect Hits: {perfectHits}";
        }

        if (roomsClearedText != null)
        {
            roomsClearedText.text = $"Rooms Cleared: {roomsCleared}";
        }

        if (highScoreText != null)
        {
            if (isNewHighScore)
            {
                highScoreText.text = $"NEW HIGH SCORE: {score}!";
                highScoreText.color = Color.yellow;
            }
            else
            {
                int highScore = PlayerPrefs.GetInt(string.IsNullOrEmpty(highScoreKey) ? "GlitchyHS" : highScoreKey, 0);
                highScoreText.text = $"High Score: {highScore}";
                highScoreText.color = Color.white;
            }
        }

        UpdateGradeDisplay(score);

        Debug.Log($"[ScoreScreen] Displaying - Score: {score}, Perfect Hits: {perfectHits}, Rooms Cleared: {roomsCleared}, New High Score: {isNewHighScore}");
    }

    /// <summary>
    /// Shows the Bongo paper report filled from <paramref name="stats"/>, or hides it
    /// when <paramref name="stats"/> is null (every non-Bongo mode). Called by
    /// GameManager right after <see cref="DisplayScore"/>. If no report is wired in
    /// the inspector, one is created under this page the first time it is needed.
    /// </summary>
    public void ShowBongoReport(BongoRunStats stats)
    {
        if (stats == null)
        {
            if (bongoReport != null) bongoReport.Hide();
            SetLegacyElementsVisible(true);
            return;
        }

        if (bongoReport == null)
        {
            bongoReport = GetComponentInChildren<BongoResultsReport>(true);
            if (bongoReport == null)
            {
                var go = new GameObject(BongoResultsReport.DefaultObjectName, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                bongoReport = go.AddComponent<BongoResultsReport>();
                Debug.Log("[ScoreScreen] No BongoResultsReport wired — built one under the Score page at runtime.");
            }
        }

        // The paper carries score / grade / high score itself, so the separate
        // on-screen texts and the grade icon go away while it is up.
        SetLegacyElementsVisible(false);
        if (isAnimating) { StopAllCoroutines(); isAnimating = false; }

        string grade = CalculateGrade(_lastScore);
        string highScoreText;
        if (_lastIsNewHighScore)
        {
            highScoreText = $"NEW! {_lastScore}";
        }
        else
        {
            int highScore = PlayerPrefs.GetInt(string.IsNullOrEmpty(_lastHighScoreKey) ? "GlitchyHS" : _lastHighScoreKey, 0);
            highScoreText = highScore.ToString();
        }

        bongoReport.Show(stats, new BongoResultsReport.Headline
        {
            Score = _lastScore,
            ScoreCountDuration = animateScore ? scoreAnimationDuration : 0f,
            Grade = grade,
            GradeColor = GradeColor(grade),
            HighScoreText = highScoreText,
            IsNewHighScore = _lastIsNewHighScore,
        });
    }

    /// <summary>
    /// Shows or hides the original separate result elements (final score, perfect
    /// hits, high score, grade letter, grade icon). Hidden while the Bongo paper
    /// report is up; restored for modes that don't use it.
    /// </summary>
    private void SetLegacyElementsVisible(bool visible)
    {
        SetVisible(finalScoreText, visible);
        SetVisible(perfectHitsText, visible);
        SetVisible(highScoreText, visible);
        SetVisible(gradeText, visible);
        SetVisible(gradeImage, visible);
    }

    private static void SetVisible(Component c, bool visible)
    {
        if (c != null && c.gameObject.activeSelf != visible) c.gameObject.SetActive(visible);
    }

    /// <summary>Display colour for a grade letter (as used by the on-screen grade text).</summary>
    public static Color GradeColor(string grade) => grade switch
    {
        "S" => new Color(1f, 0.84f, 0f),
        "A" => new Color(0f, 1f, 0.5f),
        "B" => new Color(0.3f, 0.7f, 1f),
        "C" => new Color(1f, 0.6f, 0.2f),
        "D" => new Color(0.7f, 0.7f, 0.7f),
        _ => Color.white
    };

    /// <summary>
    /// Shows <paramref name="title"/> above the results, or hides the title when
    /// null/empty. Called by GameManager with "Run Complete!" on a dungeon win.
    /// </summary>
    public void SetResultTitle(string title)
    {
        if (titleText == null) return;
        bool show = !string.IsNullOrEmpty(title);
        titleText.gameObject.SetActive(show);
        if (show) titleText.text = title;
    }

    private void SetScoreImmediate()
    {
        if (finalScoreText != null)
        {
            finalScoreText.text = displayedScore.ToString();
        }
    }

    private System.Collections.IEnumerator AnimateScoreRoutine()
    {
        float elapsed = 0f;
        int startScore = 0;

        while (elapsed < scoreAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scoreAnimationDuration);

            float easedT = EaseOutCubic(t);

            displayedScore = Mathf.RoundToInt(Mathf.Lerp(startScore, targetScore, easedT));

            if (finalScoreText != null)
            {
                finalScoreText.text = displayedScore.ToString();
            }

            yield return null;
        }

        displayedScore = targetScore;
        if (finalScoreText != null)
        {
            finalScoreText.text = displayedScore.ToString();
        }

        isAnimating = false;
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private void UpdateGradeDisplay(int score)
    {
        string grade = CalculateGrade(score);

        if (gradeText != null)
        {
            gradeText.text = grade;

            gradeText.color = GradeColor(grade);
        }

        if (gradeImage != null)
        {
            gradeImage.sprite = grade switch
            {
                "S" => sRankSprite,
                "A" => aRankSprite,
                "B" => bRankSprite,
                "C" => cRankSprite,
                "D" => dRankSprite,
                _ => null
            };

            gradeImage.enabled = gradeImage.sprite != null;
        }
    }

    private string CalculateGrade(int score)
    {
        if (score >= 2000) return "S";
        if (score >= 1500) return "A";
        if (score >= 1000) return "B";
        if (score >= 500) return "C";
        return "D";
    }

    public void SetScoreThresholds(int sRank, int aRank, int bRank, int cRank)
    {
        Debug.Log($"[ScoreScreen] Custom thresholds set - S:{sRank}, A:{aRank}, B:{bRank}, C:{cRank}");
    }
}

//## Usage:

//1. * *Create a new C# script** called `ScoreScreen.cs` and paste the code above

//2. * *Attach it to your ScoreScreenMenu GameObject**

//3. **In the Inspector, assign:**
//   - `Final Score Text` - TMP_Text component to display the score
//   - `Perfect Hits Text` - TMP_Text component to show perfect hits count
//   - `High Score Text` - TMP_Text component to show high score
//   - `Grade Text` (Optional) - TMP_Text to display grade letter (S, A, B, C, D)
//   - `Grade Image` (Optional) - Image component to display rank sprite
//   - Set `Animate Score` to true/false depending on if you want score counting animation
//   - Set `Score Animation Duration` (default 1.5 seconds)

//4. **The script automatically:**
//   -Animates score counting from 0 to final score
//   - Compares with high score from PlayerPrefs
//   - Shows "NEW HIGH SCORE!" if beaten
//   - Calculates and displays grade
//   - Color-codes the grade text

//## Grade Thresholds (customizable):

//- **S Rank**: 2000 + points(Gold)
//- **A Rank * *: 1500 - 1999 points(Green)
//- **B Rank * *: 1000 - 1499 points(Blue)
//- **C Rank * *: 500 - 999 points(Orange)
//- **D Rank * *: 0 - 499 points(Gray)

//You can adjust these thresholds by modifying the `CalculateGrade()` method or calling `SetScoreThresholds()` from another script.

//## Example UI Hierarchy:
//ScoreScreenMenu (GameObject)
//├── ScoreScreen (Script)
//├── Background (Image)
//├── ScorePanel (Panel)
//│   ├── FinalScoreText (TMP_Text) → "1234"
//│   ├── PerfectHitsText (TMP_Text) → "Perfect Hits: 15"
//│   ├── HighScoreText (TMP_Text) → "High Score: 2000"
//│   ├── GradeText (TMP_Text) → "A"
//│   └── GradeImage (Image) → Display rank sprite
//└── ButtonsPanel
//    ├── RetryButton
//    └── MainMenuButton