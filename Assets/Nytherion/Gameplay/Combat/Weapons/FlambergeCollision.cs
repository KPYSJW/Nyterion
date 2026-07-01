using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class FlambergeCollision : MonoBehaviour
    {
        [HideInInspector] public float damage;
        public GameObject hitEffectPrefab;
        
        [SerializeField] private string poolTag = "Flamberge_Slash_Effect";

        private void Awake()
        {
            poolTag = gameObject.name.Replace("(Clone)", "").Trim();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Enemy"))
            {
                IDamageable target = collision.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(damage);
                    
                    Vector2 hitPoint = collision.ClosestPoint(transform.position);
                    WeaponEffectHelper.PlayHitEffect(hitEffectPrefab, hitPoint);
                }
            }
        }

        // 애니메이션 이벤트 혹은 타이머에서 호출하여 안전하게 풀로 반환합니다.
        public void ReturnToPool()
        {
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
