# Drummi Dungeons — Design Document

> **Source of truth:** `docs/DungeonDesign.md` in the Drummi 2 repo.
> A copy is synced to Google Drive ("Drummi — Dungeon Design Document") for use in the Claude web project. When this file changes, the Drive copy must be re-synced.
>
> **Status tags:** `[Built]` implemented and verified · `[Partial]` some support exists · `[Planned]` design only
>
> Last updated: 2026-07-17

---

## 1. Vision & Pillars

Drummi Dungeons is a **rhythm dungeon-crawler roguelike** — one gamemode within Drummi. The player descends through floors of a dungeon, fighting enemies by performing rhythms, while the run mutates around them through forced effect choices.

**Pillars:**

1. **The music is the dungeon.** Each floor has one infinitely looping track. Rooms transition on bar seams — the music never stops or restarts mid-floor. Difficulty, enemies, and events are all expressed musically.
2. **Choices make it harder, not easier.** Unlike typical roguelikes, the recurring 1-of-3 "upgrade" pick is mostly a choice between *challenges*. The run gets harder and stranger as it goes — that's the point.
3. **The run builds the arrangement.** Some effects add an instrument stem to the looping track. A deep run doesn't just look different — it *sounds* fuller.
4. **Skill is the only power.** No meta power creep. Playing and dying unlock *content* (effects, floors, cosmetics), never stat advantages. A first-run player and a veteran have identical tools.

---

## 2. Run Structure

### Floors `[Partial]`
- A run consists of a **fixed number of floors** — target **3–5+** (exact count TBD via playtesting).
- Each floor has a **predetermined music track and environment** (background/theme). `[Built — Floor1–3 assets, floor-level BGM/BPM/BackgroundSprite]`
- Floors are the primary difficulty stair-step: deeper floors have **higher BPM and denser, more complex patterns** authored into their tracks.
- Every floor ends in a **boss room**. `[Planned]`

### Rooms `[Built]`
- Each floor is an ordered sequence of rooms (`DungeonFloorDefinition.Rooms`), target roughly 6–12 per floor (TBD).
- Navigation is **mostly linear** with occasional **branching via DirectionChoice rooms** — the player picks a door, choosing between paths/room types. `[Built — DirectionChoice room type]`
- Room transitions are **phase-locked to bar seams**: music continues seamlessly, encounters swap at the bar boundary. `[Built]`
- The first room of each floor uses the **3-phase start flow**: intro animation → time-zero music start → musical lead-in bars before the encounter begins. `[Built — leadInBars]`

### Winning `[Built — basic flow]`
- Reaching the end of the final floor (defeating its boss) **wins the run**. `[Built — completing the final room ends the run via GameManager.TriggerRunComplete(); placeholder "Run Complete!" title on the shared results screen. A dedicated victory screen is planned; the `_runWon` branch in GameManager.ShowResultsUI is the single swap point.]`
- Winning grants **exclusive unlocks unavailable anywhere else in the game**: cosmetics (future cosmetic system), content for other gamemodes (songs, instruments, skins), and potentially additional dungeon content. `[Planned]`

### Losing `[Built — basic flow]`
- HP reaching 0 ends the run immediately (death → results screen with score + floor reached).
- **No meta power progression** on death — see §8.

---

## 3. Health & Failure

### HP pool `[Built]`
- One health pool for the **entire run** (`DungeonHealth`). Damage persists across rooms and floors.
- Damage sources:
  - **Missed beats** in a failed bar (`missedBeats × damagePerMiss`)
  - **Out-of-window taps** (spam penalty)
- Death at 0 HP fires once (`OnHealthDepleted`) and ends the run.

### Healing `[Planned]`
- **Scarce by design.** No passive or performance-based regeneration.
- Healing comes only from **rest rooms** and **item pickups** (see §5).
- `DungeonHealth.Regenerate()` exists as an unimplemented stub — the per-bar regen concept is **rejected** by this design; the stub should be removed or repurposed for rest-room healing.

### Per-bar grading `[Built]`
- Each bar is graded via `DungeonEvaluator.EvaluateBar`: **Perfect (200 pts) / Good (100) / Passable (50) / Failed (0 + damage)**.
- Two-strike score rule: first failed bar flags the run; a second failed bar wipes accumulated score to 0.

