using UnityEngine;
using System.Collections.Generic;

public class EnemyCommander : MonoBehaviour
{
    [Header("Commander Stats")]
    public MilitaryRank myRank = MilitaryRank.Specialist;
    public float commandRadius = 20f;
    public float updateRate = 1.0f;

    private float _timer;
    private LayerMask _enemyLayer;
    
    // Keep track of everyone we have buffed
    private HashSet<EnemyController> _subordinates = new HashSet<EnemyController>();

    void Start()
    {
        _enemyLayer = LayerMask.GetMask("Default", "Enemy"); 
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            BroadcastOrders();
            _timer = updateRate;
        }
    }

    void BroadcastOrders()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, commandRadius, _enemyLayer);

        foreach (var hit in hits)
        {
            if (hit.gameObject == this.gameObject) continue;

            EnemyController grunt = hit.GetComponent<EnemyController>();
            if (grunt != null)
            {
                // Promote them
                grunt.ReceivePromotion(myRank);
                
                // Add to our list (HashSet handles duplicates automatically)
                _subordinates.Add(grunt);
            }
        }
        
        // Clean up list (remove nulls/dead enemies) periodically
        _subordinates.RemoveWhere(grunt => grunt == null || grunt.gameObject == null);
    }

    // This triggers when the Commander dies
    void OnDestroy()
    {
        foreach (EnemyController grunt in _subordinates)
        {
            if (grunt != null)
            {
                grunt.ApplyCommanderDeathPenalty();
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, commandRadius);
    }
}