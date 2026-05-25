# Drummi Dungeons — Adjusted Build Plan
_Generated 2026-05-21 — based on full codebase scan_

---

## STEP 1 — FILE SCAN

All `.cs` files under `Assets/`, grouped by folder.

### Assets/ (root — 16 files)
```
ArcadeModeController.cs     ArcadeInputHandler.cs       ArcadeGameController.cs
NoteSpawner.cs              RhythmLaneManager.cs        HitJudge.cs
HitZoneManager.cs           RhythmScoreTracker.cs       HitZoneVisual.cs
NoteObject.cs               HitJudgement.cs             Lane.cs
NoteData.cs                 SongChart.cs                ModeController.cs
TapInvoker2D.cs
```

### Assets/Scripts/ (58 files)
```
InputSystem_Actions.cs      SettingsMenu.cs             SongIconSlotter.cs
Bootstrap.cs                Beat.cs                     BongoInput.cs
HitGrader.cs                BongoAnimator.cs            HighscoreSetter.cs
CustardAnimationHandler.cs  EyeBlinker.cs               UIRotateWobble.cs
ScheduledBeat.cs            ScoreEventHandler.cs        TapUnlessDragged.cs
TapOrDragFilter.cs          HatManager.cs               UIEntranceAnimator.cs
SceneLoaderInterface.cs     ScreenTransition.cs         AndroidDeviceFeatureDetection.cs
ScrollingTexture.cs         GameConstants.cs            GameState.cs
EvaluationResult.cs         InputMatch.cs               BounceOnBeat.cs
SongProgressionTracker.cs   TimingCoordinator.cs        CarouselController.cs
AdaptiveDifficultyManager.cs PatternGenerator.cs        DrumSoundType.cs
CameraDrag.cs               BeatVisualScheduler.cs      PlayerInputVisualHandler.cs
SceneLoadManager.cs         TreeAnimationEffect.cs      GameClock.cs
PauseHandler.cs             BongoModeInputReader.cs     DrumGridCell.cs
GardenPlot.cs               GardenInputHandler.cs       Metronome.cs
DrumMachine.cs              BeatGenerator.cs            BeatEvaluator.cs
BongoModeController.cs      ScoreScreen.cs              SongItem.cs
DungeonEnemyType.cs         DungeonBeat.cs              DungeonInput.cs
DungeonScheduledBeat.cs     DungeonPatternGenerator.cs  DrumPadTouch.cs
AudioManager.cs             GameManager.cs              UIQuickSpawnEffect.cs
DungeonHealth.cs            DungeonModeController.cs    DungeonEnemyVisual.cs
DungeonEvaluator.cs         DungeonBeatManager.cs       DungeonVisualController.cs
DungeonInputReader.cs       UIMenuManager.cs
```

### Assets/Editor/ (4 files)
```
DungeonSceneWiring.cs       DungeonSceneBuilder.cs      DungeonSceneFixes.cs
ClearSelectionOnSceneLoad.cs
```

**Total: 78 files**

---

## STEP 2 — FILE ANALYSIS

### Core Infrastructure

| File | Class / Type | Responsibility | Key Public API | Timing Source | Dependencies |
|------|-------------|----------------|---------------|---------------|-------------|
| `GameClock.cs` | MonoBehaviour (singleton) | Virtual game time with pauses subtracted from DSP time | `GameTime`, `VirtualToRealDsp()`, `Pause()`, `Resume()`, `Reset()` | `AudioSettings.dspTime` | None |
| `TimingCoordinator.cs` | MonoBehaviour (singleton) | Single source of truth for bar/beat timing; calculates all BarTiming structs | `Initialize()`, `AdvanceToNextBar()`, `ShouldEvaluateNow()`, `IsSongComplete()`, `CurrentBar`, `NextBar` | `GameClock.GameTime` | `GameClock` |
| `Metronome.cs` | MonoBehaviour | Fires `OnTickEvent` per beat; counts beats 1–8 | `InitializeWithStartTime()`, `OnPause()`, `OnResume()`, `OnTickEvent` | `AudioSettings.dspTime` | `GameClock` |
| `PauseHandler.cs` | MonoBehaviour | Coordinates pause/resume across all shared systems and active ModeController | `TogglePause()` | — | `GameClock`, `AudioManager`, `GameManager`, `Metronome` |
| `GameClock.cs` | MonoBehaviour (singleton) | Pause-aware virtual time | — | DSP | — |
| `GameState.cs` | enum | State machine values for beat generators | — | — | — |
| `GameConstants.cs` | static class | Shared timing constants (BEATS_PER_LOOP, PATTERN_MEASURE_LENGTH, etc.) | — | — | — |
| `GameManager.cs` | MonoBehaviour (singleton) | Mode registry, StartGame/Cleanup lifecycle, score screen handoff | `StartGame()`, `SetMode()`, `ResetGameValues()`, `TogglePause()` | — | `TimingCoordinator`, `Metronome`, `AudioManager`, `ModeController`, `PauseHandler` |
| `ModeController.cs` | abstract MonoBehaviour | Contract all modes implement; fires `OnModeComplete` | `StartMode()`, `Cleanup()`, `ResetToInitialState()`, `CalculateTotalBeats()`, `OnModeComplete` | — | Implemented by subclasses |

### Input

