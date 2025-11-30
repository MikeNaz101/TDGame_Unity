using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TowerUpgradeNodeUI : MonoBehaviour
{
    [Header("UI Components")]
    public Image iconImage;
    public Image backgroundImage;
    public GameObject connectionLineLeft; // Line connecting to previous node
    public GameObject connectionLineRight; // Line connecting to next node
    
    [Header("Style Settings")]
    public Color lockedColor = Color.gray;
    public Color unlockedColor = Color.white;
    public Color purchasedColor = Color.green;
    public Vector3 selectedScale = new Vector3(1.2f, 1.2f, 1f);
    public Vector3 normalScale = Vector3.one;

    private RectTransform _rect;
    private Button _btn;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _btn = GetComponent<Button>();
    }

    public void Setup(TowerUpgradeData data, bool isPurchased, bool isUnlockable, bool hasNext)
    {
        if (data != null) iconImage.sprite = data.icon;
        
        // Connection Lines
        // We only show the right line if there is a next node
        if(connectionLineRight) connectionLineRight.SetActive(hasNext);
        
        // Colors
        if (isPurchased) backgroundImage.color = purchasedColor;
        else if (isUnlockable) backgroundImage.color = unlockedColor;
        else backgroundImage.color = lockedColor;
    }

    public void UpdateVisuals(float distanceToCenter)
    {
        // 0 distance = center. 
        // We calculate a scale factor based on how close we are to the center (0)
        // Range 0 to 300 pixels
        
        float maxDist = 300f;
        float factor = 1f - Mathf.Clamp01(Mathf.Abs(distanceToCenter) / maxDist);
        
        // Lerp scale
        transform.localScale = Vector3.Lerp(normalScale, selectedScale, factor);
        
        // Optional: Fade alpha for distant nodes?
        // var col = backgroundImage.color;
        // col.a = 0.5f + (factor * 0.5f);
        // backgroundImage.color = col;
    }
}