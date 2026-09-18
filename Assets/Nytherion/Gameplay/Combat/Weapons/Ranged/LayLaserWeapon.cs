using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat.Weapons;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>버튼을 누르는 동안 4단계로 충전하고 놓는 순간 광선을 발사합니다.</summary>
    public class LayLaserWeapon : RangedWeapon, IChargeableWeapon
    {
        private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int ChargeStateHash = Animator.StringToHash("Base Layer.Charge");
        private static readonly int FullChargeStateHash = Animator.StringToHash("Base Layer.FullCharge");
        private static readonly int ChargingParameterHash = Animator.StringToHash("Charging");
        private static readonly int ChargeSpeedParameterHash = Animator.StringToHash("ChargeSpeed");
        private static readonly int FullChargeSpeedParameterHash = Animator.StringToHash("FullChargeSpeed");

        [SerializeField] private SpriteRenderer weaponRenderer;

        private LayLaserWeaponData data;
        private LayLaserBeam activeBeam;
        private float heldTime;

        public bool IsCharging { get; private set; }
        public bool IsFiring => activeBeam != null && activeBeam.IsFiring;
        public override bool AllowAutoFire => false;
        public override bool AllowHeldAttackRetry => true;
        public float ChargePercent => !IsCharging ? 0f : AdjustedChargeTime <= 0f
            ? 1f : Mathf.Clamp01(heldTime / AdjustedChargeTime);
        public int ChargeStage => data != null ? data.GetChargeStage(ChargePercent) + 1 : 1;
        internal Transform CasterTransform => playerManager != null ? playerManager.transform : transform;
        internal Vector2 CurrentFireDirection
        {
            get
            {
                float scaleSign = transform.lossyScale.y < 0f ? -1f : 1f;
                float rotationOffset = data != null ? data.spriteRotationOffset : 0f;
                Vector2 direction = Quaternion.Euler(0f, 0f, -rotationOffset * scaleSign) * transform.right;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            }
        }

        private float AdjustedChargeTime => data == null ? 0f : Mathf.Max(0f, data.maxChargeTime *
            (1f - (playerManager != null && playerManager.currentPlayerData != null
                ? playerManager.currentPlayerData.chargeTimeReduction : 0f)));

        public override void Initialize(WeaponData weaponData)
        {
            CancelAttack();
            base.Initialize(weaponData);
            data = weaponData as LayLaserWeaponData;
            ConfigureChargeAnimator();
            ResetFirePointPosition();
        }

        public override bool CanAttack()
        {
            return isActiveAndEnabled && data != null && firePoint != null &&
                !IsCharging && !IsFiring && base.CanAttack();
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;
            if (weaponRenderer == null || animator == null || animator.runtimeAnimatorController == null ||
                !data.HasValidFrames || data.projectilePrefab == null ||
                !data.projectilePrefab.TryGetComponent<LayLaserBeam>(out _))
            {
                Debug.LogError("[LayLaserWeapon] 본체 렌더러, 차징 애니메이터와 광선 프리팹 참조를 확인해 주세요.", this);
                return;
            }

            heldTime = 0f;
            IsCharging = true;
            UpdateFirePointForStage(0);
            animator.SetFloat(ChargeSpeedParameterHash,
                LayLaserWeaponData.ChargeAnimationDuration / Mathf.Max(0.01f, AdjustedChargeTime));
            animator.SetFloat(FullChargeSpeedParameterHash,
                data.fullChargeFramesPerSecond / LayLaserWeaponData.ChargeAnimationFramesPerSecond);
            animator.SetBool(ChargingParameterHash, true);
            animator.Play(AdjustedChargeTime <= 0f ? FullChargeStateHash : ChargeStateHash, 0, 0f);
        }

        private void Update()
        {
            AdvanceCharge(Time.deltaTime);
        }

        private void AdvanceCharge(float deltaTime)
        {
            if (!IsCharging || deltaTime <= 0f) return;
            heldTime += deltaTime;
            UpdateFirePointForStage(data.GetChargeStage(ChargePercent));
        }

        public override void AttackEnd()
        {
            if (!IsCharging) return;
            int stage = data.GetChargeStage(ChargePercent);
            IsCharging = false;
            heldTime = 0f;
            UpdateFirePointForStage(stage);
            ReturnToIdleAnimation();

            Vector2 direction = CurrentFireDirection;
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            GameObject instance = pool != null
                ? pool.SpawnFromPool(data.projectilePrefab, firePoint.position, Quaternion.identity, 2)
                : Instantiate(data.projectilePrefab, firePoint.position, Quaternion.identity);
            if (instance == null)
            {
                ResetFirePointPosition();
                return;
            }

            activeBeam = instance.GetComponent<LayLaserBeam>();
            activeBeam.Initialize(this, firePoint, direction, data, stage,
                data.damage * EffectiveDamageMultiplier, pool);
            lastAttackTime = Time.time;
        }

        internal void OnBeamReleased(LayLaserBeam beam)
        {
            if (activeBeam != beam) return;
            activeBeam = null;
            lastAttackTime = Time.time;
            ResetFirePointPosition();
        }

        private void CancelAttack()
        {
            IsCharging = false;
            heldTime = 0f;
            ReturnToIdleAnimation();
            if (activeBeam != null) activeBeam.StopImmediately();
            activeBeam = null;
            ResetFirePointPosition();
        }

        private void ConfigureChargeAnimator()
        {
            if (weaponRenderer == null) weaponRenderer = GetComponent<SpriteRenderer>();
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null || data == null) return;

            if (data.animatorController != null)
                animator.runtimeAnimatorController = data.animatorController;
            if (animator.runtimeAnimatorController == null) return;
            animator.SetFloat(FullChargeSpeedParameterHash,
                data.fullChargeFramesPerSecond / LayLaserWeaponData.ChargeAnimationFramesPerSecond);
            ReturnToIdleAnimation();
        }

        private void ReturnToIdleAnimation()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetBool(ChargingParameterHash, false);
                animator.Play(IdleStateHash, 0, 0f);
            }
            if (weaponRenderer != null && data != null && data.weaponSprite != null)
                weaponRenderer.sprite = data.weaponSprite;
        }

        private void UpdateFirePointForStage(int stage)
        {
            if (firePoint != null && data != null)
                firePoint.localPosition = data.GetFirePointOffset(stage);
        }

        private void ResetFirePointPosition()
        {
            if (firePoint != null && data != null)
                firePoint.localPosition = data.firePointOffset;
        }

        private void OnDisable()
        {
            CancelAttack();
        }
    }
}