| File | Class / Type | Responsibility | Key Public API | Timing Source | Dependencies |
|------|-------------|----------------|---------------|---------------|-------------|
| `DrumPadTouch.cs` | MonoBehaviour | Mode-agnostic 3-pad (L/C/R) touch+keyboard input; fires events | `OnLeftHit`, `OnCenterHit`, `OnRightHit` | — | New Input System |
| `DungeonInputReader.cs` | MonoBehaviour | Records `DungeonInput` per tap; coyote buffer; calls evaluator for per-hit feedback | `TriggerInput()`, `ResetInputs()`, `allowInput`, `playerInputData` | `GameClock.GameTime` | `DrumPadTouch`, `DungeonEvaluator`, `DungeonVisualController`, `DungeonHealth` |
| `DungeonInput.cs` | plain class | Data container: virtual time + enemy type for one player tap | — | — | `DungeonEnemyType` |
| `BongoModeInputReader.cs` | MonoBehaviour | Records `BongoInput` per tap for Bongo mode | `TriggerInput()`, `ResetInputs()`, `allowInput` | `GameClock.GameTime` | `DrumPadTouch`, `BeatEvaluator` |
| `BongoInput.cs` | plain class | Data container: virtual time + isRightBongo | — | — | — |
| `InputMatch.cs` | plain class | Per-beat match result with `MatchQuality` enum | `Quality`, `TimingError`, `BeatIndex` | — | — |
| `HitGrader.cs` | MonoBehaviour | Old per-tap grader; replaced by BeatEvaluator/DungeonEvaluator | `GradeHit()`, `RegisterGrade()` | `Time` | — |
| `ArcadeInputHandler.cs` | MonoBehaviour | Routes DrumPadTouch L/R events to HitJudge for Arcade mode | — | — | `DrumPadTouch`, `HitJudge`, `HitZoneManager` |
| `GardenInputHandler.cs` | MonoBehaviour | Single-raycaster tap handler for Garden drum machine grid | — | — | Physics2D, `DrumGridCell` |
| `TapInvoker2D.cs` | MonoBehaviour | Fires UnityEvent on 2D collider tap; generic utility | `OnTapped` | — | Physics2D |
| `TapUnlessDragged.cs` | MonoBehaviour | Filters drag vs tap on UI elements | — | — | — |
| `TapOrDragFilter.cs` | MonoBehaviour | Companion drag/tap filter (older version) | — | — | — |

### Pattern / Combat Standard

| File | Class / Type | Responsibility | Key Public API | Timing Source | Dependencies |
|------|-------------|----------------|---------------|---------------|-------------|
| `DungeonBeatManager.cs` | MonoBehaviour | Orchestrates dungeon bar cycle: schedule patterns, open input window, evaluate, loop | `Initialize()`, `StartGameplay()`, `OnPause()`, `OnResume()`, `ResetToInitialState()`, `OnSongComplete` | `GameClock.GameTime`, `TimingCoordinator` | `Metronome`, `DungeonEvaluator`, `DungeonInputReader`, `DungeonVisualController`, `AudioManager` |
| `DungeonPatternGenerator.cs` | plain class | Generates random `DungeonBeat` lists respecting consecutive-type limit | `GeneratePattern()` | — | `DungeonBeat`, `DungeonEnemyType`, `GameConstants` |
| `DungeonEvaluator.cs` | MonoBehaviour | Bar evaluation + per-hit live grading for dungeon mode | `EvaluateBar()`, `EvaluateSingleInput()`, `ResetScore()`, `SaveHighScore()` | `GameClock` (pause check) | `Metronome`, `DungeonHealth`, `AudioManager` |
| `DungeonBeat.cs` | plain class | Pattern beat: duration + timeSlot + enemyType | — | — | `DungeonEnemyType` |
| `DungeonScheduledBeat.cs` | plain class | Scheduled beat: virtual time + enemyType; used as identity key for enemy pairing | — | — | `DungeonEnemyType` |
| `DungeonEnemyType.cs` | enum | Left / Center / Right (int 0/1/2) | — | — | — |
| `EvaluationResult.cs` | plain class + enum | Holds grade (AllPerfect/AllGood/Passable/Failed), hit counts, points | `Grade`, `Accuracy` | — | — |
| `PatternGenerator.cs` | plain class | Generates binary-side `Beat` lists for Bongo mode | `GeneratePattern()` | — | `Beat`, `GameConstants` |
| `BeatGenerator.cs` | MonoBehaviour | Full Bongo mode orchestrator (pattern → audio → visuals → evaluate → loop) | `Initialize()`, `StartGameplay()`, `OnPause()`, `OnResume()`, `ResetToInitialState()`, `OnSongComplete` | `GameClock`, `TimingCoordinator` | `Metronome`, `BeatEvaluator`, `BongoModeInputReader`, etc. |
| `BeatEvaluator.cs` | MonoBehaviour | Bar evaluation for Bongo mode with score/grade/adaptive logic | `EvaluateBar()`, `EvaluateSingleInput()`, `ResetScore()`, `SaveHighScore()` | `GameClock` | `BeatGenerator`, `CustardAnimationHandler`, `AudioManager` |
| `AdaptiveDifficultyManager.cs` | MonoBehaviour (serializable) | Adaptive Easy↔Hard state for Bongo starter difficulty | `ProcessRoundResult()`, `GetCurrentDurations()`, `IsInHardMode()`, `Reset()` | — | — |
| `Beat.cs` | plain class | Bongo-mode beat data (duration, timeSlot, isBongoSide) | — | — | — |
| `ScheduledBeat.cs` | plain class | Bongo-mode scheduled beat (virtualTime, isRightBongo) | — | — | — |