---

## 4. Effect System `[Built — framework + first pool of 3]`

The signature roguelike mechanic. "Upgrades" in the loose sense — most are **not beneficial**.

### Pick flow `[Built — placeholder presentation]`
- After every **2 rooms** (serialized cadence on DungeonRunner, global across floors), the player is presented with **3 effects** and **must pick exactly one**. No skipping.
- Implementation: the pick defers the next room load between rooms — the encounter is already disarmed there, the floor BGM loops on untouched, and the next room starts at the first bar seam after the pick via the normal room-start path.
- Offers are random from the pool, **excluding currently-active effects** (re-eligible after expiry). If the whole pool is active the pick is skipped.
- Current presentation is a **placeholder card overlay**; the pick moment should eventually be musical/diegetic (presented in the world rather than a menu, TBD — see §10).

### Effect categories
Primary categories for the initial pool:

| Category | Examples (not final) |
|---|---|
| **Timing / difficulty** | Tighter hit windows `[Built]` · faster patterns · denser notes *(rejected — no clean density parameter; density is emergent from designer-set pattern durations)* · no lead-in bars *(rejected for the initial pool)* · shorter response windows in C&R *(covered — window multipliers apply to C&R echo grading automatically)* |
| **Visual sabotage** | Hidden slider spawn markers `[Built]` · remove beat indicators · shrink/hide hit zones · periodic visual distractions · obscured telegraphs |

**Current pool** (`ScriptableObjects/Effects/`): **Tight Timing** (run, windows ×0.75) · **Precision Freak** (3 rooms, windows ×0.6) · **Blind Slider** (3 rooms, slider spawn markers hidden). Window multipliers stack multiplicatively across active effects.

**Architecture:** `EffectDefinition` SO (name/description/duration + modifier list) + `DungeonEffectsController` (run-scoped scene singleton). Systems **query the aggregate at point of use** (multipliers multiply, bools OR) — effects never write into other systems' state. Read-sites: DungeonEvaluator timing windows, DungeonVisualController spawn markers.

Other categories (audio sabotage, run-structure manipulation like room-type frequency shifts) are candidates for later pool expansion — see §10.

### Durations `[Built]`
- **Mix of durations** is part of effect design:
  - Most effects are **permanent for the run**.
  - Some are **floor-scoped** (expire on floor transition).
  - Some are **timed** ("for the next N rooms" — active for exactly N rooms starting with the next; ticked at room starts).
- Duration is displayed on the pick card so the choice is informed. `[Built]`

### Effects and music stems
- **Some effects carry a music stem**: picking them adds a new instrument/layer to the floor's looping track. Stems are a *flavor* of effect, not a universal rule.
- Floor tracks must be authored with stem layers to support this (see §6).
- Buff/positive effects are **to be designed later** — the initial pool is mostly negative/challenge effects.

---

## 5. Room Types

| Room type | Status | Design |
|---|---|---|
| **Combat (standard)** | `[Built]` | Enemies **swarm in, one per pattern indicator**. Player hits the generated pattern to defeat them. 8-beat bar cycle, pattern gen → spawn → input evaluation. Variants: ThreePad, TwoPad (`CombatTwoPad`), SingleDrum layouts. |
| **Call & Response** | `[Built]` | A **single enemy fires note attacks** that the player must echo back. Multi-round phrase sequencing, echo graded per bar. Includes SingleDrum Bongo variant (BongoRoom). |
| **DirectionChoice** | `[Built]` | No combat. Player picks a door to choose the next path/branch. Doors should eventually preview/hint what lies behind them (TBD). |
| **Boss** | `[Planned]` (`BossRoom` asset + `EnemyTier.Boss` exist) | Ends every floor. Boss designs **vary wildly** — unique mechanics per boss, often **combinations of combat + C&R**. Boss/music interaction is an open question (§10). |
| **Rest / heal** | `[Planned]` | Safe room. Primary healing source. May host other non-combat interactions (e.g. effect cleansing — TBD). |
| **Event / oddity** | `[Planned]` | Non-standard encounters: minigames, gambles, story moments, mystery rewards. |
| **Empty (secret)** | `[Planned]` | Apparently empty rooms containing **secret interactable doors** — reward exploration and attention. |
| **Rhythm puzzle** | `[Planned]` | Mini rhythm/music puzzles solved for rewards (items, pickups, secrets). |

