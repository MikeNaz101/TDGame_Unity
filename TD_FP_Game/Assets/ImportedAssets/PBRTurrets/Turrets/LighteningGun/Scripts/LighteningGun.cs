using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class LighteningGun : MonoBehaviour {
   
    Transform go_target;

    // Rotating Blades
    public Transform go_OuterBlade;
    public Transform go_InnerBlade;

    public float currentRotationSpeed;


    // Distance the turret can aim and fire from
    public float firingRange;

    // Particle system for the muzzel flash
    public ParticleSystem muzzelFlash;
    

    // Used to start and stop the turret firing
    bool canFire = false;


    void Start()
    {
        // Set the firing range distance
        this.GetComponent<SphereCollider>().radius = firingRange;

      
    }

    void Update()
    {
        AimAndFire();    
        go_InnerBlade.transform.Rotate(0, currentRotationSpeed * Time.deltaTime * -1, 0);
        go_OuterBlade.transform.Rotate(0, currentRotationSpeed * Time.deltaTime, 0);
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
            canFire = true;
        }

    }
    // Stop firing
    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "Enemy")
        {
            canFire = false;
        }
    }

    void AimAndFire()
    {
        // if can fire turret activates
        if (canFire)
        {
            
            // aim at enemy
            Vector3 baseTargetPostition = new Vector3(go_target.position.x, this.transform.position.y, go_target.position.z);
            Vector3 gunBodyTargetPostition = new Vector3(go_target.position.x, go_target.position.y, go_target.position.z);

            muzzelFlash.transform.LookAt(gunBodyTargetPostition);

            // start particle system 
            if (!muzzelFlash.isPlaying)
            {
                muzzelFlash.Play();
            }

            //Distance to enemy
            float dist = Vector3.Distance(go_target.position, transform.position);

            if (muzzelFlash != null)
            {
                var main = muzzelFlash.main;
                //main.startSizeZ = dist;
                main.startLifetime = dist / 15;
            }

        }
        else
        {
            // stop the particle system
            if (muzzelFlash.isPlaying)
            {
                muzzelFlash.Stop();
            }
        }
    }
}