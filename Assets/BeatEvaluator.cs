using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BeatEvaluator : MonoBehaviour
{
    public BeatGenerator beatGenerator;
    public CustardAnimationHandler custardAnimator;
    public float perfectThreshold = 0.05f;
    public float goodThreshold = 0.2f;
    public TMP_Text scoreText;
    public int score = 0;
    public bool hasFailedOnce = false;

    private void Start()
    {
        hasFailedOnce = false;
    }

    public void EvaluatePlayerInput(List<BongoInput> playerInputs)
    {
        if (Time.timeScale == 0) return;

        List<ScheduledBeat> scheduledBeats = beatGenerator.scheduledBeats;
        int correctHits = 0;

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
                inputIndex++;
            }
            else if (delta > goodThreshold)
            {
                beatIndex++;
            }
            else
            {
                if (scheduledBeats[beatIndex].isRightBongo == playerInputs[inputIndex].isRightBongo)
                {
                    if (Mathf.Abs((float)delta) <= perfectThreshold)
                        Debug.Log("Perfect!");
                    else
                        Debug.Log("Good!");

                    correctHits++;
                }
                else
                {
                    Debug.Log("Wrong Side");
                }

                beatIndex++;
                inputIndex++;
            }
        }

        Debug.Log($"Player hit {correctHits} out of {scheduledBeats.Count}");

        if (scheduledBeats.Count == 0)
            return;

        if (playerInputs.Count != scheduledBeats.Count || correctHits != scheduledBeats.Count)
        {
            Debug.Log("No Good :(");
            custardAnimator.HandleFailure();
            HandleFail();
        }
        else
        {
            Debug.Log("Successful Bar!");
            custardAnimator.HandleSuccess();
            AudioManager.instance.PlayCorrect();
            score++;
            scoreText.text = score.ToString();
            hasFailedOnce = false;
        }
    }

    private void HandleFail()
    {
        AudioManager.instance.PlayIncorrect();

        if (!hasFailedOnce)
        {
            hasFailedOnce = true;
        }
        else
        {
            score = 0;
            scoreText.text = score.ToString();
            AudioManager.instance.PlayTotalFail();
        }
    }
}
