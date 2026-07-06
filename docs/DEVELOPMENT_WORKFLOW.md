# Drummi — Feature Development Workflow

My personal process for taking a feature from a vague idea to a tested,
working implementation, using Claude.ai as planning partner and Claude Code
(via Coplay) as the builder. This exists because the process works — but only
if I run it in order. The temptation is always to skip ahead to the fun part
(animation, polish, the visible result). Skipping is what causes the thrash.

---

## The core rhythm

One loop, repeated per phase:

**recon → confirm → spec → build → report-back → verify → lock**

1. **Recon** — Claude Code reads the *actual* current code/scene, read-only.
   No edits. This is the non-negotiable step (see "Why recon is not the slow
   part" below).
2. **Confirm** — I read the recon back to the planning partner. It routinely
   overturns an assumption. That's the point.
3. **Spec** — planning partner writes a self-contained hand-off prompt for
   Claude Code (assumes it can see the project, has NOT seen our chat).
4. **Build** — I run the hand-off in Claude Code.
5. **Report-back** — the hand-off ends with specific things to confirm. These
   target *interruption and re-entry cases*, not the happy path.
6. **Verify** — I read the report and/or playtest. The happy path always
   works; I'm looking for the edge cases.
7. **Lock** — record settled decisions in the WIP doc so they don't get
   relitigated next session.

Then the next phase.

---

## The two disciplines that actually made this work

Everything else is scaffolding around these two.

### 1. Name the load-bearing risk first, and prove it before anything else

Before writing a single phase, ask: **what is the one thing that, if wrong,
breaks the whole feature?** Order the phases so that risk is proven *first*,
with everything else instant/stubbed around it.

- For a stateful UI feature, the risk was usually **input routing / state
  gating** — so I built the whole state machine with *instant* transitions and
  proved the routing couldn't leak *before* animating anything. The animations
  then layered onto a foundation already known to be correct.
- For an **audio-sync feature** (layering stems in time, beat-locked
  gameplay), the risk is **sample-accurate synchronization** — one shared DSP
  timeline, everything scheduled against a common origin. That has to be proven
  first; visuals and input come after. Applying the UI ordering here would
  optimize the wrong thing.

The feature's shape decides its risk. Don't reuse last feature's ordering by
reflex — re-ask the question each time. This single step is what makes the
process transfer across different kinds of features.

### 2. Layer onto proven foundations, never onto unverified ones

Instant/stubbed transitions before animated ones. State logic before feel.
Data model before the thing that renders it. If phase N sits on phase N-1,
phase N-1 must be *verified* (not just "written") before N starts. Testing
polish on top of an unverified foundation muddies which layer actually broke.

---

## Why recon is not the slow part (don't automate it away)

The instinct is: "the hand-off prompts are repetitive, can I template/generate
them?" The *structure* — yes (see scaffolds below). The *content* — no.

Every good hand-off was written against fresh recon that frequently overturned
what I assumed was true (where logic lived, what rendered in front of what,
whether an object even existed). A pre-written or auto-generated spec bakes in
the wrong assumptions and produces a broken hand-off. The recon-then-spec loop
is not overhead to remove — it's the part that makes the output correct.

**The realistic speedup: never re-derive the skeleton.** Keep the scaffolds
below and fill them in per phase. That's fast. Skipping the look-first step is
not fast — it's just deferred debugging.

---

## Phasing principles

- Each phase is **independently testable** and builds on the last.
- Phase 0 is usually **data model / foundational types**, not the visible
  feature. Get the shape right before anything hangs off it.
- Keep phases small enough that a report-back can meaningfully verify them.
- A phase that "adds a public surface" or "introduces a coordinator" with zero
  behavior change is legitimate and often the safest first step.
- Leave clean baselines between phases (e.g. objects at rest transform, known
  state) so the next phase composes instead of fighting residual state.

## Decision rhythm

- Planning partner proposes **defaults**; I **veto or accept in one line**.
  Keeps momentum without stalling on open questions or steamrolling intent.
- Genuinely load-bearing unknowns get **one focused question**, not a guess.
- Settled decisions go in the WIP doc immediately, so they're not reopened.

## The WIP doc is the resume anchor

A short `docs/<feature>-progress.md`, **in the repo** so both tools see it —
not just in chat. It captures resumable *state*, not prose:

- **Done & verified** (with the specific mechanisms, so I don't re-read code)
- **Immediate next step** (including any recon I still owe)
- **Still TODO** (deferred phases, with enough detail to resume cold)
- **Key decisions locked** (so they're never relitigated)

Update it at every natural stopping point and commit with a WIP message that
names the seam ("next: X"). This is what lets me stop and restart across many
sessions without re-deriving anything.

---

## What to ask for in a report-back

The whole value of the report-back is that it targets what a happy-path test
misses. Ask Claude Code (or check by playtest) to confirm the cases where bugs
actually live:

- **Interruption mid-action** — what if the user backs out / cancels while an
  animation, sequence, or audio loop is running? Does it unwind cleanly?
- **Rapid repetition / switching** — fast repeated input (switch A→B→C).
  Overlaps, ghosts, stacked tweens, double-fires?
- **Re-entry** — do it a second time. Residual state from the first run?
- **Teardown / lifecycle** — leaving the scene, launching the game, disabling
  the object mid-action — anything left dangling?
- **The load-bearing risk specifically** — for audio, "no bleed / no drift";
  for state, "the guarded thing provably cannot happen while gated."

Do NOT spend the report-back confirming the happy path. It works. Confirm the
edges.

---

## Common failure modes I've hit (and the tell)

- **Recon searched the wrong scene/file** → its "X doesn't exist" contradicts
  my lived experience of the running game. Trust the running game; re-point the
  recon. (Happened once — the "scene doesn't exist" false alarm.)
- **Claude Code went slightly beyond "read-only" or "additive"** and
  implemented a fix while reporting. Usually fine, but *verify reality matches
  the writeup* before the next phase leans on it.
- **A serialized field's type gives no hint what it should point at**
  (e.g. `GameObject[]` that wants highlights, not buttons) → silent
  mis-wiring. When authoring the scene, double-check ambiguous refs.
- **Dynamic-int button wiring set static** (all buttons pass 0) → everything
  triggers the same index. Classic Inspector mistake.
- **Modal popup with no raycast-blocking backing graphic** → taps in the gaps
  between buttons fall through to whatever's behind.

---

## Reusable scaffolds

Fill these in per phase. Structure is fixed; content comes from the current
feature and the latest recon. Keep them terse.

### Scaffold A — Read-only recon prompt

```
Read-only, no edits. [One line: what I'm about to implement and why I need
this.] Report back concisely:

1. [The thing whose current shape I must know — paste the script / show the
   data model / describe the hierarchy.]
2. [The specific mechanism I'll build on — how X is currently done: input
   detection, state tracking, scheduling, population, etc.]
3. [Any field names / public surface / sibling order / import settings I need
   to target precisely.]
4. [The load-bearing unknown — the thing that changes the whole approach
   depending on the answer.]
5. Flag anything referenced by code that has no corresponding scene object yet
   (i.e. something assumed to exist that we never authored).

No changes — I'm designing against this, not building yet.
```

Notes:
- Always state it's read-only and no edits.
- Ask for the *mechanism*, not just "does X exist" — I need to know how it
  currently works to build on it.
- Question 4 (the load-bearing unknown) is the one that most often changes the
  plan. Make it explicit.
- Question 5 catches the script-exists-but-scene-doesn't gap.

### Scaffold B — Phased implementation hand-off

```
Drummi [system] — Phase [N]: [one-line goal].
Builds on Phase [N-1] ([what exists now]). [State what must NOT change /
still work after this phase.]

CONTEXT (from recon):
- [The 2–4 facts from recon this spec depends on — sibling order, field
  names, existing method signatures, import settings, the clock/source model.]

IMPLEMENT:
1. [Step — precise, targets real names from recon.]
2. [Step — ...]
   [Include the interruption/cleanup contract inline where relevant:
   "kill any in-flight X before starting a new one"; "store the ORIGINAL
   value before animating so a mid-action cancel restores it exactly";
   "reset to clean baseline (scale 1 / rotation identity / stopped) so the
   next run composes."]
3. [Wiring into existing transition points — name them.]

[If it touches shared systems: state explicitly what it must NOT disturb —
e.g. "do not set selectedSongIndex", "do not add to speakers[]", "do not
change the wrapping math".]

REPORT BACK (no further changes): confirm [the interruption case], [the rapid
-repeat case], [the re-entry case], and [the load-bearing-risk case]. Trace it
if you can't run it.
```

Notes:
- Written assuming Claude Code can see the project but has NOT seen our chat —
  so the CONTEXT block carries the recon facts it needs.
- The report-back lists edge cases, not happy path.
- Name what must not change — the guard against a phase quietly breaking a
  shared system.
- For a **patch** to a prior phase, same shape, smaller: "Builds on Phase N,
  adds/changes exactly this."

### Scaffold C — WIP doc update prompt

```
Overwrite/update docs/<feature>-progress.md: move Phase [N] from TODO to Done,
capturing [the specific mechanism, so I don't re-read code to remember it].
Note the sole/next remaining work is [X]. No code changes this session.
```

Then commit: `WIP: <feature> phase N done; next: <seam>`.

---

## The one-line version

Look before you spec. Prove the riskiest thing first. Layer only onto verified
ground. Ask report-backs about the edges, not the happy path. Write it all down
in the repo so future-me resumes cold. The scaffolds make the structure
instant; the recon makes it correct — don't trade one for the other.
