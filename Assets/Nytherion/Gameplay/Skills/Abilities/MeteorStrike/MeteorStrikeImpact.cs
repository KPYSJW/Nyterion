using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>배정된 적을 추적하며 낙하하고, 폭발 프레임에서 해당 적에게 한 번 피해를 줍니다.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class MeteorStrikeImpact : MonoBehaviour
    {
        private Animator animator;
        private EnemyBase target;
        private float damage;
        private float fallSpeed;
        private float yOffset;
        private float expiresAt;
        private float remainingHeight;
        private bool falling;
        private bool damageApplied;

        private void Awake() => animator = GetComponent<Animator>();

        public void Begin(MeteorStrikeSkillData data, EnemyBase enemy, float strikeDamage, float size)
        {
            target = enemy;
            damage = strikeDamage;
            fallSpeed = Mathf.Max(0.01f, data.fallSpeed);
            float scale = Mathf.Max(0.01f, size);
            transform.localScale = data.impactPrefab.transform.localScale * scale;
            yOffset = data.explosionYOffset * scale;
            falling = true;
            damageApplied = false;
            remainingHeight = Mathf.Max(0.01f, data.dropHeight);
            transform.position = GetTargetPosition() + Vector3.up * remainingHeight;
            gameObject.SetActive(true);
            if (animator == null) animator = GetComponent<Animator>();
            animator.Rebind();
            animator.Update(0f);
        }

        private Vector3 GetTargetPosition() => target.TryGetGroundHitBounds(out Bounds bounds)
            ? bounds.center : target.transform.position;

        private void Update()
        {
            if (!falling)
            {
                // 애니메이션 이벤트가 누락돼도 로컬 풀로 반환합니다.
                if (Time.time >= expiresAt) DisableProjectile();
                return;
            }
            if (target == null || !target.isActiveAndEnabled || target.isDead)
            {
                DisableProjectile();
                return;
            }

            Vector3 destination = GetTargetPosition();
            // 수평 이동을 따라가되 수직 낙하 거리는 매 프레임 줄여 이동하는 적도 타격합니다.
            remainingHeight = Mathf.Max(0f, remainingHeight - fallSpeed * Time.deltaTime);
            transform.position = destination + Vector3.up * remainingHeight;
            if (remainingHeight > 0f) return;
            falling = false;
            transform.position = destination - Vector3.up * yOffset;
            expiresAt = Time.time + 2f;
            animator.SetTrigger("Explode");
        }

        // 기존 운석 애니메이션의 이벤트 이름을 유지합니다.
        public void ApplyDamage()
        {
            if (falling || damageApplied) return;
            damageApplied = true;
            if (target != null && target.isActiveAndEnabled && !target.isDead)
                target.TakeDamage(damage);
        }

        public void DisableProjectile() => gameObject.SetActive(false);

        private void OnDisable()
        {
            target = null;
            falling = false;
            damageApplied = true;
        }
    }
}
