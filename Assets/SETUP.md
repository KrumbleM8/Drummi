# Guitar Hero System — Setup Guide

## Files

| File | Type | Purpose |
|---|---|---|
| `Lane.cs` | Enum | Left / Right lane identifiers |
| `HitJudgement.cs` | Enum | Perfect / Good / Miss |
| `NoteData.cs` | Struct | Beat + Lane definition for one note |
| `SongChart.cs` | ScriptableObject | Full chart: BPM, time sig, notes, loop config |
| `NoteObject.cs` | MonoBehaviour | Prefab component — self-positions via VirtualTime |
| `NoteSpawner.cs` | MonoBehaviour | Streams notes from chart into scene |
| `RhythmLaneManager.cs` | MonoBehaviour | Per-lane queues, auto-miss, judgement dispatch |
| `HitJudge.cs` | MonoBehaviour | Converts tap → HitJudgement via TimingCoordinator |
| `RhythmInputHandler.cs` | MonoBehaviour | Keyboard + touch input, fires into HitJudge |

---

## Scene Setup

### 1. Manager GameObject
Create an empty GameObject named `RhythmManager`.
Add these components:
- `RhythmLaneManager`
- `NoteSpawner`
- `RhythmInputHandler`
- `HitJudge`

### 2. Note Prefab
Create a GameObject (e.g. a sprite or shape) and add `NoteObject`.
Assign it to `NoteSpawner > notePrefab`.

### 3. Configure NoteSpawner

| Field | Recommended starting value |
|---|---|
| `hitZoneY` | `-4` (adjust to match your camera bottom) |
| `laneXPositions` | `[-2, 2]` |
| `lookaheadBeats` | `4` |
| `scrollDistance` | Your camera's vertical world-unit height (e.g. `10`) |

### 4. Wire TimingCoordinator
In `HitJudge`, `useTimingCoordinator = true` by default.
Confirm `TimingCoordinator.Instance` exposes:
```csharp
public float PerfectWindowSeconds { get; }
public float GoodWindowSeconds    { get; }
```
Rename the references in `HitJudge.ResolveWindow()` if your property names differ.
`RhythmLaneManager` also reads `TimingCoordinator.Instance.GoodWindowSeconds` for auto-miss.

### 5. Wire Judgement Events
Subscribe to `RhythmLaneManager.Instance.OnJudgement` from your score/UI systems:
```csharp
RhythmLaneManager.Instance.OnJudgement += (lane, judgement) =>
{
    scoreManager.AddScore(lane, judgement);
    feedbackUI.Show(judgement);
};
```

---

## Creating a Song Chart

1. Right-click in Project → **Create > Drummi > Song Chart**
2. Set `bpm`, `beatsPerBar`, `loopChart`, `loopLengthBeats`
3. Add entries to the `notes` array:
   - `beat`: float beat number (0-indexed, fractions OK)
   - `lane`: Left or Right
4. Right-click the asset → **Sort Notes By Beat** (required)

### Example — 4/4 pattern, 2-bar loop, 8 notes
```
beat 0.0  Left
beat 0.5  Right
beat 1.0  Left
beat 1.5  Right
beat 2.0  Left
beat 2.5  Right
beat 3.0  Left
beat 3.5  Right
loopLengthBeats = 4
```

### Time signatures
The system is beat-based — `beatsPerBar` is cosmetic only.
- **3/4**: `beatsPerBar = 3`, place notes at beats 0, 1, 2
- **6/8**: `beatsPerBar = 6`, use 0.33 beat resolution (1 beat = 1 quaver)
- **5/4**: `beatsPerBar = 5`, free placement

---

## Scroll Speed Formula

```
lookaheadSeconds = lookaheadBeats × (60 / bpm)
scrollSpeed      = scrollDistance / lookaheadSeconds
spawnY           = hitZoneY + scrollDistance
```

Increasing `lookaheadBeats` gives a longer runway (more notes on screen).
`scrollDistance` should match your camera's world-unit height.

---

## Wiring to GameClock

`NoteObject` and `RhythmInputHandler` both call `GameClock.Instance.IsPlaying` and
`GameClock.Instance.VirtualTime`. No changes needed — these match your existing architecture.

`NoteSpawner` uses `GameClock.Instance.VirtualTime` as the sole time source.
All note hit times are stored as **absolute virtual time seconds**, so pause/resume is
drift-free by design (same principle as Drummi's TimingCoordinator refactor).

---

## Adding Hold Notes (Future)

`NoteData` can be extended with `public float holdDuration;` (in beats).
`NoteObject` can be extended with a stretched tail visual and a release detection phase.
`HitJudge.Judge()` can be split into `JudgeTapStart()` / `JudgeTapEnd()`.
No architectural changes required.
