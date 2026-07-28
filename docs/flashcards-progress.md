# Flash Cards — Progress / Resume Anchor

Sight-reading mechanic: a 2x4 card (8 beat slots) reveals a rhythm silently
over one bar (each square flips on its beat), the player performs it over the
next bar, graded by the existing evaluator pipeline. Two eventual homes:
a dungeon room type (built) and a standalone gamemode (not started).

Full phase plan came out of the Phase 0 recon (2026-07-28). Sessions so far
were one long day: Phases 0 → 2.

---

## Done & verified

### Phase 1 — Core cycle, no UI (commit `c1e25b3`)
- **FlashCardController** (`KrumbleHut.Drummi.Dungeons`) — sibling of
  CallAndResponseController, reuses the public `PhraseTiming` struct with
  fixed N=8. Cycle: RevealStart → per-slot reveal polling → PerformStart
  (+8 beats) → Evaluate (+16 beats) → next card at `NextPhraseStart`
  (zero-gap, verified no drift over 4+ cards). All GameClock virtual time.
- **FlashCardPatternGenerator** — 8 slots, quarter notes + rests only,
  slot 0 always a note, note count random in [minNotes, maxNotes]
  (controller Inspector fields, default 3–6). Outputs standard
  `DungeonBeat` list (duration=1f) + `bool[8]` noteMask.
- **Gating** — exact C&R contract: reveal bar `allowInput=false` +
  `EncounterActive=true` + `CoyoteWindowStartOverride=ResponseStartTime`;
  perform bar opens via `OpenPerformWindow` (beats/offset set BEFORE gates);
  `SingleDrumEchoConstant=Center` → timing-only grading (any pad counts).
- **Grading** — `DungeonEvaluator.EvaluateBar(inputs, cardBeats,
  timeOffset: 8*beatInterval)`. Rest-taps penalize via the nearest-beat
  geometry (≥1 beat from any note > Good window) — verified with log
  evidence. Reveal-bar taps are silently ignored, no penalty, no evaluator
  pollution (verified).
- **Room integration (minimal)** — `RoomType.FlashCard` (enum index 4),
  RoomController branch modeled on C&R: `StartRoomAudioVisual()` (suppresses
  DungeonBeatManager, `SuppressBarSlider=true`), starts encounter at
  `EncounterStartVirtualTime`, `OnEncounterComplete` → room exit, death →
  `StopEncounter()`. Debug assets: `Rooms/Debug_FlashCard.asset` +
  `Floors/FloorDebug_FlashCard.asset` (track 1, 105 BPM).

### Phase 1.1 — Window-open boundary race fix (commit `f01fa8f`)
- Found in Phase 1 verification: a tap in the sub-frame gap
  [scheduled window open, controller-Update-sets-allowInput] was silently
  eaten (C&R + FlashCard override path). Fix:
  `DungeonInputReader.windowOpenLateToleranceSeconds` (serialized, 0.05s
  virtual time) — such taps are coyote-buffered and replayed exactly once at
  window open. Single-delivery traced and log-verified (Inputs == taps).

### Phase 1.2 — Combat boundary penalty fix (commit `58be431`)
- Same race in the standard-combat branch fell through to an UNDESERVED
  out-of-window penalty. Fixed with the same tolerance field (reused, not
  duplicated). Safe by construction: window closes at +3.5 beats (≫ 0.05s)
  and CurrentBar advances post-eval, so tolerance can't mask genuine lates.
- Verified: combat boundary taps AllPerfect; stray gap taps penalize exactly
  once; FlashCard + C&R full regressions pass.

### Phase 2 — Card UI (commit `6ee4ce1`)
- **FlashCardView** on the FlashCardController object; renders
  `FlashCardPanel` under `DungeonCanvas/GameplayUI` (2x4 manual layout,
  GardenPlot pattern, 660x380 @ top-center y=-200, panel starts inactive,
  ALL Images raycastTarget=false so pad input passes through).
- **Timing contract**: flips fire from the controller's virtual-time events
  (`OnCardRevealStart` / `OnSquareReveal` / `OnPerformStart` /
  `OnCardEvaluated` / `OnEncounterComplete` / `OnEncounterStopped`).
  DOTween is cosmetic only (scaleX flip, SetLink + kill-before-reuse).
  Perform-bar beat cursor = view-local poll of GameClock vs PhraseTiming.
- **Glyphs**: placeholder sprites in `Assets/Images/Notation/`
  (QuarterNote, QuarterRest — white ink tinted by Image.color). Final art
  is a drop-in swap on two serialized Sprite fields.
- `OnEncounterStopped` event added to FlashCardController (external stop
  only) for death teardown. Debug colored-quad path removed.
