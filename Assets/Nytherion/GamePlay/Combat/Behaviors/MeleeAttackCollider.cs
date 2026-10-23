using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Characters.Player;
using Unity.VisualScripting;
using UnityEngine;
using Nytherion.GamePlay.Combat;

public class MeleeAttackCollider : MonoBehaviour
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

        hashit = EnemyAttackTarget.TryDamage(other, damage, other.ClosestPoint(transform.position));
    }
}