### Player / Health

| File | Class / Type | Responsibility | Key Public API | Timing Source | Dependencies |
|------|-------------|----------------|---------------|---------------|-------------|
| `DungeonHealth.cs` | MonoBehaviour | Player HP: miss damage + out-of-window penalty; no game-over logic | `ResetHealth()`, `TakeMissDamage()`, `TakeOutOfWindowPenalty()`, `CurrentHealth` | — | TMP_Text |
| `SongProgressionTracker.cs` | plain class (legacy) | Legacy DSP-based song completion check; superseded by TimingCoordinator | `Initialize()`, `IsSongComplete()`, `ShouldGenerateFinalPattern()` | DSP (legacy) | `GameClock`, `GameConstants` |

### Visual / Dungeon

| File | Class / Type | Responsibility | Key Public API | Timing Source | Dependencies |
|------|-------------|----------------|---------------|---------------|-------------|
| `DungeonVisualController.cs` | MonoBehaviour | Manages bar slider, spawn markers, response fill, and pooled enemy sprites | `Initialize()`, `ScheduleSpawnMarker()`, `FinalizeBarEnemyPositions()`, `SpawnInputMarker()`, `NotifyEnemyHit()`, `ResetVisuals()`, `CleanupAndDisable()` | `GameClock.GameTime`, `TimingCoordinator` | `Metronome`, `Slider`, `Image`, `DungeonEnemyVisual` pool |
| `DungeonEnemyVisual.cs` | MonoBehaviour | Pooled enemy sprite with spawn/hit/bar-end animations | `Init()`, `DespawnHit()`, `DespawnBarEnd()`, `StopAndReset()`, `InPool` | `Time.deltaTime` | `SpriteRenderer` |
| `DungeonModeController.cs` | ModeController subclass | Wires DungeonBeatManager + DungeonEvaluator + DungeonVisualController into GameManager lifecycle | `StartMode()`, `Cleanup()`, `ResetToInitialState()`, `OnPause()`, `OnResume()` | — | All Dungeon subsystems, `AudioManager` |

### Audio

| File | Class / Type | Responsibility | Key Public API | Timing Source | Dependencies |
|------|-------------|----------------|---------------|---------------|-------------|
| `AudioManager.cs` | MonoBehaviour (DontDestroyOnLoad) | Manages all speakers; schedules music; plays SFX oneshots; pause/resume | `PlayMusic()`, `PlayBongoLeft/Right()`, `PlayTurnSignal()`, `PlayIncorrect/Correct/AllPerfect/Passable/TotalFail()`, `ResetState()`, `PauseAllAudio()`, `ResumeAllAudio()` | DSP | New Input System audio |
| `DrumSoundType.cs` | enum | Kick/Snare/HiHat/Clap for Garden drum machine | — | — | — |
| `DrumMachine.cs` | MonoBehaviour | Garden drum machine sequencer; steps through GardenPlots | `TogglePlayback()`, `OnCellTapped()` | `WaitForSeconds` (coroutine) | `GardenPlot` |

### UI

