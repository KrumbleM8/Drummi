using UnityEngine;

public class ScrollingTexture : MonoBehaviour
{
    [Header("Scroll Settings")]
    [SerializeField] private float scrollSpeedX = 0.1f;
    [SerializeField] private float scrollSpeedY = 0f;

    [Header("Material Settings")]
    [SerializeField] private Material targetMaterial;
    [SerializeField] private string texturePropertyName = "_MainTex";

    private Material instanceMaterial;
    private Renderer rend;
    private Vector2 offset;

    void Start()
    {
        rend = GetComponent<Renderer>();

        if (rend != null)
        {
            // Create material instance to avoid modifying shared material
            if (targetMaterial != null)
            {
                instanceMaterial = new Material(targetMaterial);
                rend.material = instanceMaterial;
            }
            else
            {
                instanceMaterial = rend.material;
            }
        }

        offset = Vector2.zero;
    }

    void Update()
    {
        if (instanceMaterial == null) return;

        // Calculate offset based on time and speed
        offset.x += scrollSpeedX * Time.deltaTime;
        offset.y += scrollSpeedY * Time.deltaTime;

        // Apply offset to material
        instanceMaterial.SetTextureOffset(texturePropertyName, offset);
    }

    void OnDestroy()
    {
        // Clean up instanced material
        if (instanceMaterial != null)
        {
            Destroy(instanceMaterial);
        }
    }

    // Public method to change scroll speed at runtime
    public void SetScrollSpeed(float x, float y)
    {
        scrollSpeedX = x;
        scrollSpeedY = y;
    }
}