- Verified in editor: flips ≤1 frame off-beat (0.07–11ms deltas), clean
  4-card resets, death mid-cycle teardown (panel inactive, zero DOTween
  warnings), BarSlider fully dormant (IsActive=false, value 0).

---

## Immediate next step — PHASE 3 (dungeon room type, full integration)

Recon owed before spec (assumptions to verify against current files):
- RoomDefinition: what card config should be room-authored vs controller
  Inspector defaults. NOTE: `patternDurations` on a FlashCard room is INERT
  (it feeds DungeonBeatManager, which is suppressed; cards come from
  FlashCardPatternGenerator + controller minNotes/maxNotes). Phase 3 likely
  adds explicit fields (card count, min/max notes — later: difficulty tier).
- How a FlashCard room should sit inside a real floor sequence (music
  continuity across the seam into/out of a FlashCard room is untested —
  all testing so far used a single-room debug floor with fresh session).
- Room end condition: currently fixed `cardsPerEncounter`. Design says
  lessons are repeat-until-pass (no score) — decide whether that's a Phase 3
  room behavior or standalone-only.
- Per-hit grade feedback on squares was explicitly deferred (polish pass).

## Still TODO (later phases)
- **Karl's manual editor playtest** (readability at 105 BPM, reveal→perform
  clarity without instruction) — gate before Phase 3 polish decisions.
- **Mobile device pass** — touch latency vs Perfect window (±45.7ms @105),
  flip readability at device size. After editor review.
- **Phase 4 — standalone mode**: FlashCardsModeController
  (namespace KrumbleHut.Drummi.FlashCards), own scene, GameManager
  registration (inspector list), UIMenuManager page, dedicated tracks
  (AudioManager.musicTracks is a raw AudioClip[] — append, no code),
  lesson repeat-until-pass + endless streak scoring.
- **Eighths tier (blocked by design decision)**: pipeline supports half-beat
  positions end-to-end (floats everywhere, verified in recon), BUT rest
  enforcement is geometric — a rest half a beat from a note falls inside its
  Good window and would grade as a sloppy hit instead of penalizing.
  Needs semantic rest handling first. Also needs: generator mode, reveal
  payload widened (slot content enum, not bool), eighth-pair glyph.
- **Pad-encoding tier** (staff position = L/C/R) — after eighths.

## Key decisions locked
- Reveal is SILENT — no call audio ever scheduled (no PrepareCallPhrase
  analogue; no DSP headroom needed).
- Reveal-bar taps: silently ignored (C&R behavior), NOT penalized.
- Tier 1 grading is timing-only (SingleDrumEchoConstant collapse);
  pad identity is irrelevant until the pad-encoding tier.
- Rest enforcement at Tier 1 rides on the existing nearest-beat geometry —
  no rest concept in the evaluator (revisit only for eighths).
- Double-tap on one note: 2nd tap grades as an immediate hit (no penalty)
  but bar grade caps at Passable via inputs>beats. Accepted for Tier 1;
  revisit for endless/streak scoring.
- One shared boundary tolerance field (`windowOpenLateToleranceSeconds`)
  for all three window-gated paths — mobile tuning happens in one place.
- Beat accuracy lives in controllers (GameClock polling); views/tweens are
  cosmetic. Never beat-sync a tween.

## Test infrastructure (editor-only, keep until Phase 4 cleanup)
- Harnesses (Assets/Scripts, committed): `FlashCardTestRunner` (wired in
  Dungeon scene, currently DISABLED — enable for automated runs),
  `CallResponseTestRunner` + `CombatTestRunner` (runtime AddComponent only).
- Drivers (CoplayScripts/, NOT committed): StartFlashCardDebugRun /
  StartCRDebugRun / StartCombatDebugRun (play-mode: swap DungeonRunner
  floors on the runtime instance — reverts on stop — and call
  GameManager.StartDungeonRun), KillPlayerNow (death test),
  CaptureGameViewSeries (screenshots to Temp/).
- Debug assets (committed): Debug_FlashCard room, FloorDebug_FlashCard floor.
- Known debug-entry artifact: StartDungeonRun() from script skips the menu
  button's page-hide → menu remnants visible during debug runs. Cosmetic.
- Editor gotcha: writing a new CoplayScripts file during play mode triggers
  a recompile that EXITS play mode. Write scripts first, then play.
- Scene saves: always `EditorSceneManager.SaveOpenScenes()` via
  execute_script — never the mcp save_scene tool.

## Working-tree caution
Karl has in-progress uncommitted work that must NOT be swept into Flash Cards
commits: C&R `CallResponseInputMode` removal refactor
(CallAndResponseController.cs / RoomController.cs / RoomDefinition.cs),
temp rig-overlap diagnostics in DungeonInputLayoutController.cs, Floor0.asset
edits, and untracked tutorial/asset files. Phase 1 was committed via
selective hunk staging to keep these out — do the same if those files are
still dirty.
