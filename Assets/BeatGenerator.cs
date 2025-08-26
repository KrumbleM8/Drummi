using System.Collections.Generic;
using UnityEngine;

public class BeatGenerator : MonoBehaviour
{
    public Metronome metronome;
    public BeatVisualScheduler beatVisualScheduler;
    public PlayerInputVisualHandler playerInputVisual;
    public BeatEvaluator evaluator;
    public PlayerInputReader playerInputReader;
    public CustardAnimationHandler custardAnimator;

    public List<AudioSource> leftBongoSources;
    public List<AudioSource> rightBongoSources;

    public int maxBeats = 8;
    public int difficultyIndex = 0;
    public int maxSameSideHits = 2;

    private readonly float[] starterBeatDurations = { 1f };
    private readonly float[] standardBeatDurations = { 1f, 0.5f };
    //private readonly float[] standardBeatDurations = { 1f, 0.5f, 0.25f };
    private readonly float[] spicyBeatDurations = { 0.75f, 0.5f, 0.25f };
    //private readonly float[] spicyBeatDurations = { 1f, 0.75f, 0.5f, 0.25f };
    private float[] chosenBeatDurations;

    public float evaluationBeatThreshold = 7.5f;
    private bool evaluationTriggered = false;

    private int leftBongoIndex = 0;
    private int rightBongoIndex = 0;

    public List<ScheduledBeat> scheduledBeats = new List<ScheduledBeat>();

    private bool isPaused = false;
    public bool IsPaused => isPaused;

    private double VirtualDspTime() => AudioSettings.dspTime - GameManager.instance.totalPausedTime;

    private double loopStartTime = 0.0;
    public double patternStartTime = 0.0;
    public double inputStartTime { get; private set; }

    private double beatInterval;
    public float playbackOffset;

    private double gracePeriodEndTime = 0.0;
    private bool gracePeriodActive = true;

    public bool normalGeneration = true;

    private bool previousSide;

    private void OnEnable()
    {
        metronome.OnTickEvent += HandleOnTick;
        metronome.OnFreshBarEvent += HandleOnFreshBar;
    }

    private void OnDisable()
    {
        metronome.OnTickEvent -= HandleOnTick;
        metronome.OnFreshBarEvent -= HandleOnFreshBar;
    }

    public void SetBPM()
    {
        beatInterval = 60.0 / metronome.bpm;
        gracePeriodEndTime = VirtualDspTime() + (8 * beatInterval);
        loopStartTime = VirtualDspTime();

        switch (metronome.bpm)
        {
            case 111:
                playbackOffset = 0;
                break;
            case 105:
                playbackOffset = 0.05f;
                break;
            default:
                playbackOffset = 0;
                break;
        }

        metronome.RefreshValues();
    }

    private void Start()
    {
        chosenBeatDurations = difficultyIndex switch
        {
            0 => starterBeatDurations,
            1 => standardBeatDurations,
            2 => spicyBeatDurations,
            _ => spicyBeatDurations,
        };
    }

    private void Update()
    {
        if (isPaused) return;

        double currentTime = VirtualDspTime();
        double currentLoopTime = currentTime - loopStartTime;

        if (gracePeriodActive && currentTime >= gracePeriodEndTime)
        {
            gracePeriodActive = false;
        }

        if (!evaluationTriggered && !gracePeriodActive && currentLoopTime >= evaluationBeatThreshold * beatInterval)
        {
            EvaluateOneQuaverBeforeBar();
        }
    }

    private void EvaluateOneQuaverBeforeBar()
    {
        evaluator.EvaluatePlayerInput(playerInputReader.playerInputData);
        playerInputReader.allowInput = false;
        playerInputReader.ResetInputs();
        GenerateNewPattern();
        PlayPattern();
        evaluationTriggered = true;
    }

    private void HandleOnTick()
    {
        double quaver = metronome.timePerTick * 0.5;
        if (metronome.bpm == 111)
        {
            quaver = quaver - 0.05;
        }
        double beatTime = metronome.nextBeatTime;

        if (metronome.loopBeatCount == 3)
        {
            AudioManager.instance.PlayTurnSignal(beatTime + quaver);
        }

        if (metronome.loopBeatCount == 4)
        {
            inputStartTime = metronome.nextBeatTime;
            playerInputReader.allowInput = true;
            Invoke(nameof(SetListenAnimation), (float)(metronome.timePerTick / 1.5f));
        }
    }



    private void SetListenAnimation()
    {
        custardAnimator.HandleListening();
    }

    private void HandleOnFreshBar()
    {
        loopStartTime = VirtualDspTime();
        Invoke(nameof(DelayReset), 1);
    }

