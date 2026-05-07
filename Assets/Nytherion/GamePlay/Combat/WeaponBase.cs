using UnityEngine;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public abstract class WeaponBase : MonoBehaviour
    {
        
        [SerializeField]public WeaponData weaponData;
        
        [Tooltip("마지막 공격 시간 (Time.time 기준)")]
        protected float lastAttackTime;

        public float damageMultiplier = 1.0f;

        protected PlayerManager playerManager;

        protected virtual void Awake()
        {
            playerManager = GetComponentInParent<PlayerManager>();
        }

        public virtual void Initialize(WeaponData data)
        {
            weaponData = data;
            lastAttackTime = -data.cooldown;

            if (data.weaponSprite != null)
            {
                if (TryGetComponent<SpriteRenderer>(out var sr))
                {
                    sr.sprite = data.weaponSprite;
                }
            }
        }

        public virtual bool CanAttack()
        {
            return Time.time - lastAttackTime >= weaponData.cooldown;
        }
        public virtual void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
        }
        public abstract void AttackEnd();
    }
}