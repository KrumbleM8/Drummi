using UnityEngine;

namespace KrumbleHut.Drummi.FlashCards
{
    /// <summary>
    /// Rotates a background layer at a fraction of the planet's spin for tiered
    /// parallax depth (near decor ~1, mid hills ~0.4, sky/clouds ~0.05). Purely
    /// event-driven off PlanetSpinner.AngleChanged — no Update loop, nothing
    /// moves at rest. Re-syncs in OnEnable so SetMenuShown off/on toggles leave
    /// the layer consistent.
    /// </summary>
    public class ParallaxRotationLayer : MonoBehaviour
    {
        [Tooltip("The planet pivot driving this layer.")]
        [SerializeField] private PlanetSpinner source;

        [Tooltip("Fraction of the planet's rotation this layer follows (0 = static sky, 1 = on-planet decor).")]
        [SerializeField, Range(0f, 1f)] private float factor = 0.4f;

        private void OnEnable()
        {
            if (source == null) return;
            source.AngleChanged += HandleAngleChanged;
            HandleAngleChanged(source.CurrentAngle);
        }

        private void OnDisable()
        {
            if (source != null) source.AngleChanged -= HandleAngleChanged;
        }

        private void HandleAngleChanged(float angle)
        {
            transform.localEulerAngles = new Vector3(0f, 0f, angle * factor);
        }
    }
}
