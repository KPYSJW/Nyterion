using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Combat.Weapons;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable, RelicDisplayName("대시 후 돌풍 투사체")]
    public sealed class DashWindProjectileEffect : RelicEffectBase
    {
        public WindBlastProjectile projectilePrefab;
        [Min(0f)] public float damageRatio = 0.75f;
        [Min(1)] public int chargesPerDash = 1;
        [NonSerialized] private DashStormRuntime runtime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;
            runtime = DashStormRuntime.GetOrCreate(playerManager);
            runtime.AddWind(this);
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (runtime != null) runtime.RemoveWind(this);
            runtime = null;
        }
    }

    /// <summary>돌풍과 초월 효과가 함께 활성화돼도 공격당 하나의 돌풍만 발사합니다.</summary>
    public sealed class DashStormRuntime : MonoBehaviour
    {
        private readonly Dictionary<DashWindProjectileEffect, int> windCharges = new Dictionary<DashWindProjectileEffect, int>();
        private readonly Dictionary<DashChainLightningEffect, int> stormCharges = new Dictionary<DashChainLightningEffect, int>();
        private PlayerManager player;
        private PlayerCombat combat;
        private EventManager events;
        private bool connected;
        private readonly Queue<StormHit> pendingHits = new Queue<StormHit>();
        private struct StormHit
        {
            public DashChainLightningEffect Effect;
            public float Deadline;
        }

        public int RemainingStormCharges
        {
            get
            {
                int result = 0;
                foreach (int charges in stormCharges.Values) result = Mathf.Max(result, charges);
                return result;
            }
        }

        public static DashStormRuntime GetOrCreate(PlayerManager player)
        {
            DashStormRuntime runtime = player.GetComponent<DashStormRuntime>();
            if (runtime == null) runtime = player.gameObject.AddComponent<DashStormRuntime>();
            runtime.player = player;
            return runtime;
        }

        public void AddWind(DashWindProjectileEffect effect) { windCharges[effect] = 0; Connect(); }
        public void AddStorm(DashChainLightningEffect effect) { stormCharges[effect] = 0; Connect(); }
        public void RemoveWind(DashWindProjectileEffect effect) { windCharges.Remove(effect); DisconnectIfEmpty(); }
        public void RemoveStorm(DashChainLightningEffect effect)
        {
            stormCharges.Remove(effect);
            DisconnectIfEmpty();
        }

        private void Connect()
        {
            if (connected) return;
            combat = player.PlayerCombat != null ? player.PlayerCombat : player.GetComponent<PlayerCombat>();
            events = player.EventManager;
            if (combat == null || events == null) return;
            events.OnPlayerDashStarted += Recharge;
            events.OnEnemyDamagedByPlayerDetailed += HandleHit;
            events.OnPlayerRangedAttack += HandleChargedShot;
            combat.OnPlayerAttackStarted += HandleAttack;
            connected = true;
        }

        private void DisconnectIfEmpty()
        {
            if (windCharges.Count == 0 && stormCharges.Count == 0) Disconnect();
        }

        private void Disconnect()
        {
            if (events != null)
            {
                events.OnPlayerDashStarted -= Recharge;
                events.OnEnemyDamagedByPlayerDetailed -= HandleHit;
                events.OnPlayerRangedAttack -= HandleChargedShot;
            }
            if (combat != null) combat.OnPlayerAttackStarted -= HandleAttack;
            pendingHits.Clear();
            connected = false;
        }

        private void Recharge()
        {
            if (!isActiveAndEnabled) return;
            // 재대시는 최대 횟수로 갱신하며 무한 누적하지 않습니다.
            foreach (DashWindProjectileEffect effect in new List<DashWindProjectileEffect>(windCharges.Keys))
                windCharges[effect] = Mathf.Max(1, effect.chargesPerDash);
            foreach (DashChainLightningEffect effect in new List<DashChainLightningEffect>(stormCharges.Keys))
                stormCharges[effect] = Mathf.Max(1, effect.chargesPerDash);
        }

        private void HandleAttack(Vector2 direction, Vector3 target)
        {
            // 차징·스핀업 시작에는 소모하지 않고 실제 발사 이벤트에서 처리합니다.
            if (combat.currentWeapon is ChargeableRangedWeapon || combat.currentWeapon is FrenzyWeapon) return;
            ActivateAttack(direction, -1f);
        }

        private void HandleChargedShot(Vector2 direction, int count, float damage, Transform firePoint, string tag)
        {
            if (combat.currentWeapon is ChargeableRangedWeapon || combat.currentWeapon is FrenzyWeapon)
                ActivateAttack(direction, damage);
        }

        private void ActivateAttack(Vector2 direction, float shotDamage)
        {
            if (!isActiveAndEnabled || direction.sqrMagnitude < 0.0001f) return;
            DashChainLightningEffect storm = null;
            DashWindProjectileEffect wind = null;
            foreach (var pair in stormCharges)
                if (pair.Value > 0 && (storm == null || pair.Key.damageRatio > storm.damageRatio)) storm = pair.Key;
            foreach (var pair in windCharges)
                if (pair.Value > 0 && (wind == null || pair.Key.damageRatio > wind.damageRatio)) wind = pair.Key;
            if (storm == null && wind == null) return;
            // 적중 여부와 투사체 수에 관계없이 공격 한 번에 충전 하나를 사용합니다.
            foreach (DashChainLightningEffect effect in new List<DashChainLightningEffect>(stormCharges.Keys))
                stormCharges[effect] = Mathf.Max(0, stormCharges[effect] - 1);
            foreach (DashWindProjectileEffect effect in new List<DashWindProjectileEffect>(windCharges.Keys))
                windCharges[effect] = Mathf.Max(0, windCharges[effect] - 1);
            // 연속 사격 뒤 앞서 발사한 탄이 늦게 적중해도 충전 공격의 효과를 보존합니다.
            PrunePendingHits();
            if (storm != null) pendingHits.Enqueue(new StormHit
            {
                Effect = storm,
                Deadline = Time.time + Mathf.Max(0.1f, storm.hitWaitDuration)
            });

            WindBlastProjectile prefab = storm != null ? storm.projectilePrefab : wind.projectilePrefab;
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            WeaponBase weapon = combat.currentWeapon;
            if (prefab == null || pool == null || weapon == null || weapon.weaponData == null) return;
            float ratio = Mathf.Max(storm != null ? storm.projectileDamageRatio : 0f, wind != null ? wind.damageRatio : 0f);
            Vector3 position = player.transform.position + (Vector3)direction.normalized * 0.4f;
            GameObject instance = pool.SpawnFromPool(prefab.gameObject, position, Quaternion.identity);
            if (instance == null) return;
            instance.GetComponent<WindBlastProjectile>().Launch(pool, direction,
                (shotDamage >= 0f ? shotDamage : weapon.weaponData.damage * weapon.CurrentDamageMultiplier) * Mathf.Max(0f, ratio),
                weapon.EffectSizeMultiplier, storm != null ? storm.damageRatio : 0f,
                storm != null ? storm.searchRadius : 0f, storm != null ? storm.maxTargets : 0,
                storm != null ? storm.lightningEffectPrefab : null);
        }

        private void HandleHit(PlayerDamageEventData data)
        {
            if (!isActiveAndEnabled || data.IsChainDamage || data.IsDamageOverTime ||
                data.Target == null) return;
            PrunePendingHits();
            if (pendingHits.Count == 0) return;
            DashChainLightningEffect effect = pendingHits.Dequeue().Effect;
            SpreadLightning(data.Target, data.DamageAmount * Mathf.Max(0f, effect.damageRatio), effect.searchRadius, effect.maxTargets,
                effect.lightningEffectPrefab);
        }

        private void PrunePendingHits()
        {
            while (pendingHits.Count > 0 &&
                (Time.time > pendingHits.Peek().Deadline || !stormCharges.ContainsKey(pendingHits.Peek().Effect)))
                pendingHits.Dequeue();
        }

        public static void SpreadLightning(EnemyBase origin, float damage, float radius, int maxTargets,
            ChainLightningVFX lightningPrefab = null)
        {
            if (origin == null || maxTargets <= 0) return;
            // 기본 피해가 0인 무기도 충전 적중의 감전과 번개 연출은 발동합니다.
            if (!origin.isDead && origin.TryGetComponent(out StatusEffectManager originStatus))
                originStatus.ApplyEffect(new LightningEffect(3f));
            Vector3 start = origin.transform.position;
            List<Transform> lightningAnchors = new List<Transform>();
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin.transform.position, Mathf.Max(0.1f, radius));
            Array.Sort(hits, (a, b) => ((Vector2)(a.transform.position - origin.transform.position)).sqrMagnitude
                .CompareTo(((Vector2)(b.transform.position - origin.transform.position)).sqrMagnitude));
            HashSet<EnemyBase> targets = new HashSet<EnemyBase>();
            foreach (Collider2D hit in hits)
            {
                EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy == origin || enemy.isDead || !targets.Add(enemy)) continue;
                lightningAnchors.Add(enemy.transform);
                if (damage > 0f) enemy.TakeDamage(damage, true);
                if (!enemy.isDead && enemy.TryGetComponent(out StatusEffectManager status)) status.ApplyEffect(new LightningEffect(3f));
                if (targets.Count >= maxTargets) break;
            }
            if (lightningPrefab != null && (lightningAnchors.Count > 0 || lightningPrefab.HasImpactEffect))
            {
                // 번개부르미와 동일한 생성·앵커 추적·종료 경로로 재생합니다.
                ChainLightningVFX effect = UnityEngine.Object.Instantiate(lightningPrefab, start, Quaternion.identity);
                effect.name = "ChainStormLightningVFX";
                effect.Setup(origin.transform, lightningAnchors);
                if (Application.isPlaying) UnityEngine.Object.Destroy(effect.gameObject, effect.duration + 0.1f);
            }
        }

        private void OnDisable()
        {
            pendingHits.Clear();
            foreach (DashWindProjectileEffect effect in new List<DashWindProjectileEffect>(windCharges.Keys)) windCharges[effect] = 0;
            foreach (DashChainLightningEffect effect in new List<DashChainLightningEffect>(stormCharges.Keys)) stormCharges[effect] = 0;
        }

        private void OnDestroy() => Disconnect();
    }
}
