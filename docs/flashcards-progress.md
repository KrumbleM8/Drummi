# Flash Cards — Progress / Resume Anchor

Sight-reading mechanic: a 2x4 card (8 beat slots) reveals a rhythm silently
over one bar (each square flips on its beat), the player performs it over the
next bar, graded by the existing evaluator pipeline. Two eventual homes:
a dungeon room type (built, Phases 1-3 + 5) and a standalone endless mode
(built, Phase 4 — FlashCards.unity).

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

### Phase 3 — First-class dungeon room type (2026-07-30)
- **Room-authored config** — cards derive from `RoomDefinition.RoomLengthBars`
  (cards = bars/2; each card is a 2-bar cycle; odd bars round down min 1 with
  a warning; 0 = controller Inspector default). New
  `RoomDefinition.flashCardNoteRange` (Vector2Int, (0,0) sentinel = controller
  defaults) + `HasFlashCardNoteRange`. FlashCardController gained
  `StartEncounter(startVirtualTime, cardCount, noteRange)` overload resolving
  active config exactly like C&R's `_activePhraseLengths` pattern.
  `patternDurations` remains INERT for FlashCard rooms (tooltip documents it).
- **Routing polish (2 gaps fixed vs C&R quality bar)**:
  1. `SetState(Entering)` now suppresses bar visuals for FlashCard rooms too
     (was C&R-only — bar-slider flash risk during reveal).
  2. `HandleCarEncounterComplete`/`HandleFlashCardEncounterComplete` now call
     `UnsubscribeEvents()` (was self-only unsubscribe — the health-depleted
     subscription accumulated one dup per completed encounter room; benign
     due to the state guard, but wrong).
- **Per-card grade feedback** — FlashCardView border flashes the grade color
  at each evaluation boundary (event-driven moment; DOTween hold+fade back to
  idle is cosmetic and bleeds into the next reveal bar since cards chain
  zero-gap). Serialized colors per EvaluationGrade tier (no shared dungeon
  grade-color convention exists — checked). Normal completion holds the card
  ~0.8s (real-time, cosmetic) so the FINAL card's grade is readable during
  room exit; death (`OnEncounterStopped`) hides immediately.
- **Verified in editor** (temp floor [Combat → FlashA(4 bars, notes 3–5) →
  C&R{2,4} → FlashB(6 bars, defaults, BGM/BPM override track 2 @110)],
  auto-tap harnesses, two full runs + death run):
  - Seams: mid-floor entry (grid-offset +0.000ms on session grid), exit to
    C&R and back, BGM+BPM override at seam (FlashB @110, session re-anchored),
    -1 sentinels honored. Reveal/eval deltas ≤ ~11ms throughout.
  - Card counts from RoomLengthBars (4→2, 6→3); note-range authoring and
    (0,0)-sentinel fallback both observed in logs.
  - Score aggregation continuous across all room types (run total 54).
  - Pause/resume DURING reveal bar AND perform bar: virtual time froze
    exactly (537.820590 → 537.820590), flips resumed beat-correct, the
    paused card still graded AllPerfect.
  - Death mid-FlashCard (FlashB card 0, perform bar): StopEncounter → run
    failed → results, FlashCardPanel confirmed inactive, zero DOTween
    warnings/errors.
  - Regressions in-floor: combat boundary+stray-tap bars (AllPerfect +
    exactly-one-penalty Passable), C&R boundary echo + call-window ignore
    (AllPerfect), FlashCard scenario grades all as expected.
- **Test infra added**: `DungeonFloorTestDriver`
  (Assets/Scripts, committed) orchestrates the three tap runners across a
  mixed floor (C&R runner needs create-inactive→wire→activate — OnEnable
  subscribes before field wiring otherwise). `FlashCardTestRunner` now also
  clears queued taps on `OnEncounterStopped` (post-death injection artifact,
  taps were rejected by the disarmed reader but noisy).
  CoplayScripts (NOT committed): CreatePhase3TestAssets /
  StartPhase3TestRun / Phase3AutoPick (auto-clicks effect-pick overlay) /
  Phase3PauseTest / DeletePhase3TestAssets. Temp floor deleted after
  verification.

