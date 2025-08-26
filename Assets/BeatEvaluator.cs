using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BeatEvaluator : MonoBehaviour
{
    public BeatGenerator beatGenerator;
    public PlayerInputVisualHandler visualScheduler;
    public HitGrader hitGrader;
    public TMP_Text feedbackText;

    public CustardAnimationHandler custardAnimator;
    public float perfectThreshold = 0.05f;
    public float goodThreshold = 0.2f;
    public TMP_Text scoreText;
    public int score = 0;
    public bool hasFailedOnce = false;
    public int perfectHits = 0;

    public int allowedMistakes = 1;
    public int perfectReward = 200;
    public int goodReward = 100;
    public int passableReward = 50;
    // Optional: for input time debug logging
    public void LogInput(BongoInput input)
    {
        Debug.Log($"Input pressed at time: {input.inputTime}, side: {(input.isRightBongo ? "Right" : "Left")}");
        EvaluateSingleInput(input);
    }

    private void Start()
    {
        hasFailedOnce = false;
    }

    public void EvaluateSingleInput(BongoInput input)
    {
        if (Time.timeScale == 0) return;

        List<ScheduledBeat> scheduledBeats = beatGenerator.scheduledBeats;
        scheduledBeats.Sort((a, b) => a.scheduledTime.CompareTo(b.scheduledTime));

        foreach (var beat in scheduledBeats)
        {
            double scheduledTime = beat.scheduledTime + (60.0 / beatGenerator.metronome.bpm * 4);
            double delta = input.inputTime - scheduledTime;

            if (Mathf.Abs((float)delta) <= goodThreshold)
            {
                if (beat.isRightBongo == input.isRightBongo)
                {
                    if (Mathf.Abs((float)delta) <= perfectThreshold)
                    {
                        ShowFeedback("Perfect!");
                    }
                    else
                    {
                        ShowFeedback("Good!");
                    }
                    return;
                }
                else
                {
                    ShowFeedback("Oops!");
                    return;
                }
            }
        }

        ShowFeedback("Miss!");
    }


    public void EvaluatePlayerInput(List<BongoInput> playerInputs)
    {
        if (Time.timeScale == 0) return;

        List<ScheduledBeat> scheduledBeats = beatGenerator.scheduledBeats;
        int correctHits = 0;
        perfectHits = 0;

        scheduledBeats.Sort((a, b) => a.scheduledTime.CompareTo(b.scheduledTime));
        playerInputs.Sort((a, b) => a.inputTime.CompareTo(b.inputTime));

        int beatIndex = 0;
        int inputIndex = 0;

        while (beatIndex < scheduledBeats.Count && inputIndex < playerInputs.Count)
        {
            double scheduledTime = scheduledBeats[beatIndex].scheduledTime + (60.0 / beatGenerator.metronome.bpm * 4); // +4 beat delay
            double inputTime = playerInputs[inputIndex].inputTime;
            double delta = inputTime - scheduledTime;

            if (delta < -goodThreshold)
            {
                Debug.Log("Too Early - Missed");
                inputIndex++;
            }
            else if (delta > goodThreshold)
            {
                Debug.Log("Too Late - Missed");
                beatIndex++;
            }
            else
            {
                if (scheduledBeats[beatIndex].isRightBongo == playerInputs[inputIndex].isRightBongo)
                {
                    if (Mathf.Abs((float)delta) <= perfectThreshold)
                    {
                        perfectHits++;
                        Debug.Log("Hit Grade: Perfect");
                    }
                    else
                    {
                        Debug.Log("Hit Grade: Good");
                    }

                    correctHits++;
                }
                else
                {
                    Debug.Log("Hit Grade: Wrong Side");
                }

                beatIndex++;
                inputIndex++;
            }
        }

        Debug.Log($"Player hit {correctHits} out of {scheduledBeats.Count}");

        if (scheduledBeats.Count == 0)
            return;

        //Score Handling
        if (correctHits == scheduledBeats.Count - allowedMistakes) //Most Beats Hit
        {
            if (playerInputs.Count != scheduledBeats.Count)
            {
                //Failed, mistake + incorrect amount of inputs
                HandleFail();
            }
            else
            {
                //Passable ADD POINTS
                HandleSuccess(passableReward);
            }
        }
        else if (correctHits == scheduledBeats.Count) //All Beats Hit
        {
            if (playerInputs.Count != scheduledBeats.Count) //Too many or too little inputs
            {
                if (playerInputs.Count == scheduledBeats.Count - allowedMistakes || playerInputs.Count == scheduledBeats.Count + allowedMistakes)
                {
                    //Passable ADD POINTS
                    HandleSuccess(passableReward);
                }
                else
                {
                    //Failed, too many inputs
                    HandleFail();
                }
            }
            else
            {
                if (perfectHits == scheduledBeats.Count) //All Perfect
                {
                    //All Perfect ADD POINTS
                    HandleSuccess(perfectReward);
                }
                else
                {
                    //All Good ADD POINTS
                    HandleSuccess(goodReward);
                }
            }

        }
        else
        {
            HandleFail();
        }
    }

    private void HandleSuccess(int points)
    {
        Debug.Log("Successful Bar!");

        if(points == perfectReward)
        {
            AudioManager.instance.PlayAllPerfect();
        }
        else if (points == goodReward)
        {
            AudioManager.instance.PlayCorrect();
        }
        else
        {
            AudioManager.instance.PlayPassable();
        }
        custardAnimator.HandleSuccess();

        score += points;
        SaveHighScore();
        scoreText.text = score.ToString();
        hasFailedOnce = false;
    }

    private void HandleFail()
    {
        Debug.Log("No Good :(");
        custardAnimator.HandleFailure();
        AudioManager.instance.PlayIncorrect();

        if (!hasFailedOnce)
        {
            hasFailedOnce = true;
        }
        else if (score != 0)
        {
            SaveHighScore();
            score = 0;
            scoreText.text = score.ToString();
            AudioManager.instance.PlayTotalFail();
        }
    }

    public void SaveHighScore()
    {
        if(score > PlayerPrefs.GetInt("GlitchyHS")
        PlayerPrefs.SetInt("GlitchyHS", score);
    }

    private void ShowFeedback(string message)
    {
        feedbackText.text = message;
        feedbackText.rectTransform.anchoredPosition = new Vector2(visualScheduler.GetCurrentSliderXPosition(), -236); //
        CancelInvoke(nameof(ClearFeedback));
        Invoke(nameof(ClearFeedback), 0.41f); // Hide after 0.41s (or adjust as needed)
    }

    private void ClearFeedback()
    {
        feedbackText.text = "";
    }

    
}
