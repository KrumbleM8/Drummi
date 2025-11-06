using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreScreen : MonoBehaviour
{
    [Header("Score Display")]
    public TMP_Text finalScoreText;
    public TMP_Text perfectHitsText;
    public TMP_Text highScoreText;

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

    private int targetScore;
    private int displayedScore;
    private bool isAnimating;

    private void OnEnable()
    {
        if (!animateScore && targetScore > 0)
        {
            SetScoreImmediate();
        }
    }

    public void DisplayScore(int score, int perfectHits)
    {
        targetScore = score;

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

        int highScore = PlayerPrefs.GetInt("GlitchyHS", 0);
        if (highScoreText != null)
        {
            if (score >= highScore)
            {
                highScoreText.text = "NEW HIGH SCORE!";
                highScoreText.color = Color.yellow;
            }
            else
            {
                highScoreText.text = $"High Score: {highScore}";
                highScoreText.color = Color.white;
            }
        }

        UpdateGradeDisplay(score);

        Debug.Log($"[ScoreScreen] Displaying - Score: {score}, Perfect Hits: {perfectHits}, High Score: {highScore}");
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

            gradeText.color = grade switch
            {
                "S" => new Color(1f, 0.84f, 0f),
                "A" => new Color(0f, 1f, 0.5f),
                "B" => new Color(0.3f, 0.7f, 1f),
                "C" => new Color(1f, 0.6f, 0.2f),
                "D" => new Color(0.7f, 0.7f, 0.7f),
                _ => Color.white
            };
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