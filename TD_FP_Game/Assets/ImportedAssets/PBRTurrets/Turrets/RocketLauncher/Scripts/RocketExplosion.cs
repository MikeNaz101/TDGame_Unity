using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocketExplosion : MonoBehaviour {
    public float countdownTime;
	// Use this for initialization
	void Start () {
        Destroy(gameObject, countdownTime);
    }
}
