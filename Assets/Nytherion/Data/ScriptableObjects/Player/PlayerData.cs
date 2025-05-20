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
        public float dashCooldown;
    }
}