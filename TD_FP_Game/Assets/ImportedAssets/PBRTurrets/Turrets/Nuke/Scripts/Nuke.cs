using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Nuke : MonoBehaviour {
    
    public Transform enemy;
    // deside between heat seeking or standard rocket
    bool isHeatSeeking;
    // speed of rocket
    public float speed;
    float lerpedSpeed;
    //amount of time in seconds the missile will last for
    public float life;

    public ParticleSystem particleSmoke;

    public GameObject rocketMesh;

    bool canDestroyMissile = true;

    public GameObject explosion;

    // starting value for the Lerp
    private static float t = 0.0f;


    void Start () {
        enemy = GameObject.FindWithTag("Enemy").transform;
        StartCoroutine (HeatSeaking());
    }
	
	
	void Update () {

        life -= Time.deltaTime;

        // Destroy rocket after life time is 0
        if(life < 0)
        {
            DestroyRocket();
        }
        
        // decide if the rocket will follow the target
        if (isHeatSeeking)
        {
            transform.LookAt(enemy.transform);

            

        }

        lerpedSpeed = Mathf.Lerp(0, speed, t);
        t += 0.2f * Time.deltaTime;

        // rocket speed
        transform.Translate(0, 0, lerpedSpeed * Time.deltaTime);
	}

    // Detect an Enemy hit
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Enemy")
        {
            DestroyRocket();
        }

    }

    void DestroyRocket()
    {
        if (canDestroyMissile)
        {
            canDestroyMissile = false;

            isHeatSeeking = false;
            Instantiate(explosion, transform.position, Quaternion.identity);

            // Remove rocket mesh and particle flame
            Destroy(rocketMesh, 0);
            // Stop smoke emitter but alow particle system to play through(fade out)
            particleSmoke.GetComponent<ParticleSystem>().Stop();
            // Remove entire rocket from the scene
            Destroy(gameObject, 3);

        }
       
    }

    IEnumerator HeatSeaking()
    {
        yield return new WaitForSeconds(5);
        isHeatSeeking = true;
    }
}