Elite variant of combat exists as data (`EliteRoom`, `EnemyTier.Elite`) — harder combat with better score, no unique mechanics defined yet.

---

## 6. Music System

### Looping floor tracks `[Built — single track per floor]`
- Each floor owns **one infinitely looping track** at a fixed BPM (`DungeonFloorDefinition.BgmTrackIndex` / `.Bpm`).
- Looping relies on Unity's sample-accurate AudioSource loop — **clips must be exported bar-exact** (whole number of 4/4 bars at the floor BPM) or the loop drifts out of phase with the beat grid. Delve Bit (34 bars) and ComputerBoy (62 bars) are exact; **Boppy_110 is 41.25 bars and needs re-export**.
- Rooms may override track/BPM via `-1` sentinel fields (`RoomDefinition.BgmTrackOverride` / `.BpmOverride`), swapped at the next bar seam. Override precedence: room → floor → current state. `[Built]`
- Music starts at time-zero on floor entry and is never delayed; encounter logic is what waits (lead-in bars). `[Built]`

### Dynamic stems `[Planned]`
- Floor tracks are authored as a **base loop + mutable stem layers** (instruments).
- **Stem sources:**
  - **Effect-linked stems**: picking certain effects enables a stem for as long as the effect lasts (see §4).
- Stems must loop at the same length/tempo as the base track and toggle at bar seams.
- Technical approach (multiple synced AudioSources vs. mixer snapshots vs. authored stem sets) is an implementation decision for later.

### Timing infrastructure `[Built]`
- All timing derives from **GameClock virtual time** with DSP-scheduled audio; bar seams are phase-locked; the metronome stays enabled throughout the run.

---

## 7. Difficulty Curves

Two stacked curves:

1. **Floor-driven (authored)** `[Partial — floor BPM exists]`
   - Deeper floors = higher BPM, denser/more complex patterns, harder enemy tiers, harsher room distributions.
   - Baked into each floor's track and room list — predictable and learnable.

2. **Effect-driven (emergent)** `[Built — first pool live]`
   - Mandatory effect picks accumulate, making each run harder in a player-influenced way.
   - By the final floor, the player is playing the hardest music **and** carrying a run's worth of debuffs.

The intersection is the design's core tension: the player has *some* control over **how** the game gets harder, but not **whether**.

---

## 8. Meta Progression & Unlocks `[Planned]`

**Rule: unlocks, never power.** Nothing earned between runs makes the player mechanically stronger.

Unlockable through playing and dying (milestones, depth reached, secrets found):
- **New effects** added to the pick pool (deeper runs discover effects that then appear in future runs)
- **New floors / music tracks**
- **Cosmetics** (for the to-be-implemented cosmetic system)
- **Drum button skins + input sounds**
- **Content for other gamemodes**

Unlockable **only by winning** (reaching the end):
- Exclusive rewards unavailable anywhere else in the game — cosmetics, content for other modes, potentially dungeon content (see §2).

Persistence system (save data for unlocks) does not exist yet.

---

## 9. Current Implementation State

As of 2026-07-17 (verified against code, commit `f800e03`).