| File | Class / Type | Responsibility | Key Public API | Timing Source | Dependencies |
|------|-------------|----------------|---------------|---------------|-------------|
| `UIMenuManager.cs` | MonoBehaviour | Page-based menu system with slide transitions | `ShowPage()`, `ShowPageImmediate()`, `TransitionToScene()`, `currentPage` | `Time.deltaTime` | `ScreenTransition` |
| `ScreenTransition.cs` | MonoBehaviour | Black panel wipe in/out | `StartCover()`, `StartReveal()`, `IsScreenCovered` | `Time.deltaTime` | — |
| `ScoreScreen.cs` | MonoBehaviour | Animates score count-up; displays grade letter/image | `DisplayScore()` | `Time.deltaTime` | TMP, `PlayerPrefs` |
| `BeatVisualScheduler.cs` | MonoBehaviour | Bongo-mode bar slider + spawn indicators | `ScheduleVisualBeat()`, `ResetVisuals()`, `CleanupAndDisable()` | `GameClock.GameTime`, `TimingCoordinator` | `Slider`, `Metronome`, `BeatGenerator`, `CustardAnimationHandler` |
| `PlayerInputVisualHandler.cs` | MonoBehaviour | Bongo-mode player-input slider + indicators | `SpawnInputIndicator()`, `SpawnPerfectInputStar()`, `CleanupAndDisable()` | `GameClock.GameTime`, `TimingCoordinator` | `Slider`, `Metronome`, `BeatGenerator` |
| `UIQuickSpawnEffect.cs` | MonoBehaviour | Scale+rotate pop animation on spawn | `rotateAngle`, `wrongHit`, `recoilScale` | `Time.unscaledDeltaTime` | `RectTransform` |
| `BounceOnBeat.cs` | MonoBehaviour | UI element that bounces on each Metronome tick | — | `Metronome.OnTickEvent` | `Metronome` |
| `UIRotateWobble.cs` | MonoBehaviour | Continuous wobble rotation | — | `Time.deltaTime` | — |
| `UIEntranceAnimator.cs` | MonoBehaviour | Slide-in entrance animation | — | `Time.deltaTime` / coroutine | — |
| `BongoAnimator.cs` | MonoBehaviour (singleton) | Scale/rotate animation for bongo character on hit | `PlayBongoAnimation()` | `Time.unscaledDeltaTime` | `RectTransform` |
| `CustardAnimationHandler.cs` | MonoBehaviour | Sprite-swap animations for Custard character (bongo, listening, success, fail) | `PlayLeftBongo()`, `PlayRightBongo()`, `HandleListening()`, `HandleSuccess()`, `HandleFailure()` | — | `SpriteRenderer` |
| `EyeBlinker.cs` | MonoBehaviour | Blink animation for UI character eyes | — | coroutine | — |
| `HitZoneVisual.cs` | MonoBehaviour | Arcade hit-zone colour flash (press/Perfect/Good/Miss) | `NotifyPress()`, `NotifyJudgement()` | `Time.deltaTime` | `SpriteRenderer` |
| `HitZoneManager.cs` | MonoBehaviour | Routes Arcade judgement events to HitZoneVisual instances | `NotifyPress()` | — | `RhythmLaneManager`, `HitZoneVisual` |
| `HighscoreSetter.cs` | MonoBehaviour | Sets highscore text in song carousel from PlayerPrefs | — | — | TMP, `PlayerPrefs` |
| `ScoreEventHandler.cs` | MonoBehaviour | Likely wires score events to UI display | — | — | — |
| `ScrollingTexture.cs` | MonoBehaviour | Scrolls a UI/material texture for background effect | — | `Time.deltaTime` | — |
| `TreeAnimationEffect.cs` | MonoBehaviour | Animates tree visuals in Garden scene | — | `Time.deltaTime` / coroutine | — |
| `CarouselController.cs` | MonoBehaviour | Handles song selection carousel swipe/scroll | — | `Time.deltaTime` | — |
| `SongIconSlotter.cs` | MonoBehaviour | Places song icons in carousel slots | — | — | — |

### Arcade (Guitar Hero system — separate mini-game)

| File | Class / Type | Responsibility |
|------|-------------|----------------|
| `ArcadeGameController.cs` | MonoBehaviour (singleton) | Top-level Arcade orchestrator: audio scheduling, note spawning start, song completion |
| `ArcadeModeController.cs` | ModeController | Bridges GameManager lifecycle to ArcadeGameController |
| `ArcadeInputHandler.cs` | MonoBehaviour | Routes DrumPadTouch L/R to HitJudge |
| `NoteSpawner.cs` | MonoBehaviour | Spawns NoteObjects from SongChart beat-space |
| `RhythmLaneManager.cs` | MonoBehaviour (singleton) | Per-lane note queues; auto-miss detection; `TryHitLane()` |
| `RhythmScoreTracker.cs` | MonoBehaviour | Score/combo/multiplier tracker for Arcade mode |
| `HitJudge.cs` | MonoBehaviour | Converts lane tap to Perfect/Good/Miss via RhythmLaneManager |
| `HitZoneManager.cs` | MonoBehaviour | Routes judgements to HitZoneVisual instances |
| `HitZoneVisual.cs` | MonoBehaviour | Per-lane colour flash feedback |
| `NoteObject.cs` | MonoBehaviour | Self-moving note prefab driven by GameClock virtual time |
| `SongChart.cs` | ScriptableObject | Song data: BPM, AudioClip, NoteData[], loop settings |
| `NoteData.cs` | struct | Beat position + lane for one note |
| `HitJudgement.cs` | enum | Perfect / Good / Miss |
| `Lane.cs` | enum | Left / Right |

### Garden (Drum Machine — isolated mode)

| File | Class / Type | Responsibility |
|------|-------------|----------------|
| `DrumMachine.cs` | MonoBehaviour (singleton) | Multi-plot drum sequencer |
| `GardenInputHandler.cs` | MonoBehaviour | Raycaster tap handler for grid cells |
| `GardenPlot.cs` | MonoBehaviour | One drum plot: grid of cells, step processing |
| `DrumGridCell.cs` | MonoBehaviour | Individual grid cell: state toggle, visual update |
| `DrumSoundType.cs` | enum | Kick / Snare / HiHat / Clap |

### Scene / Bootstrap

| File | Class / Type | Responsibility |
|------|-------------|----------------|
| `Bootstrap.cs` | MonoBehaviour | Sets target framerate; loads next scene |
| `SceneLoadManager.cs` | MonoBehaviour (DontDestroyOnLoad) | `ResetDrummi()` — loads scene 0 with clean AudioManager state |
| `SceneLoaderInterface.cs` | MonoBehaviour | Simple `LoadSceneByName()` wrapper |
| `SongItem.cs` | MonoBehaviour | Carousel song entry: title + BPM |
| `InputSystem_Actions.cs` | auto-generated class | Input action bindings (Unity Input System generated) |
| `AndroidDeviceFeatureDetection.cs` | MonoBehaviour | Detects Android device features |
| `SettingsMenu.cs` | MonoBehaviour | Settings UI |
| `CameraDrag.cs` | MonoBehaviour | Garden scene camera drag |
| `HatManager.cs` | MonoBehaviour | Hat cosmetic manager for Custard character |

### Editor

