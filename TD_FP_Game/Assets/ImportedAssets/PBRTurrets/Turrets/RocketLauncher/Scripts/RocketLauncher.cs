using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocketLauncher : MonoBehaviour
{
    // target the gun will aim at
    Transform go_target;

    // Gameobjects need to control rotation and aiming
    public Transform go_baseRotation;
    public Transform go_barrel;

    public GameObject spawnPointsHolder;
    public GameObject[] spawnpoints;
    public int count;

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
    public Transform barrel;

    void Start()
    {
        // Set the firing range distance
        this.GetComponent<SphereCollider>().radius = firingRange;

        // Load all spawnpoints into an array
        count = 0;
        foreach (Transform i in spawnPointsHolder.transform)
        {
            count++;
        }
        spawnpoints = new GameObject[count];
        count = 0;
       foreach (Transform i in spawnPointsHolder.transform)
       {
           spawnpoints[count] = i.gameObject;
           count++;
       }

    }

    void Update()
    {
        AimAndFire();
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
            canAim = true;
            canFire = true;
            
        }

    }
    // Stop firing
    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "Enemy")
        {
            canAim = false;
            canFire = false;
        }
    }

    void AimAndFire()
    {
        // if can fire turret activates
        if (canAim)
        {
            // aim at enemy
            Vector3 baseTargetPostition = new Vector3(go_target.position.x, this.transform.position.y, go_target.position.z);
            Vector3 gunBodyTargetPostition = new Vector3(go_target.position.x, go_target.position.y, go_target.position.z);

            go_baseRotation.transform.LookAt(baseTargetPostition);
            go_barrel.transform.LookAt(gunBodyTargetPostition);

            if (Time.time - lastfired > 1 / FireRate)
            {
                //muzzelFlash.Play();
                lastfired = Time.time;

                // Randomly choose a barrel to fire rocket from
                int spawnNumber = Random.RandomRange(0, count);
                Instantiate(projectile, spawnpoints[spawnNumber].transform.position, barrel.transform.rotation);
           

            }
        }
    }
}