    private void DelayReset()
    {
        evaluationTriggered = false;
    }

    public void GenerateNewPattern()
    {
        beatVisualScheduler.ResetVisuals();
        playerInputVisual.ResetVisuals();
        scheduledBeats.Clear();
        evaluationTriggered = false;

        float timeSlot = 0f;
        float measureLength = 3.5f;
        beatPattern.Clear();

        // Single loop covers both “normal” and “else” modes
        while (timeSlot < measureLength)
        {
            // pick a duration that will fit
            List<float> validDurations = new();
            foreach (float d in chosenBeatDurations)
                if (timeSlot + d <= measureLength)
                    validDurations.Add(d);

            if (validDurations.Count == 0) break;

            float chosenDuration = validDurations[Random.Range(0, validDurations.Count)];

            // decide side, but check last maxSameSideHits entries
            bool isBongoSide;
            if (beatPattern.Count >= maxSameSideHits)
            {
                // look at the side of the last beat
                bool lastSide = beatPattern[^1].isBongoSide;
                // check if the previous maxSameSideHits are all the same
                bool allSame = true;
                for (int i = 1; i <= maxSameSideHits; i++)
                {
                    if (beatPattern[^i].isBongoSide != lastSide)
                    {
                        allSame = false;
                        break;
                    }
                }
                // if they were all the same, force a flip; otherwise, random
                isBongoSide = allSame ? !lastSide : (Random.value > 0.5f);
            }
            else
            {
                // fewer than maxSameSideHits exist, so just random
                isBongoSide = Random.value > 0.5f;
            }

            // add and advance
            beatPattern.Add(new Beat(chosenDuration, timeSlot, isBongoSide));
            timeSlot += chosenDuration;
        }
    }


    private readonly List<Beat> beatPattern = new();

    public void PlayPattern()
    {
        scheduledBeats.Clear();
        double beatIntervalLocal = 60.0 / metronome.bpm;
        patternStartTime = metronome.GetNextBeatTime() + playbackOffset;

        foreach (Beat beat in beatPattern)
        {
            double scheduledTime = patternStartTime + (beat.timeSlot * beatIntervalLocal);
            scheduledBeats.Add(new ScheduledBeat(scheduledTime, beat.isBongoSide));

            // 1. Schedule sound and visual
            if (beat.isBongoSide)
            {
                rightBongoSources[rightBongoIndex].PlayScheduled(scheduledTime);
                beatVisualScheduler.ScheduleVisualBeat(scheduledTime, true);
                rightBongoIndex = (rightBongoIndex + 1) % rightBongoSources.Count;
            }
            else
            {
                leftBongoSources[leftBongoIndex].PlayScheduled(scheduledTime);
                beatVisualScheduler.ScheduleVisualBeat(scheduledTime, false);
                leftBongoIndex = (leftBongoIndex + 1) % leftBongoSources.Count;
            }

            // 2. Schedule Custard animations
            double neutralTime = scheduledTime - 0.1;
            if (neutralTime > AudioSettings.dspTime) // Only if it's not in the past
            {
                StartCoroutine(ScheduleAnimation(neutralTime, () => custardAnimator.HandleNeutral()));
            }

            if (beat.isBongoSide)
            {
                StartCoroutine(ScheduleBongoWithReturn(scheduledTime, true));
            }
            else
            {
                StartCoroutine(ScheduleBongoWithReturn(scheduledTime, false));
            }
        }

        inputStartTime = patternStartTime + (4 * beatIntervalLocal);
    }

    private System.Collections.IEnumerator ScheduleAnimation(double dspTime, System.Action action)
    {
        double delay = dspTime - AudioSettings.dspTime;
        if (delay > 0)
            yield return new WaitForSecondsRealtime((float)delay);

        action?.Invoke();
    }
    private System.Collections.IEnumerator ScheduleBongoWithReturn(double dspTime, bool isRight)
    {
        double delay = dspTime - AudioSettings.dspTime;
        if (delay > 0)
            yield return new WaitForSecondsRealtime((float)delay);

        if (isRight)
            custardAnimator.PlayRightBongo();
        else
            custardAnimator.PlayLeftBongo();

        // Hold pose long enough for readability and impact
        float holdDuration = Mathf.Max(0.3f, (float)(beatInterval * 0.9f));
        yield return new WaitForSecondsRealtime(holdDuration);

        if (custardAnimator.spriteRenderer.sprite == custardAnimator.sprites[4])
        {

        }
        else
        {
            custardAnimator.HandleNeutral();
        }
    }


    public void StartGame()
    {
        GenerateNewPattern();
    }
}