| File | Responsibility |
|------|---------------|
| `DungeonSceneBuilder.cs` | One-shot: creates Dungeon scene UI hierarchy, prefabs, DrumPadTouch GO |
| `DungeonSceneWiring.cs` | One-shot: wires all Inspector references across Dungeon scene components |
| `DungeonSceneFixes.cs` | Incremental hotfixes/patches to the Dungeon scene setup |
| `ClearSelectionOnSceneLoad.cs` | Editor utility: clears Inspector selection on scene open to prevent stale InstanceID errors |

---

## STEP 3 — CATEGORY MAP

| File | Category | Reuse Verdict |
|------|----------|--------------|
| `DrumPadTouch.cs` | INPUT | **REUSE_AS_IS** — already handles 3 pads; fires L/C/R events |
| `DungeonInputReader.cs` | INPUT | **REUSE_AS_IS** — coyote buffer, allowInput gate, per-hit evaluation |
| `DungeonInput.cs` | INPUT | **REUSE_AS_IS** — correct data container |
| `InputMatch.cs` | INPUT | **REUSE_AS_IS** — MatchQuality enum covers all cases |
| `BongoModeInputReader.cs` | INPUT | **REPLACE** — bongo-specific (2 pads, no enemyType) |
| `BongoInput.cs` | INPUT | **REPLACE** — bongo-specific |
| `HitGrader.cs` | INPUT | **REPLACE** — old grader; logic covered by DungeonEvaluator |
| `ArcadeInputHandler.cs` | INPUT | **REPLACE** — arcade-specific, wrong paradigm |
| `GardenInputHandler.cs` | INPUT | **REPLACE** — garden-specific raycaster |
| `TapInvoker2D.cs` | UTIL | **REUSE_AS_IS** |
| `TapUnlessDragged.cs` | UTIL | **REUSE_AS_IS** |
| `TapOrDragFilter.cs` | UTIL | **REUSE_AS_IS** |
| `GameClock.cs` | TIMING | **REUSE_AS_IS** — virtual-time architecture is solid |
| `TimingCoordinator.cs` | TIMING | **REUSE_AS_IS** — bar/beat cycle management works perfectly |
| `Metronome.cs` | TIMING | **REUSE_AS_IS** — BPM source + tick event |
| `PauseHandler.cs` | TIMING | **REUSE_AS_IS** — coordinates all systems correctly |
| `GameState.cs` | TIMING | **REUSE_AS_IS** — state machine enum |
| `GameConstants.cs` | TIMING | **EXTEND** — may need dungeon-specific constants (e.g. HP values, elite drain rates) |
| `SongProgressionTracker.cs` | TIMING | **REPLACE** — legacy DSP-based; TimingCoordinator supersedes it |
| `DungeonBeatManager.cs` | COMBAT_STD | **REUSE_AS_IS** — complete bar-cycle orchestrator for standard enemies |
| `DungeonPatternGenerator.cs` | COMBAT_STD | **REUSE_AS_IS** — 3-type randomiser |
| `DungeonEvaluator.cs` | COMBAT_STD | **REUSE_AS_IS** — bar eval + live per-hit quality |
| `DungeonBeat.cs` | COMBAT_STD | **REUSE_AS_IS** |
| `DungeonScheduledBeat.cs` | COMBAT_STD | **REUSE_AS_IS** — identity-key pairing |
| `DungeonEnemyType.cs` | COMBAT_STD | **REUSE_AS_IS** |
| `EvaluationResult.cs` | COMBAT_STD | **REUSE_AS_IS** — grade/score result |
| `AdaptiveDifficultyManager.cs` | COMBAT_STD | **EXTEND** — Easy↔Hard state machine; adapt for dungeon's difficulty tiers |
| `PatternGenerator.cs` | COMBAT_STD | **REPLACE** — bongo-specific (boolean side), not needed for dungeon |
| `BeatGenerator.cs` | COMBAT_STD | **REPLACE** — bongo-mode orchestrator; DungeonBeatManager replaces it |
| `BeatEvaluator.cs` | COMBAT_STD | **REPLACE** — bongo-specific; DungeonEvaluator replaces it |
| `Beat.cs` | COMBAT_STD | **REPLACE** — bongo-specific |
| `ScheduledBeat.cs` | COMBAT_STD | **REPLACE** — bongo-specific |
| `DungeonHealth.cs` | PLAYER | **EXTEND** — has HP + two damage sources; needs game-over trigger and drain mechanics |
| `DungeonVisualController.cs` | COMBAT_STD | **REUSE_AS_IS** — slider + enemy pool already working |
| `DungeonEnemyVisual.cs` | COMBAT_STD | **REUSE_AS_IS** — pooled enemy sprite with 3 animations |
| `DungeonModeController.cs` | COMBAT_STD | **REUSE_AS_IS** — ModeController wiring complete |
| `BeatVisualScheduler.cs` | UI | **REPLACE** — bongo-specific bar slider |
| `PlayerInputVisualHandler.cs` | UI | **REPLACE** — bongo-specific input slider |
| `AudioManager.cs` | AUDIO | **EXTEND** — needs SFX slots for elite/boss combat feedback |
| `DrumSoundType.cs` | AUDIO | **REUSE_AS_IS** |
| `UIMenuManager.cs` | UI | **REUSE_AS_IS** — page-based menu works for dungeon menus |
| `ScreenTransition.cs` | UI | **REUSE_AS_IS** |
| `ScoreScreen.cs` | UI | **EXTEND** — reads `GlitchyHS` key; needs dungeon high score key + run summary data |
| `UIQuickSpawnEffect.cs` | UI | **REUSE_AS_IS** — already used for dungeon input indicators |
| `BounceOnBeat.cs` | UI | **REUSE_AS_IS** |
| `UIRotateWobble.cs` | UI | **REUSE_AS_IS** |
| `UIEntranceAnimator.cs` | UI | **REUSE_AS_IS** |
| `HitZoneVisual.cs` | UI | **REPLACE** — arcade-specific |
| `HitZoneManager.cs` | UI | **REPLACE** — arcade-specific |
| `BongoAnimator.cs` | UI | **REPLACE** — bongo character |
| `CustardAnimationHandler.cs` | UI | **REPLACE** — bongo mode character |
| `EyeBlinker.cs` | UTIL | **REUSE_AS_IS** |
| `ScrollingTexture.cs` | UTIL | **REUSE_AS_IS** |
| `TreeAnimationEffect.cs` | UTIL | **REUSE_AS_IS** |
| `CarouselController.cs` | UI | **REUSE_AS_IS** |
| `SongIconSlotter.cs` | UTIL | **REUSE_AS_IS** |
| `HighscoreSetter.cs` | PROGRESSION | **EXTEND** — needs dungeon high score key |
| `ScoreEventHandler.cs` | UI | **REUSE_AS_IS** |
| `GameManager.cs` | UTIL | **REUSE_AS_IS** — mode registry + lifecycle is solid |
| `ModeController.cs` | UTIL | **REUSE_AS_IS** — abstract base |
| `BongoModeController.cs` | UTIL | **REPLACE** — bongo-specific ModeController |
| `ArcadeModeController.cs` | UTIL | **REPLACE** — arcade-specific ModeController |
| `Bootstrap.cs` | UTIL | **REUSE_AS_IS** |
| `SceneLoadManager.cs` | UTIL | **REUSE_AS_IS** |
| `SceneLoaderInterface.cs` | UTIL | **REUSE_AS_IS** |
| `SongItem.cs` | UTIL | **REUSE_AS_IS** |
| `InputSystem_Actions.cs` | UTIL | **REUSE_AS_IS** — auto-generated |
| `AndroidDeviceFeatureDetection.cs` | UTIL | **REUSE_AS_IS** |
| `SettingsMenu.cs` | UTIL | **REUSE_AS_IS** |
| `CameraDrag.cs` | UTIL | **REUSE_AS_IS** |
| `HatManager.cs` | UTIL | **REUSE_AS_IS** |
| `ArcadeGameController.cs` | UNKNOWN | **REPLACE** — arcade-specific |
| `NoteSpawner.cs` | UNKNOWN | **REPLACE** — arcade-specific |
| `RhythmLaneManager.cs` | UNKNOWN | **REPLACE** — arcade-specific |
| `RhythmScoreTracker.cs` | UNKNOWN | **REPLACE** — arcade-specific |
| `HitJudge.cs` | UNKNOWN | **REPLACE** — arcade-specific |
| `NoteObject.cs` | UNKNOWN | **REPLACE** — arcade-specific |
| `SongChart.cs` | UNKNOWN | **REPLACE** — arcade-specific |
| `NoteData.cs` | UNKNOWN | **REPLACE** — arcade-specific |
| `HitJudgement.cs` | UNKNOWN | **REPLACE** — arcade-specific |
| `Lane.cs` | UNKNOWN | **REPLACE** — arcade-specific |
| `DrumMachine.cs` | UNKNOWN | **REPLACE** — garden-specific |
| `GardenInputHandler.cs` | UNKNOWN | **REPLACE** — garden-specific |
| `GardenPlot.cs` | UNKNOWN | **REPLACE** — garden-specific |
| `DrumGridCell.cs` | UNKNOWN | **REPLACE** — garden-specific |
| `DungeonSceneBuilder.cs` | UTIL | **REUSE_AS_IS** — editor scaffold |
| `DungeonSceneWiring.cs` | UTIL | **REUSE_AS_IS** — editor scaffold |
| `DungeonSceneFixes.cs` | UTIL | **REUSE_AS_IS** — editor hotfixes |
| `ClearSelectionOnSceneLoad.cs` | UTIL | **REUSE_AS_IS** — editor utility |

