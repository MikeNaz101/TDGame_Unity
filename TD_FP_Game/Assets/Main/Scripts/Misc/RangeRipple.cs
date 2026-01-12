using UnityEngine;

public class RangeRipple : MonoBehaviour
{
    [Header("Settings")]
    public float pulseSpeed = 2.0f;
    public Color validColor = new Color(0, 1, 0, 0.3f); // Transparent Green
    public Color invalidColor = new Color(1, 0, 0, 0.3f); // Transparent Red

    private Material _mat;
    private float _currentRange = 10f;
    private float _timer = 0f;
    private Transform _visualMesh;

    void Start()
    {
        // Try to find a renderer to get the material
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            // We create a clone of the material so we can fade it individually
            _mat = rend.material; 
            _visualMesh = rend.transform;
        }
    }

    public void SetProperties(float range, bool isValid)
    {
        _currentRange = range;
        
        if (_mat != null)
        {
            // Set color based on validity (Red if blocked, Green if good)
            Color targetColor = isValid ? validColor : invalidColor;
            
            // Keep the alpha fading logic separate, so just update RGB
            Color currentColor = _mat.color;
            targetColor.a = currentColor.a; // Preserve current alpha for the pulse effect
            _mat.color = targetColor;
        }
    }

    void Update()
    {
        if (_visualMesh == null) return;

        // 1. Calculate Pulse (0 to 1 loop)
        _timer += Time.deltaTime * pulseSpeed;
        if (_timer > 1.0f) _timer = 0f;

        // 2. Animate Scale (Expand from center to full range)
        // We multiply by 2 because Range is a radius (Diameter = Range * 2)
        float currentScale = Mathf.Lerp(0.1f, _currentRange * 2f, _timer);
        
        // Assuming the visual is a flattened cylinder/sphere, we scale X and Z
        // We keep Y flat so it looks like a disc on the ground
        _visualMesh.localScale = new Vector3(currentScale, 0.01f, currentScale);

        // 3. Animate Alpha (Fade out as it gets bigger)
        if (_mat != null)
        {
            Color c = _mat.color;
            // Alpha starts high (0.5) and fades to 0
            c.a = Mathf.Lerp(0.4f, 0f, _timer); 
            _mat.color = c;
        }
    }
}