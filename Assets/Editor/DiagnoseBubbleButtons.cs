using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using KrumbleHut.Drummi.UI;

public class DiagnoseBubbleButtons
{
    public static string Execute()
    {
        var buttons = Object.FindObjectsByType<BubbleButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (buttons.Length == 0)
            return "NO BubbleButton components found in scene.";

        var sb = new System.Text.StringBuilder();
        foreach (var btn in buttons)
        {
            var rt = btn.GetComponent<RectTransform>();
            var iconTransform = btn.transform.Find("Icon");
            var iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;

            // Reflect private fields
            var type = typeof(BubbleButton);
            var unlockKeyField     = type.GetField("unlockKey",       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var startUnlockedField = type.GetField("startUnlocked",   System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lockedSpriteField  = type.GetField("lockedSprite",    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var unlockedSpriteField= type.GetField("unlockedSprite",  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var iconImageField     = type.GetField("iconImage",       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lockedScaleField   = type.GetField("lockedScale",     System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            string unlockKey      = unlockKeyField     != null ? (string)unlockKeyField.GetValue(btn)        : "?";
            bool   startUnlocked  = startUnlockedField != null ? (bool)startUnlockedField.GetValue(btn)      : false;
            Sprite lockedSprite   = lockedSpriteField  != null ? (Sprite)lockedSpriteField.GetValue(btn)     : null;
            Sprite unlockedSprite = unlockedSpriteField!= null ? (Sprite)unlockedSpriteField.GetValue(btn)   : null;
            Image  cachedIcon     = iconImageField     != null ? (Image)iconImageField.GetValue(btn)         : null;
            float  lockedScale    = lockedScaleField   != null ? (float)lockedScaleField.GetValue(btn)       : 0f;

            sb.AppendLine($"=== {btn.gameObject.name} ===");
            sb.AppendLine($"  localScale:      {rt.localScale}");
            sb.AppendLine($"  unlockKey:       '{unlockKey}'");
            sb.AppendLine($"  startUnlocked:   {startUnlocked}");
            sb.AppendLine($"  lockedScale:     {lockedScale}");
            sb.AppendLine($"  lockedSprite:    {(lockedSprite  != null ? lockedSprite.name  : "NULL")}");
            sb.AppendLine($"  unlockedSprite:  {(unlockedSprite!= null ? unlockedSprite.name: "NULL")}");
            sb.AppendLine($"  iconImage(field):{(cachedIcon    != null ? cachedIcon.name    : "NULL")}");
            sb.AppendLine($"  Icon child:      {(iconTransform != null ? "found"            : "NOT FOUND")}");
            sb.AppendLine($"  Icon Image:      {(iconImage     != null ? iconImage.sprite?.name ?? "sprite=null" : "NULL component")}");
        }
        return sb.ToString();
    }
}
