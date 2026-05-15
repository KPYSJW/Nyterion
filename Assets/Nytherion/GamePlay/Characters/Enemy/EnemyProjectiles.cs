using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Characters.Player;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyProjectiles : MonoBehaviour
{
   private float damage;
   private bool hashit;
   public void Initialize(float damage)
   {
      this.damage=damage;
      hashit=false;
   }
   private void OnTriggerEnter2D(Collider2D other) 
   {
      if(hashit)return;
      if(other.CompareTag(Tags.Player))
      {
         hashit=true;
          if (other.TryGetComponent<PlayerHealth>(out var playerHealth))
            {
                playerHealth.TakeDamage(damage);
            }
      }
      if (other.gameObject.CompareTag(Tags.Player)||other.gameObject.CompareTag(Tags.Wall))
      {
         Destroy(gameObject);
      }
   }
    
}
