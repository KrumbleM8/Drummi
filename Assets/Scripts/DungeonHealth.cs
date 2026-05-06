using UnityEngine;
using TMPro;

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

    [Header("UI")]
    [SerializeField] private TMP_Text healthText;

    public int CurrentHealth { get; private set; }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        UpdateDisplay();
    }

    public void TakeMissDamage(int missedBeats)
    {
        if (missedBeats <= 0) return;
        int damage = missedBeats * damagePerMiss;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        Debug.Log($"[DungeonHealth] Miss damage: -{damage} ({missedBeats} missed)  HP: {CurrentHealth}");
        UpdateDisplay();
    }

    public void TakeOutOfWindowPenalty()
    {
        CurrentHealth = Mathf.Max(0, CurrentHealth - outOfWindowPenalty);
        Debug.Log($"[DungeonHealth] Out-of-window penalty: -{outOfWindowPenalty}  HP: {CurrentHealth}");
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (healthText != null) healthText.text = CurrentHealth.ToString();
    }
}
