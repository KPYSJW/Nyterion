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
        [InspectorName("효과 범위 증가")]
        [Tooltip("플레이어 공격의 크기와 피해 판정 배율입니다. 1이면 기본 크기입니다.")]
        public float projectileSizeMultiplier = 1f;
        public float lifesteal;
        public float chargeTimeReduction;
        public float critChance = 0.1f;
        public float critDamageMultiplier = 1.5f;
    }
}