---

## STEP 4 — GAP REPORT

| Drummi Dungeons System | Status |
|------------------------|--------|
| INPUT | ✅ Complete — DrumPadTouch + DungeonInputReader already working |
| TIMING | ✅ Complete — GameClock + TimingCoordinator + Metronome + PauseHandler all solid |
| COMBAT_STD | ✅ Complete — DungeonBeatManager + DungeonPatternGenerator + DungeonEvaluator + DungeonVisualController all working |
| COMBAT_ELITE | ❌ MISSING — no HP-drain enemy, no continuous threat mechanic |
| COMBAT_BOSS | ❌ MISSING — no boss encounter, no modifier system |
| PLAYER | ⚠️ Partial — DungeonHealth has HP but no game-over, no run persistence, no cross-room state |
| ROOM | ❌ MISSING — no room state machine, no room data definition |
| DUNGEON | ❌ MISSING — no floor generator, no room sequencer |
| PROGRESSION | ❌ MISSING — no unlock system, no run manager, no meta-state |
| AUDIO | ⚠️ Partial — AudioManager exists; missing elite/boss SFX slots, adaptive music |
| UI | ⚠️ Partial — menus and transitions work; no dungeon HUD (room name, floor depth, run timer), no run-summary screen |
| UTIL | ✅ Complete — GameManager, ModeController, SceneLoadManager all reusable |

