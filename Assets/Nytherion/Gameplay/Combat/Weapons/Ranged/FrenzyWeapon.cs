using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class FrenzyWeapon : RangedWeapon, IChargeableWeapon
    {
        [Header("Frenzy Spin-up Settings")]
        [Tooltip("동력장치가 없을 때 풀차징 연사를 유지하는 시간입니다.")]
        [SerializeField, Min(0f)] private float burstDuration = 3f;

        [Header("Frenzy 탄착군 설정")]
        [Tooltip("조준 방향에서 탄환이 좌우로 벗어날 수 있는 최대 각도. 0이면 정확히 직진합니다.")]
        [SerializeField, Min(0f)] private float spreadHalfAngle = 4f;

        private readonly FrenzyAttackState attackState = new FrenzyAttackState();
        private GameObject chargeEffect;
        private GameObject chargeEffectPrefab;
        private ObjectPoolManager chargeEffectPool;
        private float lastProjectileFireTime = 0f;
        private Vector2 lastAimDirection = Vector2.right;

        public bool IsCharging => attackState.IsCharging;
        public float ChargePercent => attackState.ChargePercent;
        public bool IsFiring => attackState.IsFiring;
        public override bool AllowHeldAttackRetry => true;

        protected override Vector2 GetProjectileDirection(Vector2 direction)
        {
            // 추가 연사 탄환에도 매번 별도의 편차를 적용한다.
            float halfAngle = Mathf.Max(0f, spreadHalfAngle);
            if (halfAngle > 0f)
            {
                float angleOffset = Random.Range(-halfAngle, halfAngle);
                direction = Quaternion.AngleAxis(angleOffset, Vector3.forward) * direction;
            }

            return direction;
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (firePoint == null || weaponData == null || !CanAttack()) return;
            lastAimDirection = direction;
            attackState.Begin(Time.time);
            if (attackState.IsFiring) lastProjectileFireTime = float.NegativeInfinity;
            AdvanceAttack(Time.time);
        }

        public override void AttackEnd()
        {
            if (!attackState.IsFiring) AdvanceAttack(Time.time);
            bool wasFiring = attackState.IsFiring;
            bool singleShot = attackState.Release(Time.time, burstDuration, HasPowerDevice());
            if (!attackState.IsFiring)
            {
                StopAllCoroutines();
                damageMultiplier = 1f;
            }
            UpdateChargeEffect();
            if (singleShot) FireShot(false);
            else if (!wasFiring && attackState.IsFiring) FireShot(true);
        }

        private void Update() => AdvanceAttack(Time.time);

        private void AdvanceAttack(float now)
        {
            if (firePoint == null || weaponData == null) return;
            bool wasFiring = attackState.IsFiring;
            float reduction = playerManager != null && playerManager.currentPlayerData != null
                ? playerManager.currentPlayerData.chargeTimeReduction : 0f;
            float chargeTime = Mathf.Max(0f, weaponData.maxChargeTime * (1f - reduction));
            attackState.Advance(now, weaponData.chargeThresholdTime, chargeTime, HasPowerDevice());
            UpdateChargeEffect();
            if (wasFiring && !attackState.IsFiring)
            {
                StopAllCoroutines();
                damageMultiplier = 1f;
            }
            if (attackState.IsFiring && (!wasFiring || now - lastProjectileFireTime >= weaponData.cooldown))
                FireShot(true);
        }

        private void FireShot(bool charged)
        {
            if (firePoint == null || weaponData == null) return;
            lastAimDirection = firePoint.right;
            damageMultiplier = charged && HasChargeRelic() ? 2f : 1f;
            FireProjectiles(lastAimDirection, 1);
            lastProjectileFireTime = Time.time;
            lastAttackTime = Time.time;
        }

        public override bool CanAttack() => attackState.CanBegin && base.CanAttack();

        private bool HasPowerDevice()
        {
            return playerManager != null && playerManager.playerRelicManager != null &&
                playerManager.playerRelicManager.IsRelicActive("PowerDevice");
        }

        private void UpdateChargeEffect()
        {
            if (!attackState.IsCharging)
            {
                HideChargeEffect();
                return;
            }
            if (chargeEffect != null || weaponData == null || weaponData.chargeEffectPrefab == null) return;
            chargeEffectPrefab = weaponData.chargeEffectPrefab;
            chargeEffectPool = ObjectPoolManager.Instance;
            chargeEffect = chargeEffectPool != null
                ? chargeEffectPool.SpawnFromPool(chargeEffectPrefab, transform.position, transform.rotation)
                : Instantiate(chargeEffectPrefab, transform.position, transform.rotation);
            if (chargeEffect == null) return;
            chargeEffect.transform.SetParent(transform, false);
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            Vector3 center = renderer != null ? renderer.localBounds.center : Vector3.zero;
            chargeEffect.transform.localPosition = center + chargeEffectPrefab.transform.localPosition;
            chargeEffect.transform.localRotation = chargeEffectPrefab.transform.localRotation;
            chargeEffect.transform.localScale = chargeEffectPrefab.transform.localScale;
            foreach (SpriteRenderer effectRenderer in chargeEffect.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer == null) break;
                effectRenderer.sortingLayerID = renderer.sortingLayerID;
                effectRenderer.sortingOrder = renderer.sortingOrder + 1;
            }
            foreach (Animator effectAnimator in chargeEffect.GetComponentsInChildren<Animator>())
            {
                effectAnimator.Rebind();
                effectAnimator.Play(0, 0, 0f);
                effectAnimator.Update(0f);
            }
        }

        private void HideChargeEffect()
        {
            if (chargeEffect != null)
            {
                if (chargeEffectPool != null) chargeEffectPool.ReturnToPool(chargeEffectPrefab.name, chargeEffect);
                else Destroy(chargeEffect);
            }
            chargeEffect = null;
            chargeEffectPrefab = null;
            chargeEffectPool = null;
        }

        protected override bool ShouldSpawnFireEffect()
        {
            // 발사 묶음이 아닌 실제 탄환 스폰 시점에 재생하여 추가 연사 탄에도 맞춥니다.
            return false;
        }

        protected override void ConfigureSpawnedProjectile(GameObject projectile, Vector2 direction, float chargePercent)
        {
            base.ConfigureSpawnedProjectile(projectile, direction, chargePercent);
            if (firePoint == null || weaponData == null || weaponData.fireEffectPrefab == null) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            WeaponVFXHelper.PlayFireEffect(weaponData.fireEffectPrefab, firePoint.position,
                Quaternion.AngleAxis(angle, Vector3.forward), firePoint);
        }

        protected override bool ShouldPlayFireAnimation()
        {
            // 매 발사 시 캐릭터/무기 애니메이션 트리거가 튀는 것 방지
            return false;
        }

        private bool HasChargeRelic()
        {
            return playerManager != null &&
                   playerManager.playerRelicManager != null &&
                   playerManager.playerRelicManager.IsRelicActive("ManaCharger");
        }

        private void ResetFrenzyState()
        {
            attackState.Reset();
            HideChargeEffect();
            damageMultiplier = 1f;
            // 무기를 비활성화하면 대기 중인 추가 연사도 중단합니다.
            StopAllCoroutines();
        }

        private void OnDisable()
        {
            ResetFrenzyState();
        }
    }
}
