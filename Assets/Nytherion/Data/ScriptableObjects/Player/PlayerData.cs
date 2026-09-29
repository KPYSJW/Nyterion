using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Player
{
    [CreateAssetMenu(fileName = "NewPlayerData", menuName = "Data/Player")]
    public class PlayerData : ScriptableObject
    {
        public float maxHealth;
        public float moveSpeed;
        public float meleeDamage;
        public float rangedDamage;
        public float meleeSpeed;
        public float rangedSpeed;
        public float dashSpeed;
        public float dashDuration;
        public float dashDistance;
        public float dashCooldown;
        public float defense;
        public float extraProjectiles;
        [Tooltip("투사체와 폭발의 크기 배율입니다. 1이면 기본 크기입니다.")]
        public float projectileSizeMultiplier = 1f;
        [Tooltip("공격 범위 배율입니다. 1이면 기본 범위입니다.")]
        public float attackRangeMultiplier = 1f;
        public float lifesteal;
        public float chargeTimeReduction;
        public float critChance = 0.1f;
        public float critDamageMultiplier = 1.5f;
    }
}