### Missing Systems (must be built from scratch)
1. **COMBAT_ELITE** — Elite enemy: HP bar, continuous damage drain while pattern plays, custom visual
2. **COMBAT_BOSS** — Boss encounter: phase-based patterns, mechanic modifiers, win condition
3. **ROOM** — `RoomDefinition` ScriptableObject, `RoomController` state machine (enter → play → evaluate → exit)
4. **DUNGEON** — `DungeonFloor` layout, `DungeonRunner` that sequences rooms, win/loss routing
5. **PROGRESSION** — `RunManager` (current run state), unlock table, meta-persistence via PlayerPrefs or ScriptableObjects

---

## STEP 5 — ADJUSTED 6-PHASE BUILD PLAN

> **Legend:**
> - 🟢 REUSE_AS_IS — just wire it in
> - 🟡 EXTEND — existing file, add described features
> - 🔴 REPLACE/NEW — build from scratch or rewrite

---

### Phase 1 — Core Loop Validation _(~1 session)_

**Goal:** Prove the standard combat loop runs end-to-end in a single isolated room.

**What already works (do nothing):**
- 🟢 `GameClock`, `TimingCoordinator`, `Metronome`, `PauseHandler` — fully operational
- 🟢 `DrumPadTouch` — 3-pad input fires correctly
- 🟢 `DungeonInputReader` — coyote buffer, allowInput gate, per-hit quality feedback
- 🟢 `DungeonBeatManager` — bar-cycle orchestrator
- 🟢 `DungeonPatternGenerator`, `DungeonEvaluator`, `DungeonVisualController`, `DungeonEnemyVisual`
- 🟢 `DungeonModeController` — GameManager lifecycle wiring
- 🟢 `AudioManager` — music scheduling + SFX
- 🟢 `GameManager` — `SetMode("Dungeon") → StartGame()` flow

**Validation tasks:**
- Run the Dungeon scene; confirm a full song completes, score screen appears, second round re-enables visuals (bug fixed in preceding commit)
- Verify `PauseHandler.TogglePause()` correctly freezes and resumes slider + enemies

---

### Phase 2 — Player Health & Game-Over _(~1 session)_

**Goal:** HP reaches zero → run ends, not just cosmetic damage.

**Extend:**

🟡 `DungeonHealth.cs`
- Add `OnHealthDepleted` event (Action)
- Fire it in `TakeMissDamage()` and `TakeOutOfWindowPenalty()` when `CurrentHealth <= 0`
- Add optional `regenPerBar` parameter (for future elite drain recovery)

🔴 `DungeonGameOverHandler.cs` _(new)_
- Subscribes to `DungeonHealth.OnHealthDepleted`
- Calls `GameManager.instance.ActiveMode.Cleanup()` then shows game-over UI page
- Hands off to future DUNGEON system for run failure routing

🟡 `UIMenuManager` — add a `"GameOver"` page entry in Inspector (no code change; just scene wiring)

🟡 `AudioManager.cs` — add one extra `otherSounds` slot for game-over sting; call from `DungeonGameOverHandler`

---

### Phase 3 — Room State Machine _(~2 sessions)_

**Goal:** A single fight is wrapped in a `RoomController`; rooms have entrance/exit transitions.

**New files needed:**

🔴 `RoomDefinition.cs` _(ScriptableObject)_
```csharp
// Fields: roomName, enemyTier (Standard/Elite/Boss),
//         patternDifficulty (float[] durations), bgmTrackIndex, artOverride
```

🔴 `RoomController.cs` _(MonoBehaviour)_
```csharp
// States: Entering, Playing, Evaluating, Exiting, Complete
// Drives: DungeonModeController.StartMode() on enter
//         Subscribes to DungeonModeController.OnModeComplete
//         Fires OnRoomComplete(RoomResult) for DungeonRunner to consume
// Uses: ScreenTransition for enter/exit wipes
```

🔴 `RoomResult.cs` _(plain class)_
```csharp
// Fields: survived (bool), score, health remaining, time taken
```

**Wire into existing systems:**
- 🟡 `DungeonModeController` — expose a `SetDifficulty(float[] durations)` override so `RoomController` can push per-room difficulty (currently hardcoded in `DungeonBeatManager.Initialize()`)
- 🟢 `ScreenTransition` — already handles cover/reveal; just call from `RoomController`

---

### Phase 4 — Dungeon Floor & Sequencing _(~2 sessions)_

**Goal:** Multiple rooms sequence automatically; floor has a start, middle, and boss room.

**New files needed:**

🔴 `DungeonFloorDefinition.cs` _(ScriptableObject)_
```csharp
// Fields: List<RoomDefinition> rooms, floorName, floorIndex
```

