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

    private readonly float[] starterBeatDurations = { 1f};
    private readonly float[] standardBeatDurations = { 1f, 0.5f};
    //private readonly float[] standardBeatDurations = { 1f, 0.5f, 0.25f };
    private readonly float[] spicyBeatDurations = {0.75f, 0.5f, 0.25f };
    //private readonly float[] spicyBeatDurations = { 1f, 0.75f, 0.5f, 0.25f };
    private float[] chosenBeatDurations;

    public float evaluationBeatThreshold = 7.5f;
    private bool evaluationTriggered = false;

    private int leftBongoIndex = 0;
    private int rightBongoIndex = 0;

    public List<ScheduledBeat> scheduledBeats = new List<ScheduledBeat>();

    private double totalPausedTime = 0.0;
    private double pauseStartTime = 0.0;
    private bool isPaused = false;
    public bool IsPaused => isPaused;

    private double VirtualDspTime() => AudioSettings.dspTime - totalPausedTime;

    private double loopStartTime = 0.0;
    public double patternStartTime = 0.0;
    public double inputStartTime { get; private set; }

    private double beatInterval;
    public float playbackOffset;

    private double gracePeriodEndTime = 0.0;
    private bool gracePeriodActive = true;

    private void OnEnable()
    {
        metronome.OnTickEvent += HandleOnTick;
        metronome.OnFreshBarEvent += HandleOnFreshBar;

        SetBPM();
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
        if (metronome.loopBeatCount == 4)
        {
            inputStartTime = VirtualDspTime(); // Match this exactly to input timing
            playerInputReader.allowInput = true;
            Invoke(nameof(SetListenAnimation), (float)metronome.timePerTick / 1.5f);
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

        while (timeSlot < measureLength)
        {
            List<float> validDurations = new();
            foreach (float duration in chosenBeatDurations)
            {
                if (timeSlot + duration <= measureLength)
                    validDurations.Add(duration);
            }

            if (validDurations.Count == 0) break;

            float chosenDuration = validDurations[Random.Range(0, validDurations.Count)];
            bool isBongoSide = Random.value > 0.5f;
            beatPattern.Add(new Beat(chosenDuration, timeSlot, isBongoSide));
            timeSlot += chosenDuration;
        }
    }

    private readonly List<Beat> beatPattern = new();

    public void PlayPattern()
    {
        scheduledBeats.Clear();
        double beatIntervalLocal = 60.0 / metronome.bpm;

        // Schedule beats at the NEXT bar start
        patternStartTime = metronome.GetNextBeatTime() + playbackOffset;

        foreach (Beat beat in beatPattern)
        {
            double scheduledTime = patternStartTime + (beat.timeSlot * beatIntervalLocal);
            scheduledBeats.Add(new ScheduledBeat(scheduledTime, beat.isBongoSide));

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
        }

        // Important: Schedule inputStartTime for the **next bar**
        inputStartTime = patternStartTime + (4 * beatIntervalLocal);
    }


    public void StartGame()
    {
        GenerateNewPattern();
    }

    public void OnPause()
    {
        if (!isPaused)
        {
            isPaused = true;
            pauseStartTime = AudioSettings.dspTime;
        }
    }

    public void OnResume()
    {
        if (isPaused)
        {
            double pauseDuration = AudioSettings.dspTime - pauseStartTime;
            totalPausedTime += pauseDuration;
            isPaused = false;
        }
    }
}
