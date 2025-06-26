using UnityEngine;

public class CustardAnimationHandler : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public Sprite[] sprites;
    public GameObject[] effects;

    public void HandleSuccess()
    {
        spriteRenderer.sprite = sprites[5];
    }
    public void HandleFailure()
    {
        spriteRenderer.sprite = sprites[6];
    }
    public void HandleNeutral()
    {
        spriteRenderer.sprite = sprites[0];
    }
    public void HandleListening()
    {
        spriteRenderer.sprite = sprites[4];
    }
    public void PlayLeftBongo()
    {
        spriteRenderer.sprite = sprites[3];
    }
    public void PlayRightBongo()
    {
        spriteRenderer.sprite = sprites[2];
    }
}
