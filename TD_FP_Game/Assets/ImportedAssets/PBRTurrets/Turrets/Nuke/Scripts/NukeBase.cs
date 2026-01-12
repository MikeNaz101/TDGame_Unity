using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NukeBase : MonoBehaviour
{
    // target the gun will aim at
    Transform go_target;

    public GameObject spawnPoint;
    string animationName;

    // Distance the turret can aim and fire from
    public float firingRange;

    // Used to start and stop the turret firing
    bool canFire = false;

    bool canAim = false;

    public float FireRate;   // The number of bullets fired per second
    float lastfired;          // The value of Time.time at the last firing moment

    bool gunIsFiring;

    public GameObject projectile;

    void Start()
    {
        // Set the firing range distance
        this.GetComponent<SphereCollider>().radius = firingRange;
    }

    private void Update()
    {
        // nuke fire rate
        if (Time.time - lastfired > 1 / FireRate && canFire)
        {
            lastfired = Time.time;
            StartCoroutine(AimAndFire());
        }

    }
    void OnDrawGizmosSelected()
    {
        // Draw a red sphere at the transform's position to show the firing range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, firingRange);
    }

    // Detect an Enemy, aim and fire
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Enemy")
        {
            go_target = other.transform;
            StartCoroutine(AimAndFire());

        }

    }
    // Stop firing
    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "Enemy")
        {
            StopCoroutine(AimAndFire());
            canFire = false;
        }
    }

    IEnumerator AimAndFire()
    {
        // if can fire turret activates
        this.GetComponent<Animation>().Play("Nuke_Base_Doors");
        yield return new WaitForSeconds(1);
        canFire = true;
        Instantiate(projectile, spawnPoint.transform.position, spawnPoint.transform.rotation);
            
        
    }
}