### Scripts (Assets/Scripts/)
| Script | Responsibility |
|---|---|
| `DungeonRunner` | Sequences the run (floors → rooms), aggregates score, routes win/loss, defers room loads for effect picks (`pickCadence`), drives effect lifecycle hooks |
| `DungeonEffectsController` | Run-scoped active-effect holder; aggregate queries (window multiplier, marker hiding), NRooms/Floor expiry, pick offers |
| `EffectPickOverlayController` | Placeholder 1-of-3 pick overlay (cards, no skip) under TransitionOverlayCanvas |
| `DungeonModeController` | ModeController subclass; audio/timing across rooms, bar-seam continuity, BGM/BPM precedence, musical lead-in (`leadInBars`, `EncounterStartVirtualTime`, `PrepareLeadIn()` stub for floor transitions) |
| `RoomController` | Per-room state machine (Entering → Playing → Evaluating → Exiting), routes C&R vs combat vs DirectionChoice, `introDuration` pre-clock intro |
| `DungeonBeatManager` | 8-beat bar cycle, pattern generation, spawn audio scheduling, input-window gating, `EncounterActive` gating during lead-in |
| `DungeonEvaluator` | Timing-window grading (Perfect/Good/Passable/Failed), scoring 200/100/50, two-strike score wipe, damage calc; windows scaled by active effects |
| `DungeonHealth` | Run-wide HP, `damagePerMiss` + `outOfWindowPenalty`, unimplemented `Regenerate()` stub |
| `CallAndResponseController` / `CallPerformer` | C&R phrase clock, beat-snapped rounds, directional bounce visuals, pre-scheduled audio |
| `DungeonInputReader` | Tap input, bar-start coyote buffer, EncounterActive gating |
| `DungeonVisualController` | Enemy spawning, slider markers (suppressible by effects), hit/miss visuals |
| `DungeonInputLayoutController` | ThreePad / TwoPad / SingleDrum rig switching |
| `DungeonGameOverHandler` | Defers to DungeonRunner mid-run; otherwise GameManager.TriggerGameOver() |

### Data & assets
- `DungeonFloorDefinition`: BgmTrackIndex, Bpm, BackgroundSprite, ordered Rooms list
- `RoomDefinition`: RoomType enum (CombatEncounter, DirectionChoice, CallAndResponse, CombatTwoPad), EnemyTier (Standard/Elite/Boss), BgmTrackOverride/BpmOverride (-1 sentinel), RoomLengthBars
- Assets: `StandardRoom`, `EliteRoom`, `BossRoom`, `BongoRoom`; `Floor1`–`Floor3`
- `EffectDefinition`: DisplayName, Description, Duration (Run/Floor/NRooms) + RoomCount, modifier list (TimingWindowMultiplier, HideSliderMarkers). Assets: `TightTiming`, `PrecisionFreak`, `BlindSlider`

### Not built yet
- Dynamic music stems (incl. effect-linked stems)
- Diegetic effect-pick presentation (placeholder card overlay exists)
- Rest, event, empty/secret, and rhythm-puzzle room types
- Boss encounters (asset/tier exist; no boss mechanics)
- Dedicated victory screen (win reuses the results screen with a placeholder title)
- Meta progression / unlock persistence
- Cosmetic system
- HP pickups / rest-room healing
- Floor-transition lead-ins (`PrepareLeadIn()` exists but is not called by DungeonRunner)

---

## 10. Open Questions

1. **Run length numbers** — exact floor count (3–5+?) and rooms per floor; effect-pick cadence (implemented as every 2 rooms, serialized and tunable).
2. **Boss music** — full-stem climax of the floor loop vs. authored boss section vs. separate boss track at a bar seam.
3. **Buff design** — the pool is mostly negative; what do positive/hybrid effects look like without violating the "skill is the only power" pillar within a run?
4. **Additional effect categories** — audio sabotage, run-structure manipulation (room-type frequency, floor length) as pool expansions.
5. **Effect pick presentation** — diegetic vs. menu; when exactly in the bar/room flow the pick happens.
6. **Effect cleansing** — can rest/event rooms remove an effect, and at what cost?
7. **Cosmetic system** — dependency for win rewards; not yet designed.
8. **Stem tech approach** — synced AudioSources vs. mixer snapshots vs. authored stem sets.
9. **DirectionChoice previews** — how much do doors telegraph about what's behind them?

---

## 11. Glossary

- **GameClock / virtual time** — shared virtual timeline all modes derive timing from; audio is DSP-scheduled against it.
- **Bar seam** — a bar boundary at which rooms transition and track/BPM swaps occur; music never restarts mid-floor.
- **Lead-in (`leadInBars`)** — bars at floor start during which music plays but the encounter is suppressed, letting the player hear the song first.
- **3-phase first-room flow** — (1) intro animation pre-clock → (2) time-zero: GameClock reset + music scheduled → (3) lead-in bars → encounter starts.
- **C&R** — Call & Response: enemy performs a phrase, player echoes it, echo graded per bar.
- **Stem** — an individual instrument layer of a floor track that can be toggled while the base loop plays.
- **Effect** — a run modifier picked 1-of-3 every ~2 rooms; mostly challenges, sometimes stem-linked.
