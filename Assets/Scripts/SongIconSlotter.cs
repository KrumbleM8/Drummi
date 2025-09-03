using UnityEngine;

public class SongIconSlotter : MonoBehaviour
{
    private GameObject currentIcon;
    public RectTransform slotTransform;
    public float adjustedScale = 0.57f;

    public void SetIcon(int index)
    {
        currentIcon = transform.GetChild(0).transform.GetChild(index).gameObject;
        currentIcon.SetActive(true);
    }

    private void OnDisable()
    {
        currentIcon.SetActive(false);
    }
}