🔴 `DungeonRunner.cs` _(MonoBehaviour)_
```csharp
// Owns: DungeonFloorDefinition currentFloor, int currentRoomIndex
// Drives: RoomController.LoadRoom(RoomDefinition)
//         on OnRoomComplete → advance or end floor
// Handles: win (all rooms cleared) and loss (health depleted) routing
// Fires: OnFloorComplete, OnRunFailed
```

🟡 `GameManager.cs`
- No code change needed; `DungeonRunner` can call `GameManager.instance.StartGame()` after setting up the next room — or bypass GameManager and call `DungeonModeController.StartMode()` directly for a tighter loop
- Prefer a thin wrapper that doesn't duplicate the shared systems reset

**Consider:**
- Whether each room starts a fresh `GameClock.Reset()` (recommended: yes, per-room virtual time)
- Whether `TimingCoordinator` re-initializes per room (yes — each room is one song)

---

### Phase 5 — Elite & Boss Combat _(~2–3 sessions)_

**Goal:** Elite enemy drains HP continuously; boss has phase-based mechanics.

#### COMBAT_ELITE

🔴 `EliteEnemyController.cs` _(MonoBehaviour)_
```csharp
// Has its own HP bar (not player HP)
// Spawns with DungeonEnemyType pattern like standard enemy
// While alive: drains DungeonHealth by drainRatePerSecond each frame
// On hit (Perfect/Good): reduces enemy HP
// On enemy HP = 0: despawn, stop drain
// Wires into DungeonInputReader.OnHit event (extend DungeonInputReader)
```

🟡 `DungeonInputReader.cs`
- Add `OnHit` event carrying `(DungeonEnemyType, InputMatch.MatchQuality)` so EliteEnemyController can react without a direct reference

🟡 `DungeonHealth.cs`
- Add `TakeDrainDamage(float amount)` that accepts fractional values per frame (for continuous drain)
- Guard: don't fire `OnHealthDepleted` twice

🟡 `DungeonVisualController.cs`
- Add elite enemy visual variant (different sprite or tint; enemy stays on screen until its own HP is gone rather than despawning on first hit)

#### COMBAT_BOSS

🔴 `BossController.cs` _(MonoBehaviour)_
```csharp
// Phases: each phase is a List<RoomDefinition> (pattern set)
// Transitions phase when boss HP threshold crossed
// Phase modifiers: speedMultiplier, extraPads (optional), patternOverride
// On all phases cleared: fire OnBossDefeated
```

🔴 `BossPhaseDefinition.cs` _(ScriptableObject)_
```csharp
// Fields: patternDurations, bossHpThreshold, bgmIntensity, visualModifier
```

---

### Phase 6 — Progression, UI Polish & Audio _(~2 sessions)_

#### PROGRESSION

🔴 `RunManager.cs` _(MonoBehaviour, DontDestroyOnLoad optional)_
```csharp
// Tracks: currentFloorIndex, roomsCleared, totalScore, highestFloorReached
// Persists: PlayerPrefs or a SaveData ScriptableObject
// Exposes: IsNewRecord, GetBestRun()
```

🟡 `HighscoreSetter.cs`
- Add a `"DungeonHS"` case alongside existing Bongo/Latin/Jazz entries

#### UI POLISH

🟡 `ScoreScreen.cs`
- Add `DisplayDungeonResult(RunManager runData)` method showing floor reached, rooms cleared, run score
- Change `highScoreText` key lookup to accept a configurable key (currently hardcoded `"GlitchyHS"`)

🔴 `DungeonHUD.cs` _(new MonoBehaviour)_
```csharp
// Shows: current room name, floor depth indicator, HP bar (driven by DungeonHealth),
//        current score (driven by DungeonEvaluator.Score)
// Subscribes to: DungeonHealth events for HP flash, Metronome.OnTickEvent for pulse
```

🔴 `RunSummaryScreen.cs` _(new MonoBehaviour — or extend ScoreScreen)_
```csharp
// Shown after DungeonRunner.OnFloorComplete or OnRunFailed
// Displays: floor reached, total score, rooms cleared, new record badge
```

#### AUDIO POLISH

🟡 `AudioManager.cs`
- Add `PlayEliteSpawn()`, `PlayBossPhaseTransition()`, `PlayRunComplete()` methods
- Extend `otherSounds` array (currently 6 slots) to accommodate new clips — inspector-assigned

🔴 _(optional)_ `AdaptiveMusicController.cs`
- Layer-based music intensity: layer 0 = calm (menu), layer 1 = standard fight, layer 2 = elite fight, layer 3 = boss
- Fades between DSP-scheduled AudioSources on room type change

---

## Summary

| Phase | Goal | Sessions | Key New Files |
|-------|------|----------|---------------|
| 1 | Validate existing combat loop | 1 | None — all REUSE_AS_IS |
| 2 | HP game-over | 1 | `DungeonGameOverHandler` |
| 3 | Room state machine | 2 | `RoomDefinition`, `RoomController`, `RoomResult` |
| 4 | Floor sequencing | 2 | `DungeonFloorDefinition`, `DungeonRunner` |
| 5 | Elite & Boss combat | 2–3 | `EliteEnemyController`, `BossController`, `BossPhaseDefinition` |
| 6 | Progression + UI + Audio | 2 | `RunManager`, `DungeonHUD`, `RunSummaryScreen` |

**Total estimated new files: ~10**
**Total files requiring extension: ~7**
**Files the Dungeon systems reuse as-is: ~25**
