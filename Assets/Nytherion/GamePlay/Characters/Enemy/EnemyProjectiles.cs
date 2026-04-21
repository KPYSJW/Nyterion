using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Systems;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyProjectiles : MonoBehaviour
{
     private void OnTriggerEnter2D(Collider2D other) {
            if (other.gameObject.CompareTag(Tags.Player)||other.gameObject.CompareTag(Tags.Wall))
            {
               Destroy(gameObject);
            }
        }
    
}
