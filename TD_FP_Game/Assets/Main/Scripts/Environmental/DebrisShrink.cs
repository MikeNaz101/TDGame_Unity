using System.Collections;
using UnityEngine;

/// <summary>Lets a debris chunk sit for a while, then shrinks it away and destroys it.</summary>
public class DebrisShrink : MonoBehaviour
{
    public float lifetime = 4f;
    public float shrinkTime = 0.6f;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, lifetime - shrinkTime));

        Vector3 startScale = transform.localScale;
        float t = 0f;
        while (t < shrinkTime)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t / shrinkTime);
            yield return null;
        }
        Destroy(gameObject);
    }
}
