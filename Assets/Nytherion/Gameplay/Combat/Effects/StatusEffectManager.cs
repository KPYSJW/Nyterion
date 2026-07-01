using UnityEngine;
using System.Collections.Generic;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.Data.ScriptableObjects;
using Nytherion.Core.Data;
using Nytherion.GamePlay.Relics;
using VContainer;

namespace Nytherion.GamePlay.Combat
{
    public class StatusEffectManager : MonoBehaviour
    {
        private List<StatusEffect> activeEffects = new List<StatusEffect>();
        private EnemyBase owner;
        private Dictionary<string, GameObject> vfxDictionary = new Dictionary<string, GameObject>();
        private EnemyStatusDisplayUI statusDisplay;
        private StatusEffectDatabase database;

        [Inject]
        public void Construct(StatusEffectDatabase database)
        {
            this.database = database;
        }

        private void Awake()
        {
            owner = GetComponent<EnemyBase>();
            CacheVFX();

            // VContainer 자동 주입이 수행되지 않은 경우(예: 풀링에 의한 동적 생성), 수동으로 전역 컨테이너에서 해소
            if (database == null && RootLifetimeScope.Instance != null)
            {
                IObjectResolver container = RootLifetimeScope.Instance.Container;
                if (container != null)
                {
                    database = container.Resolve<StatusEffectDatabase>();
                }
            }
        }

        private void Start()
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            statusDisplay = gameObject.AddComponent<EnemyStatusDisplayUI>();
            statusDisplay.Initialize(sr);
        }

