# Bongo Album Song-Select — WIP status

## Done & verified in editor (PLAYABLE END-TO-END)
- SongSelectController state machine (Browsing/Expanded/Previewing/Popup) — works
- Album cards (AlbumItem + AlbumDefinition), carousel converted song→album
- Vinyls (VinylItem x3), tap-catcher, difficulty popup — built, wired, working
- Phase 2 album center + lift animation (DOTween) — works
- FinalizeSong launch path — WORKS. Game launches at correct BPM + difficulty.
  pageToCloseOnStart teardown confirmed hiding the menu correctly.

## Known issues to fix (next session)
1. CAROUSEL SNAP-BACK / wrapped duplicates. Scrolling several wraps then
   tapping a repeated album yanks the carousel all the way to the album's
   canonical index — very jarring. ROOT CAUSE: circular modulo carousel;
   centering targets the album's absolute child index, not the visual copy
   nearest the player's view. FIX DIRECTION: center on the NEAREST offset
   that centers an instance of the tapped album (snap to closest visual
   copy = small move), instead of its canonical offset. Same fix likely
   needed for _originalOffset snap-back on Collapse (can also fling across
   wraps). Keep scoped — do NOT rewrite the carousel.
2. DEFAULT DIFFICULTY = Normal (Standard, index 1). Popup must open with
   slot 1 pre-highlighted, and FinalizeSong's no-selection fallback must
   default to 1 (NOT 0). Make the two agree. (Difficulties: Starter=0,
   Standard/"Normal"=1, Spicy=2.)
3. ALBUM COVER not showing on album card. Likely NOT a misassignment —
   nothing was built to push AlbumDefinition.cover onto the card's Image.
   Verify: (a) is cover assigned in the AlbumDefinition asset? (b) does any
   code/AlbumItem drive the card's Image from definition.cover? If (b) is
   missing, that's the fix — apply cover to the card Image (and later, to
   vinyls if they show album art).

## Still TODO (deferred polish phases)
- Phase 2 snap-back patch ("B"): tween CurrentOffset → _originalOffset on
  Collapse. VERIFY whether applied; note issue #1 interacts with this.
- Phase 3: vinyl fan-out animation (vinyls currently appear instantly)
- Phase 4: vinyl scale+spin on select (spin = continuous DORotate/Update,
  NOT kill-rebuild sequence)
- Phase 5: audio preview loop (15.0→25.0s, fade in/out, repeat). StopPreview()
  is a stub FinalizeSong already calls — implement here.

## Key decisions locked
- musicTracks[] kept; SongDefinition.trackIndex maps song→clip
- SetMusic() bypassed (old SongItem coupling); FinalizeSong sets
  metronome.bpm + selectedSongIndex directly from SongDefinition
- DOTween is the tween lib; _activeSequence.Kill() before every new transition
- "Any scroll/other-album/empty tap while expanded = collapse to Browsing"
- difficulty must be set BEFORE StartGame (BeatGenerator reads it at Initialize)