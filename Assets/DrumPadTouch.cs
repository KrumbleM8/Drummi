using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using System.Collections.Generic;

public class DrumPadTouch : MonoBehaviour
{
    [SerializeField] private RectTransform leftPad;
    [SerializeField] private RectTransform rightPad;

    private PlayerInputReader playerInputReader;

    private HashSet<int> processedFingers = new HashSet<int>();
    private const float AUDIO_LATENCY = 0.05f;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
        TouchSimulation.Enable();
#endif

        playerInputReader = GameManager.instance.gameObject.GetComponent<PlayerInputReader>();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        TouchSimulation.Disable();
#endif
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        // Clear processed fingers each frame
        processedFingers.Clear();

        foreach (Touch touch in Touch.activeTouches)
        {
            // Only process Began phase
            if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;

            int fingerId = touch.finger.index;
            if (processedFingers.Contains(fingerId)) continue;
            processedFingers.Add(fingerId);

            Vector2 screenPos = touch.screenPosition;

            // Check left pad
            if (RectTransformUtility.RectangleContainsScreenPoint(leftPad, screenPos))
            {
                PlayHit("LeftPad", fingerId);
                continue;
            }

            // Check right pad
            if (RectTransformUtility.RectangleContainsScreenPoint(rightPad, screenPos))
            {
                PlayHit("RightPad", fingerId);
            }
        }
    }

    private void PlayHit(string padName, int fingerId)
    {
        bool isRightPad = padName == "RightPad";
        playerInputReader.TriggerInput(isRightPad);

        Debug.Log($"{padName} hit by finger {fingerId} at {Time.time}");
    }
}