        private void CacheVFX()
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == transform) continue;

                string name = child.name;

                if (name == "Electricity")
                {
                    CacheAndResetVFX("Lightning", child.gameObject);
                }
                else if (name == "Freezing")
                {
                    CacheAndResetVFX("Ice", child.gameObject);
                }
                else if (name == "Poisoning")
                {
                    CacheAndResetVFX("Poison", child.gameObject);
                }
                else if (name == "Burning")
                {
                    CacheAndResetVFX("Fire", child.gameObject);
                }
                else if (name == "Fire" || name == "Curse" || name == "Holy" || name == "Demonic")
                {
                    CacheAndResetVFX(name, child.gameObject);
                }
                else if (name.EndsWith("EffectVFX"))
                {
                    string key = name.Replace("EffectVFX", "");
                    CacheAndResetVFX(key, child.gameObject);
                }
            }
        }

        private void CacheAndResetVFX(string key, GameObject go)
        {
            vfxDictionary[key] = go;
            go.SetActive(false);
        }

        public void PlayVFX(string effectId)
        {
            // 속성 이펙트 재생 비활성화
            /*
            GameObject go;
            if (vfxDictionary.TryGetValue(effectId, out go))
            {
                go.SetActive(true);
                if (effectId == "Lightning")
                {
                    float randomZRotation = UnityEngine.Random.Range(0f, 360f);
                    go.transform.localRotation = Quaternion.Euler(0f, 0f, randomZRotation);
                }
                Animator animatorComponent = go.GetComponent<Animator>();
                if (animatorComponent == null)
                {
                    animatorComponent = go.GetComponentInChildren<Animator>();
                }
                if (animatorComponent != null)
                {
                    animatorComponent.Rebind();
                    animatorComponent.Update(0f);
                }
            }
            */
        }

        public void StopVFX(string effectId)
        {
            // 속성 이펙트 정지 비활성화
            /*
            GameObject go;
            if (vfxDictionary.TryGetValue(effectId, out go))
            {
                go.SetActive(false);
            }
            */
        }

        public void ApplyEffect(StatusEffect newEffect)
        {
            if (database == null)
            {
                Debug.LogError("StatusEffectDatabase가 주입되지 않았습니다! RootLifetimeScope를 확인하세요.");
            }
            if (newEffect == null) return;

            if (newEffect is FireEffect)
            {
                Nytherion.Core.Managers.RelicManager relicManager = UnityEngine.Object.FindObjectOfType<Nytherion.Core.Managers.RelicManager>();
                if (relicManager != null)
                {
                    foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                    {
                        RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                        if (block != null && block.RelicId == "Sulphur Hourglass" && !block.SourceData.isDisabled)
                        {
                            float multiplier = 1.5f + (block.SourceData.level - 1) * 0.1f;
                            newEffect.ModifyDuration(newEffect.Duration * multiplier);
                            break;
                        }
                    }
                }
            }
            else if (newEffect is PoisonEffect)
            {
                Nytherion.Core.Managers.RelicManager relicManager = UnityEngine.Object.FindObjectOfType<Nytherion.Core.Managers.RelicManager>();
                if (relicManager != null)
                {
                    float durationMultiplier = 1.0f;
                    foreach (KeyValuePair<string, Vector2Int> pair in relicManager.GetPlacedBlocks())
                    {
                        RelicBlock block = relicManager.GetBlockAt(pair.Value.y, pair.Value.x);
                        if (block != null && !block.SourceData.isDisabled)
                        {
                            if (block.RelicId == "Venom Hourglass")
                            {
                                float bonus = 0.5f + (block.SourceData.level - 1) * 0.1f;
                                durationMultiplier += bonus;
                            }
                            else if (block.RelicId == "Hydra's Fang")
                            {
                                float bonus = 0.3f + (block.SourceData.level - 1) * 0.05f;
                                durationMultiplier += bonus;
                            }
                        }
                    }
                    if (durationMultiplier > 1.0f)
                    {
                        newEffect.ModifyDuration(newEffect.Duration * durationMultiplier);
                    }
                }
            }

            if (database != null)
            {
                newEffect.EffectIcon = database.GetIcon(newEffect.EffectId);
            }

            StatusEffect existing = activeEffects.Find(e => e.EffectId == newEffect.EffectId);
            if (existing != null)
            {
                existing.ResetDuration();
                existing.OnStack(newEffect);
                existing.EffectIcon = newEffect.EffectIcon;
                if (statusDisplay != null)
                {
                    statusDisplay.UpdateDisplay(activeEffects);
                }
                return;
            }

            activeEffects.Add(newEffect);
            newEffect.Initialize(owner, this, newEffect.Duration);
            newEffect.OnApply();

            if (owner != null)
            {
                owner.UpdateStatusColor();
            }

            if (statusDisplay != null)
            {
                statusDisplay.UpdateDisplay(activeEffects);
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            bool anyRemoved = false;
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                StatusEffect effect = activeEffects[i];
                effect.UpdateTimer(deltaTime);
                effect.OnUpdate(deltaTime);

                if (effect.Timer <= 0f)
                {
                    effect.OnRemove();
                    activeEffects.RemoveAt(i);
                    anyRemoved = true;
                }
            }

            if (anyRemoved)
            {
                if (owner != null)
                {
                    owner.UpdateStatusColor();
                }
                if (statusDisplay != null)
                {
                    statusDisplay.UpdateDisplay(activeEffects);
                }
            }
        }

        public float GetReceivedDamageMultiplier()
        {
            float multiplier = 1.0f;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (activeEffects[i] is CurseEffect curse)
                {
                    multiplier *= curse.DamageMultiplier;
                }
            }
            return multiplier;
        }

        public float GetSpeedMultiplier()
        {
            float multiplier = 1.0f;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (activeEffects[i] is IceEffect chill)
                {
                    multiplier *= (1f - chill.SpeedReduction);
                }
            }
            return multiplier;
        }

        public float GetOutgoingDamageMultiplier()
        {
            float multiplier = 1.0f;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (activeEffects[i] is HolyEffect holy)
                {
                    multiplier *= holy.OutgoingDamageMultiplier;
                }
            }
            return multiplier;
        }

        public float GetCritDamageMultiplierModifier()
        {
            float modifier = 0f;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (activeEffects[i] is DemonicEffect demonic)
                {
                    modifier += demonic.ExtraCritDamageMultiplier;
                }
            }
            return modifier;
        }

        public float GetDefenseMultiplier()
        {
            float multiplier = 1.0f;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (activeEffects[i] is DemonicEffect demonic)
                {
                    multiplier *= (1f - demonic.DefenseReduction);
                }
            }
            return multiplier;
        }

        public bool HasEffect(string effectId)
        {
            return activeEffects.Exists(e => e.EffectId == effectId);
        }

        public Color GetStatusEffectColor()
        {
            if (activeEffects.Count == 0)
            {
                return Color.white;
            }
            return activeEffects[activeEffects.Count - 1].EffectColor;
        }

        public void TriggerLightningChain(float damageAmount)
        {
            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (activeEffects[i] is LightningEffect shock)
                {
                    shock.TriggerChainLightning(damageAmount);
                    break;
                }
            }
        }

        public void TriggerHolyHeal()
        {
            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (activeEffects[i] is HolyEffect holy)
                {
                    holy.TriggerHealChance();
                    break;
                }
            }
        }

        public void ClearAllEffects()
        {
            for (int i = 0; i < activeEffects.Count; i++)
            {
                activeEffects[i].OnRemove();
            }
            activeEffects.Clear();
            if (statusDisplay != null)
            {
                statusDisplay.UpdateDisplay(activeEffects);
            }
        }

        private void OnEnable()
        {
            if (vfxDictionary != null)
            {
                foreach (KeyValuePair<string, GameObject> pair in vfxDictionary)
                {
                    if (pair.Value != null)
                    {
                        pair.Value.SetActive(false);
                    }
                }
            }
        }

        private void OnDisable()
        {
            ClearAllEffects();
        }
    }
}
