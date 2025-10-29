using UnityEngine;
using UnityEngine.AI;

public class SecurityLasers : MonoBehaviour
{
    public Transform laserStart;
    public float laserRange = 10f;
    public LayerMask detectionLayer;
    public Color laserColor = Color.red;
    public float laserWidth = 0.05f;
    //public AudioClip alarmSound;
    public Light environmentLight;
    public Color alarmLightColor = Color.red;
    public EnemyController enemyController;

    private LineRenderer lineRenderer;
    private bool alarmTriggered = false;
    public AudioSource audioSource;
    private Color originalLightColor;

    void Start()
    {
        // Setup LineRenderer
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = laserWidth;
        lineRenderer.endWidth = laserWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = laserColor;
        lineRenderer.endColor = laserColor;
    }

    void Update()
    {
        // Cast the ray forward from the laserStart position
        RaycastHit hit;
        Vector3 laserEnd = laserStart.position + laserStart.forward * laserRange;

        if (Physics.Raycast(laserStart.position, laserStart.forward, out hit, laserRange, detectionLayer))
        {
            laserEnd = hit.point; // Stop the laser where it collides

            if (!alarmTriggered && hit.collider.CompareTag("Player"))
            {
                TriggerAlarm(laserStart.position);
            }
        }

        // Update LineRenderer positions
        lineRenderer.SetPosition(0, laserStart.position);
        lineRenderer.SetPosition(1, laserEnd);
    }

    void TriggerAlarm(Vector3 alarmPosition)
    {
        alarmTriggered = true;
        Debug.Log("ALARM! Player detected!");

        if (audioSource) audioSource.Play();

        if (environmentLight) environmentLight.color = alarmLightColor;

        if (enemyController)
        {
            enemyController.GoToAlarm(alarmPosition, this);
        }
    }

    public void ResetAlarm()
    {
        Debug.Log("Alarm Reset!");
        alarmTriggered = false;

        if (audioSource) audioSource.Stop();
        if (environmentLight) environmentLight.color = originalLightColor;
    }
}
