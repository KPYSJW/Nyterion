using UnityEngine;
using System.Collections.Generic;
using Nytherion.GamePlay.Characters.Enemy;

namespace Nytherion.GamePlay.Combat
{
    public class StatusEffectManager : MonoBehaviour
    {
        private List<StatusEffect> activeEffects = new List<StatusEffect>();
        private EnemyBase owner;
        private Dictionary<string, GameObject> vfxDictionary = new Dictionary<string, GameObject>();

        private void Awake()
        {
            owner = GetComponent<EnemyBase>();
            CacheVFX();
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
        }

        public void StopVFX(string effectId)
        {
            GameObject go;
            if (vfxDictionary.TryGetValue(effectId, out go))
            {
                go.SetActive(false);
            }
        }

        public void ApplyEffect(StatusEffect newEffect)
        {
            if (newEffect == null) return;

            StatusEffect existing = activeEffects.Find(e => e.EffectId == newEffect.EffectId);
            if (existing != null)
            {
                existing.ResetDuration();
                existing.OnStack(newEffect);
                return;
            }

            activeEffects.Add(newEffect);
            newEffect.Initialize(owner, this, newEffect.Duration);
            newEffect.OnApply();

            if (owner != null)
            {
                owner.UpdateStatusColor();
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

            if (anyRemoved && owner != null)
            {
                owner.UpdateStatusColor();
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
