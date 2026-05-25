# Drummi Dungeons — Project Context

## What this project is
A mobile-first rhythm roguelike dungeon crawler built in Unity 6.
Arcade tone, short sessions, portrait orientation.
Platforms: iOS, Android (primary), PC (later port).

## Architecture rules
- All beat-sensitive timing uses GameClock.GameTime (DSP-based virtual time).
  Never use Time.time, Time.deltaTime, or WaitForSeconds for timing logic.
- New Input System only. No legacy Input.GetKey anywhere.
- Object pooling via ObjectPooler for all spawned note/enemy objects.
- ModeController pattern: all game modes subclass ModeController and register
  with GameManager. DungeonModeController is the active dungeon mode.
- Events over direct calls: cross-system communication uses C# Action events.

## Core systems — do not rewrite
- GameClock.cs — DSP virtual time, pause-aware
- TimingCoordinator.cs — bar/beat cycle, BarTiming structs
- Metronome.cs — OnTickEvent per beat
- PauseHandler.cs — coordinates pause across all systems
- DrumPadTouch.cs — 3-pad touch input (L/C/R events)
- DungeonInputReader.cs — coyote buffer, per-hit evaluation, allowInput gate
- DungeonBeatManager.cs — bar-cycle orchestrator for standard enemies
- DungeonPatternGenerator.cs — 3-type (L/C/R) random pattern generation
- DungeonEvaluator.cs — bar evaluation + live per-hit quality grading
- DungeonVisualController.cs — bar slider, spawn markers, enemy sprite pool
- DungeonEnemyVisual.cs — pooled enemy sprite (spawn/hit/barend animations)
- DungeonModeController.cs — GameManager lifecycle wiring
- AudioManager.cs — music scheduling + SFX (extend, don't rewrite)

## Data types in use
- DungeonEnemyType enum: Left=0, Center=1, Right=2
- DungeonBeat: duration + timeSlot + enemyType
- DungeonScheduledBeat: virtualTime + enemyType (identity key)
- DungeonInput: virtualTime + enemyType (per tap record)
- InputMatch: MatchQuality enum (Perfect/Good/Miss) + TimingError + BeatIndex
- EvaluationResult: Grade enum + hit counts + score points

## Current build phase
Working on Drummi Dungeons roguelike layer.
Standard combat is complete. Building room/floor/progression systems.

## Naming conventions
- Namespace: KrumbleHut.DrummiDungeons.[System]
- ScriptableObjects: suffix Definition (e.g. RoomDefinition, BossPhaseDefinition)
- MonoBehaviours: suffix Controller, Handler, Manager, Runner
- Plain data classes: no suffix (e.g. RoomResult, DungeonInput)
- SerializeField over public fields
- XML doc comments on all public methods
