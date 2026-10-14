using UnityEngine;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 하이브리드 무기 시스템을 위한 범용 원거리 무기 클래스
    /// 특정 무기 로직 없이 WeaponData에 정의된 설정을 바탕으로 동작
    /// </summary>
    public class GenericRangedWeapon : RangedWeapon
    {
        private PlayerController playerController;
        private Vector3 rightFacingLocalPosition;
        private Vector3 rightFacingLocalScale;
        private float rightFacingRotation;
        private bool facingPoseCached;

        public override bool OverrideRotation => weaponData != null && weaponData.useFixedFacingPose;

        protected override void Awake()
        {
            base.Awake();
            playerController = GetComponentInParent<PlayerController>();
        }

        private void Start()
        {
            if (!OverrideRotation) return;
            CacheRightFacingPose();
            ApplyFacingPose();
        }

        public override void Initialize(WeaponData data)
        {
            base.Initialize(data);
            if (!OverrideRotation) return;
            CacheRightFacingPose();
        }

        private void LateUpdate()
        {
            if (OverrideRotation) ApplyFacingPose();
        }

        private void CacheRightFacingPose()
        {
            // Blazeshade와 동일하게 장착 시의 위치/크기를 기준으로 좌우 자세를 반전합니다.
            rightFacingLocalPosition = transform.localPosition;
            rightFacingLocalScale = transform.localScale;
            rightFacingLocalScale.x = Mathf.Abs(rightFacingLocalScale.x);
            rightFacingRotation = weaponData.spriteRotationOffset;
            facingPoseCached = true;
        }

        private void ApplyFacingPose()
        {
            if (!facingPoseCached) CacheRightFacingPose();
            bool right = playerController == null || playerController.IsFacingRight;
            Vector3 position = rightFacingLocalPosition;
            position.x *= right ? 1f : -1f;
            transform.localPosition = position;
            Vector3 scale = rightFacingLocalScale;
            scale.x *= right ? 1f : -1f;
            transform.localScale = scale;
            transform.localRotation = Quaternion.Euler(0f, 0f, right ? rightFacingRotation : -rightFacingRotation);
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            // RangedWeapon의 FireProjectiles를 호출하여 투사체를 발사
            // 기본 발사 개수는 1개이며, 추가 투사체 각인 등이 있다면 내부 로직에 의해 자동으로 증가
            FireProjectiles(direction, 1);
            
            lastAttackTime = Time.time;
            
        }

        public override void AttackEnd()
        {
        }
    }
}
