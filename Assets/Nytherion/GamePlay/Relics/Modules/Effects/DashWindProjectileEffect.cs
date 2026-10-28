using System;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable, RelicDisplayName("대시 돌풍 투사체")]
    public sealed class DashWindProjectileEffect : RelicEffectBase
    {
        public WindBlastProjectile projectilePrefab;
        [Min(0f)] public float damageRatio = 0.75f;
        // 이전 에셋의 직렬화 값을 보존하며 대시 충전은 사용하지 않습니다.
        [HideInInspector] public int chargesPerDash = 1;
        [Min(0.1f), InspectorName("대시 대상 탐색 반경")] public float targetSearchRadius = 8f;
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

    /// <summary>돌풍과 초월 효과의 대시 발사를 통합하고 플레이어의 추가 투사체 수를 반영합니다.</summary>
    public sealed class DashStormRuntime : MonoBehaviour
    {
        private readonly HashSet<DashWindProjectileEffect> windEffects = new HashSet<DashWindProjectileEffect>();
        private readonly HashSet<DashChainLightningEffect> stormEffects = new HashSet<DashChainLightningEffect>();
        private PlayerManager player;
        private PlayerCombat combat;
        private EventManager events;
        private bool connected;

        public static DashStormRuntime GetOrCreate(PlayerManager player)
        {
            DashStormRuntime runtime = player.GetComponent<DashStormRuntime>();
            if (runtime == null) runtime = player.gameObject.AddComponent<DashStormRuntime>();
            runtime.player = player;
            return runtime;
        }

        public void AddWind(DashWindProjectileEffect effect) { windEffects.Add(effect); Connect(); }
        public void AddStorm(DashChainLightningEffect effect) { stormEffects.Add(effect); Connect(); }
        public void RemoveWind(DashWindProjectileEffect effect) { windEffects.Remove(effect); DisconnectIfEmpty(); }
        public void RemoveStorm(DashChainLightningEffect effect)
        {
            stormEffects.Remove(effect);
            DisconnectIfEmpty();
        }

        private void Connect()
        {
            if (connected) return;
            combat = player.PlayerCombat != null ? player.PlayerCombat : player.GetComponent<PlayerCombat>();
            events = player.EventManager;
            if (events == null) return;
            events.OnPlayerDashStarted += HandleDash;
            events.OnEnemyDamagedByPlayerDetailed += HandleHit;
            connected = true;
        }

        private void DisconnectIfEmpty()
        {
            if (windEffects.Count == 0 && stormEffects.Count == 0) Disconnect();
        }

        private void Disconnect()
        {
            if (events != null)
            {
                events.OnPlayerDashStarted -= HandleDash;
                events.OnEnemyDamagedByPlayerDetailed -= HandleHit;
            }
            connected = false;
        }

        private void HandleDash()
        {
            if (!isActiveAndEnabled || player == null) return;
            DashChainLightningEffect storm = GetActiveStorm();
            DashWindProjectileEffect wind = null;
            foreach (DashWindProjectileEffect effect in windEffects)
                if (wind == null || effect.damageRatio > wind.damageRatio) wind = effect;
            if (storm == null && wind == null) return;
            WindBlastProjectile prefab = storm != null && storm.projectilePrefab != null
                ? storm.projectilePrefab : wind != null ? wind.projectilePrefab : null;
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            WeaponBase weapon = combat != null ? combat.currentWeapon : null;
            if (prefab == null || pool == null || weapon == null || weapon.weaponData == null) return;
            float radius = Mathf.Max(storm != null ? storm.dashTargetSearchRadius : 0f,
                wind != null ? wind.targetSearchRadius : 0f);
            Vector3 position = player.transform.position;
            Vector2 direction = GetDashDirection(position, radius);
            float ratio = Mathf.Max(storm != null ? storm.projectileDamageRatio : 0f, wind != null ? wind.damageRatio : 0f);
            int extraCount = player.currentPlayerData != null
                ? Mathf.Max(0, Mathf.FloorToInt(player.currentPlayerData.extraProjectiles)) : 0;
            int count = 1 + extraCount;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            const float spreadAngle = 15f;
            float angleStep = count > 1 ? spreadAngle / (count - 1) : 0f;
            float startOffset = count > 1 ? -spreadAngle * 0.5f : 0f;
            float damage = weapon.weaponData.damage * weapon.CurrentDamageMultiplier * Mathf.Max(0f, ratio);
            for (int i = 0; i < count; i++)
            {
                float angle = (baseAngle + startOffset + angleStep * i) * Mathf.Deg2Rad;
                Vector2 shotDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                GameObject instance = pool.SpawnFromPool(prefab.gameObject, position, Quaternion.identity);
                if (instance == null) continue;
                instance.GetComponent<WindBlastProjectile>().Launch(pool, shotDirection, damage,
                    weapon.EffectSizeMultiplier, 0f, 0f, 0, null, 0f, this);
            }
        }

        private static Vector2 GetDashDirection(Vector3 position, float radius)
        {
            EnemyBase nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Collider2D hit in Physics2D.OverlapCircleAll(position, Mathf.Max(0.1f, radius)))
            {
                EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.isDead) continue;
                float distance = ((Vector2)(enemy.transform.position - position)).sqrMagnitude;
                if (distance < 0.0001f || distance >= nearestDistance) continue;
                nearest = enemy;
                nearestDistance = distance;
            }
            return nearest != null ? ((Vector2)(nearest.transform.position - position)).normalized : RandomDirection();
        }

        private static Vector2 RandomDirection()
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private void HandleHit(PlayerDamageEventData data)
        {
            if (!isActiveAndEnabled || data.IsChainDamage || data.IsDamageOverTime ||
                data.Target == null) return;
            // 번개는 대시 충전과 무관하게 일반 적중마다 판정합니다.
            DashChainLightningEffect effect = GetActiveStorm();
            if (effect == null || !RollLightning(effect.triggerChance)) return;
            SpreadLightning(data.Target, data.DamageAmount * Mathf.Max(0f, effect.damageRatio), effect.searchRadius, effect.maxTargets,
                effect.lightningEffectPrefab, (enemy, position, damage) =>
                    SpawnLightningWind(enemy, position, damage, effect.projectilePrefab, effect.projectileDamageRatio, effect.windSpawnChance));
        }

        public void SpawnLightningWind(EnemyBase source, Vector3 position, float lightningDamage,
            WindBlastProjectile prefab, float damageRatio, float spawnChance)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (!isActiveAndEnabled || player == null || prefab == null || pool == null || !RollLightning(spawnChance)) return;
            Vector2 direction = RandomDirection();
            GameObject instance = pool.SpawnFromPool(prefab.gameObject, position, Quaternion.identity);
            if (instance == null) return;
            PlayerCombat playerCombat = player.PlayerCombat != null ? player.PlayerCombat : player.GetComponent<PlayerCombat>();
            WeaponBase weapon = playerCombat != null ? playerCombat.currentWeapon : null;
            instance.GetComponent<WindBlastProjectile>().Launch(pool, direction,
                lightningDamage * Mathf.Max(0f, damageRatio), weapon != null ? weapon.EffectSizeMultiplier : 1f,
                0f, 0f, 0, null, 0f, this, source);
        }

        public void TryProjectileLightning(EnemyBase enemy, float damage)
        {
            if (!isActiveAndEnabled) return;
            DashChainLightningEffect storm = GetActiveStorm();
            if (storm == null || !RollLightning(storm.triggerChance)) return;
            SpreadLightning(enemy, damage * Mathf.Max(0f, storm.damageRatio), storm.searchRadius, storm.maxTargets,
                storm.lightningEffectPrefab, (target, position, lightningDamage) =>
                    SpawnLightningWind(target, position, lightningDamage, storm.projectilePrefab, storm.projectileDamageRatio, storm.windSpawnChance));
        }

        private DashChainLightningEffect GetActiveStorm()
        {
            DashChainLightningEffect storm = null;
            foreach (DashChainLightningEffect effect in stormEffects)
                if (storm == null || effect.damageRatio > storm.damageRatio) storm = effect;
            return storm;
        }

        public static bool RollLightning(float chance) => chance >= 1f ||
            (chance > 0f && UnityEngine.Random.value < chance);

        public static void SpreadLightning(EnemyBase origin, float damage, float radius, int maxTargets,
            ChainLightningVFX lightningPrefab = null, Action<EnemyBase, Vector3, float> onLightningHit = null)
        {
            if (origin == null || maxTargets <= 0) return;
            // 기본 피해가 0인 무기도 충전 적중의 감전과 번개 연출은 발동합니다.
            if (!origin.isDead && origin.TryGetComponent(out StatusEffectManager originStatus))
                originStatus.ApplyEffect(new LightningEffect(3f));
            Vector3 start = origin.transform.position;
            // 최초 번개 피격 대상도 판정하며 원래 공격 피해를 중복 적용하지 않습니다.
            onLightningHit?.Invoke(origin, start, damage);
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
                Vector3 position = enemy.transform.position;
                if (damage > 0f) enemy.TakeDamage(damage, true);
                // 피해 0인 번개도 피격 연출과 동일한 대상에서 돌풍 확률을 판정합니다.
                onLightningHit?.Invoke(enemy, position, damage);
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

        private void OnDestroy() => Disconnect();
    }
}
