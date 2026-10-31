using System;
using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 공격에 냉기를 부여하고, 냉기를 소모해 추가 피해를 발생시킨다.
    /// </summary>
    [Serializable, RelicDisplayName("만년서리 세트 강화")]
    public sealed class FrostSetBonusEffect : RelicEffectBase
    {
        [Min(0f)] public float chillDuration = 5f;
        [Range(0f, 1f)] public float shatterChance = 0.5f;

        [Header("파쇄")]
        public bool enableShatter;
        [Min(0f)] public float shatterDamageRatio = 0.5f;
        [Min(0.1f)] public float shatterRadius = 2.5f;
        [Min(1)] public int maxShatterTargets = 3;

        [Header("파편 연출")]
        public Texture2D shardTexture;
        public IceShotSkillData shardHitEffect;

        [NonSerialized] private FrostSetBonusRuntime appliedRuntime;

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            RemoveEffect(playerManager, level);
            if (playerManager == null) return;

            appliedRuntime = playerManager.GetComponent<FrostSetBonusRuntime>();
            if (appliedRuntime == null)
            {
                appliedRuntime = playerManager.gameObject.AddComponent<FrostSetBonusRuntime>();
            }

            appliedRuntime.SetBonus(this, playerManager, new FrostSetBonusValues
            {
                ChillDuration = chillDuration,
                ShatterChance = shatterChance,
                EnableShatter = enableShatter,
                ShatterDamageRatio = shatterDamageRatio,
                ShatterRadius = shatterRadius,
                MaxShatterTargets = maxShatterTargets,
                ShardTexture = shardTexture,
                ShardHitEffect = shardHitEffect
            });
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            if (appliedRuntime == null && playerManager != null)
            {
                appliedRuntime = playerManager.GetComponent<FrostSetBonusRuntime>();
            }

            appliedRuntime?.RemoveBonus(this);
            appliedRuntime = null;
        }
    }

    public struct FrostSetBonusValues
    {
        public float ChillDuration;
        public float ShatterChance;
        public bool EnableShatter;
        public float ShatterDamageRatio;
        public float ShatterRadius;
        public int MaxShatterTargets;
        public Texture2D ShardTexture;
        public IceShotSkillData ShardHitEffect;
    }

    public sealed class FrostSetBonusRuntime : MonoBehaviour
    {
        private readonly Dictionary<FrostSetBonusEffect, FrostSetBonusValues> bonuses =
            new Dictionary<FrostSetBonusEffect, FrostSetBonusValues>();

        private PlayerManager playerManager;
        private EventManager eventManager;
        private Texture2D shardTexture;
        private IceShotSkillData shardHitEffect;
        private readonly Dictionary<Texture2D, Sprite[]> shardFrameCache = new Dictionary<Texture2D, Sprite[]>();
        private readonly List<SpriteRenderer> shardVisuals = new List<SpriteRenderer>();

        private float ChillDuration { get; set; }
        private float ShatterChance { get; set; }
        private bool EnableShatter { get; set; }
        private float ShatterDamageRatio { get; set; }
        private float ShatterRadius { get; set; }
        private int MaxShatterTargets { get; set; }

        public void SetBonus(
            FrostSetBonusEffect source,
            PlayerManager currentPlayerManager,
            FrostSetBonusValues values)
        {
            if (source == null || currentPlayerManager == null) return;

            bonuses[source] = values;
            ConfigurePlayer(currentPlayerManager);
            Recalculate();
        }

        public void RemoveBonus(FrostSetBonusEffect source)
        {
            if (source == null || !bonuses.Remove(source)) return;
            Recalculate();
        }

        private void ConfigurePlayer(PlayerManager currentPlayerManager)
        {
            playerManager = currentPlayerManager;
            EventManager newEventManager = currentPlayerManager.EventManager;
            if (eventManager == newEventManager) return;

            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerDetailed -= HandleEnemyDamaged;
            }

            eventManager = newEventManager;
            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerDetailed += HandleEnemyDamaged;
            }
        }

        private void Recalculate()
        {
            ChillDuration = 0f;
            ShatterChance = 0f;
            EnableShatter = false;
            ShatterDamageRatio = 0f;
            ShatterRadius = 0f;
            MaxShatterTargets = 0;
            shardTexture = null;
            shardHitEffect = null;

            foreach (FrostSetBonusValues bonus in bonuses.Values)
            {
                ChillDuration = Mathf.Max(ChillDuration, bonus.ChillDuration);
                if (bonus.EnableShatter) ShatterChance = Mathf.Max(ShatterChance, bonus.ShatterChance);
                EnableShatter |= bonus.EnableShatter;
                ShatterDamageRatio = Mathf.Max(ShatterDamageRatio, bonus.ShatterDamageRatio);
                ShatterRadius = Mathf.Max(ShatterRadius, bonus.ShatterRadius);
                MaxShatterTargets = Mathf.Max(MaxShatterTargets, bonus.MaxShatterTargets);
                if (bonus.EnableShatter && bonus.ShardTexture != null && bonus.ShardHitEffect != null)
                {
                    shardTexture = bonus.ShardTexture;
                    shardHitEffect = bonus.ShardHitEffect;
                }
            }
        }

        private void HandleEnemyDamaged(PlayerDamageEventData eventData)
        {
            if (bonuses.Count == 0 || eventData.IsChainDamage || eventData.IsDamageOverTime ||
                eventData.Target == null || eventData.Target.isDead ||
                !eventData.Target.TryGetComponent(out StatusEffectManager statusManager)) return;

            bool triggerShatter = EnableShatter && statusManager.TryGetEffect(out IceEffect _) &&
                UnityEngine.Random.value < ShatterChance;
            if (triggerShatter) statusManager.RemoveEffect<IceEffect>();

            // 냉기 속성 공격은 기존 공격 처리에서 냉기를 부여한다.
            if (!triggerShatter && !eventData.HasAttackTrait(Nytherion.Core.Enums.EquipmentTrait.Ice))
            {
                statusManager.ConfigureCombatContext(playerManager);
                statusManager.ApplyEffect(new IceEffect(ChillDuration));
            }

            if (!triggerShatter) return;
            Vector3 center = eventData.Target.transform.position;
            eventData.Target.TakeDamage(eventData.DamageAmount * Mathf.Max(0f, ShatterDamageRatio), true);
            if (playerManager != null &&
                playerManager.TryGetComponent(out IceSplinterRuntime iceSplinter))
            {
                iceSplinter.Fire(center, eventData.Target, eventData.DamageAmount * iceSplinter.ShardDamageRatio, 3, 8f);
            }
        }

        private void DamageNearbyEnemies(
            EnemyBase origin,
            Vector3 center,
            float radius,
            int maxTargets,
            float damage,
            bool applyIce)
        {
            if (damage <= 0f) return;

            Collider2D[] colliders = Physics2D.OverlapCircleAll(center, radius);
            HashSet<EnemyBase> uniqueEnemies = new HashSet<EnemyBase>();
            int hitCount = 0;
            foreach (Collider2D hit in colliders)
            {
                EnemyBase enemy = hit != null ? hit.GetComponentInParent<EnemyBase>() : null;
                if (enemy == null || enemy == origin || enemy.isDead || !uniqueEnemies.Add(enemy))
                {
                    continue;
                }

                PlayShardVisual(center, enemy.transform.position);
                enemy.TakeDamage(damage, true);
                if (applyIce && !enemy.isDead &&
                    enemy.TryGetComponent(out StatusEffectManager statusManager))
                {
                    statusManager.ConfigureCombatContext(playerManager);
                    statusManager.ApplyEffect(new IceEffect(3f));
                }

                hitCount++;
                if (hitCount >= maxTargets) break;
            }
        }

        private void PlayShardVisual(Vector3 origin, Vector3 destination)
        {
            if (shardTexture == null || shardHitEffect == null ||
                shardHitEffect.hitFrames == null || shardHitEffect.hitFrames.Length == 0 ||
                shardHitEffect.hitFrames[0] == null || !isActiveAndEnabled) return;

            if (!shardFrameCache.TryGetValue(shardTexture, out Sprite[] shardFrames))
            {
                if (shardTexture.width != 128 || shardTexture.height != 32) return;
                shardFrames = new Sprite[4];
                for (int i = 0; i < shardFrames.Length; i++)
                {
                    shardFrames[i] = Sprite.Create(shardTexture, new Rect(i * 32, 0, 32, 32),
                        new Vector2(0.5f, 0.5f), 32f);
                }
                shardFrameCache.Add(shardTexture, shardFrames);
            }

            SpriteRenderer visual = shardVisuals.Find(item => item != null && !item.gameObject.activeSelf);
            if (visual == null)
            {
                GameObject instance = new GameObject("Relic Ice Shard VFX");
                instance.transform.SetParent(transform, false);
                visual = instance.AddComponent<SpriteRenderer>();
                visual.sortingOrder = 10;
                shardVisuals.Add(visual);
            }

            visual.gameObject.SetActive(true);
            StartCoroutine(AnimateShard(visual, origin, destination, shardFrames, shardHitEffect));
        }

        private static IEnumerator AnimateShard(SpriteRenderer visual, Vector3 origin, Vector3 destination,
            Sprite[] frames, IceShotSkillData hitEffect)
        {
            Vector2 direction = (Vector2)(destination - origin);
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float duration = Mathf.Max(0.05f, Vector3.Distance(origin, destination) / 12f);
            float elapsed = 0f;
            visual.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, angle));
            visual.transform.localScale = Vector3.one * 0.6f;
            while (elapsed < duration)
            {
                visual.transform.position = Vector3.Lerp(origin, destination, elapsed / duration);
                visual.sprite = frames[Mathf.FloorToInt(elapsed * 12f) % frames.Length];
                yield return null;
                elapsed += Time.deltaTime;
            }

            visual.transform.SetPositionAndRotation(destination, Quaternion.Euler(0f, 0f, angle - 90f));
            visual.transform.localScale = Vector3.one * Mathf.Max(0.01f, hitEffect.hitVisualScale);
            elapsed = 0f;
            float fps = Mathf.Max(1f, hitEffect.hitFps);
            while (elapsed * fps < hitEffect.hitFrames.Length)
            {
                visual.sprite = hitEffect.hitFrames[Mathf.FloorToInt(elapsed * fps)];
                yield return null;
                elapsed += Time.deltaTime;
            }
            visual.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            foreach (SpriteRenderer visual in shardVisuals)
                if (visual != null) visual.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (Sprite[] frames in shardFrameCache.Values)
                foreach (Sprite frame in frames)
                    if (frame != null) Destroy(frame);
            shardFrameCache.Clear();
            if (eventManager != null)
            {
                eventManager.OnEnemyDamagedByPlayerDetailed -= HandleEnemyDamaged;
            }
        }
    }
}
