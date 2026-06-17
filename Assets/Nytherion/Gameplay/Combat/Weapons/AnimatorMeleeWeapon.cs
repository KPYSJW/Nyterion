using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class AnimatorMeleeWeapon : MeleeWeapon
    {
        [Header("Animator Settings")]
        [Tooltip("무기 프리팹에 부착된 Animator 컴포넌트")]
        [SerializeField] private Animator animator;
        
        [Tooltip("공격 시 작동시킬 트리거 파라미터 이름")]
        [SerializeField] private string attackTriggerName = "Attack";

        public override void Start()
        {
            base.Start();
            
            // 프리팹 루트 또는 자식에서 Animator를 탐색하여 자동으로 할당합니다.
            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                {
                    animator = GetComponentInChildren<Animator>();
                }
            }
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack())
            {
                return;
            }

            // Animator의 Attack 트리거를 작동시킵니다.
            if (animator != null)
            {
                animator.SetTrigger(attackTriggerName);
            }

            lastAttackTime = Time.time;
        }

        public override void AttackEnd()
        {
            // 필요 시 추가적인 공격 종료 로직을 작성합니다.
        }
    }
}