---

### Phase 5 — Eighths tier (commit `de158f0`, 2026-07-30)
- SquareTemplate vocabulary per square: Quarter / EighthPair / DownbeatEighth /
  OffbeatEighth / Rest. Squares stay one beat; eighths at slot + 0.5.
- Rest enforcement stayed GEOMETRIC — recon proved shipped tuning
  (goodFraction 0.199 < 0.25) leaves a BPM-invariant 0.102-beat penalizable
  gap at half-beat spacing; semantic rests would have changed zero outcomes.
  Tuning guard warns if effective Good ≥ 0.25 beat on an eighth-bearing card.
- `FlashCardVocab` flags on RoomDefinition (None → controller default
  Quarter|Rest); noteRange counts note-bearing squares; slot-0 anchor rule.
- Zero evaluator/reader logic changes (evaluator code-default fractions
  aligned to scene values, constants only).

### Phase 6 — Sixteenths & triplets (2026-07-31)
- Vocabulary: REMOVED DownbeatEighth (performance-identical to Quarter; enum
  member deleted — safe, SquareTemplate is never serialized). FlashCardVocab
  bit 1<<2 is RESERVED forever (stale serialized ints — old ladder steps,
  "Everything" = -1 assets — may carry it; generator never reads it, verified
  inert with a 300-card stale-bit sample). ADDED SixteenthQuad (1<<5; taps at
  +0/.25/.5/.75) and Triplet (1<<6; +0/1/3/2/3). Both downbeat-bearing
  (anchor-eligible). Log chars: S / T.
- **Accept-leniency decision** (simulated against the real evaluator,
  CoplayScripts/SimulateDenseMatching): at quad spacing Good windows overlap
  (0.398 > 0.25) but greedy IN-ORDER bar matching pairs late-shifted clusters
  correctly (+0.10 → AllGood; +0.22 → Passable), mash → Passable with no
  phantom penalties (windows tile the square; standalone has no health),
  6-tap spam → Failed via |inputs−beats| tolerance. Per-beat window caps
  rejected: would not fix attribution (midpoint unchanged), only add
  difficulty, and would touch both evaluator hot paths.
- Tuning guard: CheckEighthsTuningGuard → CheckSubdivisionTuningGuard.
  Threshold stays Good < 0.25 (binding case now: rest 0.25 beat from a quad's
  last note); trigger broadened to any subdivided template; added
  Perfect < 0.125 check (Perfect zones must never overlap; shipped 0.051).
  New read-only DungeonEvaluator.EffectivePerfectWindowBeats.
- Endless ladder now 5 steps: 0 Q|R (3-4) → 4 +EighthPair (3-5) →
  10 +OffbeatEighth (4-6) → 16 +Triplet (4-6) → 22 +SixteenthQuad (4-6);
  FlashCards.unity serialized ints migrated 17/19/27/91/123.
- Debug_FlashCard.asset vocab = -1 (Everything) auto-gains the new templates —
  approved as-is (debug room).
- Physical: quad inter-tap 143ms @105 (7/s), triplet 190ms (5.25/s); the drum
  accepts alternating multi-touch. Content guideline: keep quads out of
  dungeon rooms ≥ 120 BPM.
- Verified: 2000-card full-vocab sample (anchor/range/fractional beat shapes,
  incl. float-third precision — error ~1e-8 beat, non-issue), 500-card legacy
  + 300-card stale-bit samples, dense-matching sim, endless run through all 5
  ladder steps with 17-beat cards grading AllPerfect, dungeon mixed-floor
  regression. Triplet/quad placeholder glyphs generated + wired in both scenes.

