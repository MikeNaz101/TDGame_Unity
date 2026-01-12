using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RailGun : MonoBehaviour
{
    // target the gun will aim at
    Transform go_target;

    // Gameobjects need to control rotation and aiming
    public Transform go_baseRotation;
    public Transform go_GunBody;
    public Transform go_barrel;

    string animationName;

    // Distance the turret can aim and fire from
    public float firingRange;

    // Particle system for the muzzel flash
    public ParticleSystem muzzelFlash;

    // Used to start and stop the turret firing
    bool canFire = false;

    bool canAim = false;

    public float FireRate;   // The number of bullets fired per second
    float lastfired;          // The value of Time.time at the last firing moment

    bool gunIsFiring;


    void Start()
    {
        // Set the firing range distance
        this.GetComponent<SphereCollider>().radius = firingRange;

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
            go_GunBody.transform.LookAt(gunBodyTargetPostition);

            if (Time.time - lastfired > 1 / FireRate)
            {
                muzzelFlash.Play();
                lastfired = Time.time;
                // play the only animation on the barrel
                go_barrel.GetComponent<Animation>().Play();
                
            }
        }
    }
}