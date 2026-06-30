# Bongo Album Song-Select — WIP status

## Done & verified (PLAYABLE END-TO-END)
- SongSelectController state machine (Browsing/Expanded/Previewing/Popup)
- Album cards (AlbumItem + AlbumDefinition), carousel converted song→album
- Vinyls (VinylItem x3), tap-catcher, difficulty popup — built, wired, working
- Phase 2 album center + lift animation (DOTween)
- FinalizeSong launch path — game launches at correct BPM + difficulty
- Carousel wrap snap-back FIXED: NearestEquivalentOffset helper, applied to
  both tap-centering (OnAlbumTapped) and collapse restore. No cross-wrap fling.
- Default difficulty = Normal (index 1): popup opens with Normal highlighted
  (OpenPopup calls SelectDifficulty(1)); FinalizeSong fallback also 1.
- Album cover: AlbumItem applies definition.Cover to a serialized coverImage
  in Awake (runtime only — no edit-mode preview by choice). Per-card Inspector
  wiring: assign coverImage to the card's dedicated cover-art child Image.
- Phase 3: vinyl fan-out animation.
  - Rest positions read from RectTransforms in Start (_vinylRestPos[]) —
    editor-tunable, not hardcoded.
  - On expand-complete (sequence OnComplete): each vinyl SetActive(true),
    jumps to vinylFanStartLocalPos (0,−60) at vinylFanStartScale (0.6), then
    DOAnchorPos + DOScale to rest position/scale 1, staggered 0.06s per slot.
  - Sort-order: SetSiblingIndex(0) at animation start (vinyl behind album
    cover during travel); single DOVirtual.DelayedCall after last tween fires
    RestoreVinylSiblingOrder() which iterates i = 2→0 (reverse) to avoid
    cascading-shift and places vinyls back at siblings 3/4/5.
  - Collapse/HideVinyls: KillVinylTweens() kills fan tweens AND the restore
    callback; HideVinyls iterates reverse to restore sibling indices cleanly
    from any mid-fan state.
  - 5 tunables on SongSelectController under "Vinyl Fan Animation":
    vinylFanStartLocalPos, vinylFanStartScale, vinylFanDuration,
    vinylFanEase, vinylFanStagger.
- Phase 4: vinyl scale + spin on select.
  - VinylItem owns all selection animation: Select(), Deselect(),
    CancelSelection().
  - Spin is Update-driven (transform.Rotate per frame, gated by _spinning
    bool) — NOT a DOTween loop. Avoids stutter on frequent selection changes
    that kill-rebuild tweens produce.
  - Scale via DOTween: Select() tweens to (selectedScale, selectedScale, 1)
    with OutBack ease; Deselect() tweens back to Vector3.one (no ease).
    Defaults: selectedScale 1.15, scaleDuration 0.18s.
  - Deselect() snaps localRotation to Quaternion.identity immediately
    (before scale tween starts) to preserve the Phase 3 rest-state contract.
    Snap is invisible because scale is simultaneously shrinking.
  - CancelSelection() is the hard path: kills _scaleTween, sets _spinning
    false, no new tween. Used by HideVinyls before SetActive(false) so no
    live tween survives deactivation.
  - Wiring in SongSelectController:
    - Expanded→Previewing: vinyls[songSlot].Select()
    - A→B switch: capture prevSlot before overwriting _previewingSlot,
      vinyls[prevSlot].Deselect(), vinyls[songSlot].Select()
    - OpenPopup: vinyls[_previewingSlot].Deselect() (popup covers vinyls;
      also prevents spin-under-popup edge case)
    - ClosePopup: vinyls[_previewingSlot].Select() (re-spin on dismiss)
    - Collapse/HideVinyls: CancelSelection() on all vinyls (hard cancel,
      transforms reset by HideVinyls directly after)
- Phase 5: audio preview loop.
  - Dedicated AudioSource (previewSource) on SongSelectController.gameObject —
    NOT in AudioManager.speakers[], so ResetState()/PauseAllAudio() never
    touch it. Created via AddComponent in Awake if not assigned in Inspector.
  - SongDefinition.previewStartSeconds (default 15 s) tells StartPreview()
    where to seek within the clip. Clamped so window fits before clip.length.
  - All tracks are PCM Decompress-On-Load; audioSource.time = N is free.
  - Coroutine (PreviewLoop) owns loop structure: fade-in → hold → fade-out →
    re-seek → repeat. WaitForSecondsRealtime so wall-clock is used (unaffected
    by paused game time). DOTween (DOFade) owns the actual volume curves.
  - TearDownPreview() kills _previewFadeTween + stops _previewLoopCoroutine.
    Called inside StopPreview() before the stop-fade, and at the top of
    StartPreview() so no orphaned coroutine/tween survives a slot switch.
  - StopPreview() = TearDownPreview() + DOFade(0, stopFade).OnComplete(Stop).
    Safe to call when nothing is playing.
  - 5 tunables under "Audio Preview": previewSource (optional, auto-created),
    previewLengthSeconds (10), previewFadeInSeconds (0.6),
    previewFadeOutSeconds (0.4), previewStopFadeSeconds (0.15).
  - Wiring:
    - Expanded→Previewing: StartPreview(songSlot)
    - A→B switch: StopPreview() then StartPreview(newSlot)
    - OpenPopup: StopPreview() (added — was previously missing)
    - ClosePopup: StartPreview(_previewingSlot) (re-start on dismiss)
    - Collapse: StopPreview() (already present)
    - FinalizeSong: StopPreview() (already present)
    - OnDisable/OnDestroy: TearDownPreview() + previewSource.Stop()

## Still TODO (deferred polish — none blocking; game is fully playable)
- None. All song-select phases (0–5) are implemented.

## Key decisions locked
- musicTracks[] kept; SongDefinition.trackIndex maps song→clip
- SetMusic() bypassed (old SongItem coupling); FinalizeSong sets
  metronome.bpm + selectedSongIndex directly from SongDefinition
- DOTween is the tween lib; _activeSequence.Kill() before every new transition
- "Any scroll/other-album/empty tap while expanded = collapse to Browsing"
- difficulty set BEFORE StartGame (BeatGenerator reads it at Initialize)
- offset space is continuous/modular, period = count*spacing; center on
  nearest visual copy, never canonical index