### Phase 4 — Standalone endless mode (2026-07-31)
- **Phase 4.0 recon findings (all confirmed in build):**
  - FlashCardController's only dungeon entanglement was
    DungeonModeController.SessionStartVirtualTime (grid origin) — solved by
    `StartEndless(start, gridOrigin, vocab, noteRange)` taking the origin
    explicitly. Reader/evaluator are scene-instantiable; TimingCoordinator is
    never consulted on the FlashCard input path; DungeonEffectsController
    null → window scale 1 by design.
  - Penalty routing: NO health object at all (reader + evaluator are
    `health?.` null-safe). Run failure is strikes-based in the mode.
  - Evaluator seams (additive, dungeon defaults unchanged): serialized
    `highScoreKey` ("FlashCardsHS" in this scene) and
    `resetScoreOnRepeatedFailure` (false here — dungeon double-fail zeroing
    would wipe endless scores on consecutive strikes).
  - OnPause/OnResume: NO rescheduling obligations — reveal is silent, BGM
    pause is central (PauseAllAudio + accumulatedPauseDuration), controller
    is GameClock-polled. OnPause = SaveHighScore only.
  - AllPerfect already implies Inputs == Beats (DetermineGrade), so
    "streak = consecutive AllPerfect" makes spam break streaks for free.
- **Build:** `FlashCardsModeController` (namespace KrumbleHut.Drummi.FlashCards)
  — ModeId "FlashCards"; endless via `CalculateTotalBeats` = 100k;
  serialized escalation ladder {minCardsCleared, vocab, noteRange}:
  0+ Q|R 3-4 → 4+ +EighthPair 3-5 → 10+ +DownbeatEighth 4-6 →
  16+ +OffbeatEighth 4-6; 3 strikes (Failed cards) end the run;
  `SetCardConfig` applies ladder changes on the NEXT card by construction.
  Serialized music pair: track 1 @ 105 BPM (stand-in), BGM loops.
- **Scene:** FlashCards.unity built by CoplayScripts/BuildFlashCardsScene
  (rerunnable): GameClock/Metronome/TimingCoordinator/GameManager+PauseHandler/
  ScreenTransition/UIMenuManager (Main+Score pages)/AudioManager prefab/
  single-drum rig (rect wired straight into DrumPadTouch.centerPad — no
  layout controller)/HUD (3 strike pips, score, streak)/card panel (dungeon
  geometry)/ScoreScreen (best-streak + cards-cleared texts owned by the mode,
  written before CompletMode). Build Settings entry added; Home scene got a
  FlashCards bubble (duplicated Dungeon bubble → GoToFlashCardsScene, added
  to UIMenuManager as a one-liner).
- **Verified in editor:** escalation at exactly 4/10/16 cleared with
  next-card-only vocab landing (card patterns: pure Q → first E on card 4,
  first D on card 10, first O on card 16); 3-strike run-over → results →
  replay with fully reset state; pause/resume mid-reveal and across the
  perform boundary resumes at +2.2ms deltas; scene circuit FlashCards → Home
  → Dungeon → Home → FlashCards leaves no residue (singletons healthy, one
  view, timeScale 1) and a fresh run works after; Phase-3 mixed dungeon floor
  run passes untouched (4 rooms, run won).
- **PENDING: mobile device pass** — menu flow + a full endless run on device
  (touch latency vs Perfect window, drum hit-zone size, HUD readability).
- Editor-harness gotcha: driving FlashCard encounters by script WITHOUT the
  room flow leaves DungeonHealth at 0 in the Dungeon scene (first penalty =
  instant game-over teardown); the standalone scene has no health object so
  the issue cannot occur there.

## Immediate next step

- **Karl's manual editor playtest** (see below) — includes eyeballing the new
  grade-flash colors for tier distinctness (code-verified only; colors are
  serialized on FlashCardView).
- Room end condition question remains OPEN (deferred out of Phase 3): fixed
  card count from RoomLengthBars is the room behavior; repeat-until-pass is
  standalone-mode territory unless design says otherwise.
- Adding FlashCard rooms to real floors is now a pure content decision
  (author RoomLengthBars even, optional flashCardNoteRange, SingleDrum
  layout) — intentionally NOT done (tutorial/introduction design deferred).

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
