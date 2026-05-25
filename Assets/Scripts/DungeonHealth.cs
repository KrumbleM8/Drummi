using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tracks player health for Dungeon mode.
/// Two damage sources:
///   - Bar failure: missedBeats * damagePerMiss
///   - Out-of-window input: flat outOfWindowPenalty per tap
/// Health is clamped to [0, maxHealth] but causes no game-over.
/// </summary>
public class DungeonHealth : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxHealth         = 100;
    [SerializeField] private int damagePerMiss     = 10;
    [SerializeField] private int outOfWindowPenalty = 1;

    [Header("Regeneration")]
    [SerializeField] private float regenPerBar = 0f;

    [Header("UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private float  sliderLerpSpeed = 8f;

    /// <summary>Fired once when CurrentHealth reaches zero. Reset by ResetHealth().</summary>
    public event Action OnHealthDepleted;

    public int CurrentHealth { get; private set; }

    private bool  _isDead;
    private float _displayedValue;

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        _isDead = false;
        InitSlider();
        UpdateDisplay();
    }

    public void TakeMissDamage(int missedBeats)
    {
        if (missedBeats <= 0) return;
        int damage = missedBeats * damagePerMiss;
        CurrentHealth -= damage;
        Debug.Log($"[DungeonHealth] Miss damage: -{damage} ({missedBeats} missed)  HP: {Mathf.Max(0, CurrentHealth)}");
        CheckDepletion();
        UpdateDisplay();
    }

    public void TakeOutOfWindowPenalty()
    {
        CurrentHealth -= outOfWindowPenalty;
        Debug.Log($"[DungeonHealth] Out-of-window penalty: -{outOfWindowPenalty}  HP: {Mathf.Max(0, CurrentHealth)}");
        CheckDepletion();
        UpdateDisplay();
    }

    /// <summary>
    /// Applies continuous drain damage (e.g. Elite enemy Phase 5).
    /// <paramref name="amount"/> is a float; damage applied is Mathf.CeilToInt(amount).
    /// </summary>
    public void TakeDrainDamage(float amount)
    {
        if (amount <= 0f) return;
        int damage = Mathf.CeilToInt(amount);
        CurrentHealth -= damage;
        Debug.Log($"[DungeonHealth] Drain damage: -{damage}  HP: {Mathf.Max(0, CurrentHealth)}");
        CheckDepletion();
        UpdateDisplay();
    }

    /// <summary>
    /// Called by DungeonBeatManager at the end of each bar when regenPerBar > 0.
    /// Stub — regen logic to be implemented when room/floor systems are ready.
    /// </summary>
    public void Regenerate()
    {
        // TODO: implement bar-end regeneration using regenPerBar
    }

    private void CheckDepletion()
    {
        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            if (!_isDead)
            {
                _isDead = true;
                OnHealthDepleted?.Invoke();
            }
        }
    }

    private void Update()
    {
        if (healthSlider == null) return;
        float target = Mathf.Max(0, CurrentHealth);
        if (Mathf.Approximately(_displayedValue, target)) return;
        _displayedValue      = Mathf.Lerp(_displayedValue, target, Time.deltaTime * sliderLerpSpeed);
        healthSlider.value   = _displayedValue;
    }

    private void InitSlider()
    {
        if (healthSlider == null) return;
        healthSlider.minValue    = 0;
        healthSlider.maxValue    = maxHealth;
        healthSlider.wholeNumbers = false;
        _displayedValue          = maxHealth;
        healthSlider.value       = maxHealth;
    }

    private void UpdateDisplay()
    {
        // CurrentHealth is updated immediately; Update() lerps the visual toward it.
    }
}
