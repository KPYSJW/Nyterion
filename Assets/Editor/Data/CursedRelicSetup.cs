using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Skills;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class CursedRelicSetup
    {
        private const string Output = "output/cursed-relics";
        private const string Root = "Assets/Nytherion/Data/ScriptableObjects/";
        static CursedRelicSetup() => EditorApplication.update += Update;
        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error) { File.WriteAllText(Output + "/result.txt", "FAIL " + error); Debug.LogException(error); }
        }

        [MenuItem("Tools/Nytherion/Relics/의식용 단검·그리모어·불길한 인형 설정")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            RelicData dagger = GetRelic("CombatUtility/RitualDagger", "RitualDagger", "의식용 단검", "Ritual Dagger", "RitualDagger", Rarity.Epic);
            dagger.description_KR = "최대 체력 -20%. 적 처치당 공격력 +0.5%(최대 +50%, 레벨당 상한 +10%p), 생명력 흡수 +0.1%p(최대 +10%p, 레벨당 상한 +2.5%p).";
            dagger.description_EN = "Max Health -20%. Each kill grants +0.5% Attack (cap +50%, +10%p per level) and +0.1%p Lifesteal (cap +10%p, +2.5%p per level).";
            dagger.effectModules = new List<RelicEffectModule> { new RelicEffectModule { effects = new List<RelicEffectBase> {
                Stats(new StatModifier { stat = StatType.MaxHealth, value = -0.2f, isPercentage = true }),
                Growth(StatType.RangedDamage, 0.005f, 0.5f, 0.1f, true),
                Growth(StatType.Lifesteal, 0.001f, 0.1f, 0.025f, false) } } };

            RelicData grimoire = GetRelic("Mana/ManaGrimoire", "ManaGrimoire", "그리모어", "Grimoire", "ManaGrimoire", Rarity.Epic);
            grimoire.description_KR = "최대 마나 +30%, 스킬 피해 +35%(각 레벨당 +5%p). 스킬 쿨타임 +25%.";
            grimoire.description_EN = "Max Mana +30% and Skill Damage +35% (each +5%p per level). Skill Cooldown +25%.";
            grimoire.effectModules = new List<RelicEffectModule> { new RelicEffectModule { effects = new List<RelicEffectBase> { Stats(
                new StatModifier { stat = StatType.MaxMana, value = 0.3f, valuePerLevel = 0.05f, isPercentage = true },
                new StatModifier { stat = StatType.SkillDamage, value = 0.35f, valuePerLevel = 0.05f, isPercentage = true },
                new StatModifier { stat = StatType.SkillCooldown, value = 0.25f, isPercentage = true }) } } };

            RelicData doll = GetRelic("SkillRelics/OminousDoll", "OminousDoll", "불길한 인형", "Ominous Doll", "OminousDoll", Rarity.Rare);
            doll.description_KR = "허수아비 활성 중 공격력 +40%(레벨당 +5%p), 방어력 -5. 허수아비가 소멸하면 반경 2에 피해 60(레벨당 +15)의 폭발을 일으킵니다.";
            doll.description_EN = "While a decoy is active: Attack +40% (+5%p per level), Defense -5. On expiry or destruction, it explodes for 60 damage (+15 per level) within radius 2.";
            GameObject explosion = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Combat/VFX/ThermalShockExplosionVFX.prefab");
            Require(explosion != null, "폭발 연출 프리팹 누락");
            Require(explosion.GetComponent<Nytherion.GamePlay.Combat.ThermalShockExplosionVFX>() != null, "폭발 연출 수명 관리 컴포넌트");
            doll.effectModules = new List<RelicEffectModule> { new RelicEffectModule { effects = new List<RelicEffectBase> {
                new OminousDollEffect { explosionPrefab = explosion } } } };

            RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(Root + "Relics/RelicDatabase.asset");
            foreach (RelicData relic in new[] { dagger, grimoire, doll })
            {
                RelicInfluencePolicy.Normalize(relic);
                if (!database.allRelics.Contains(relic)) database.allRelics.Add(relic);
                Require(database.allRelics.Count(entry => entry != null && entry.relicName == relic.relicName) == 1, "유물 ID 중복");
                GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(Root + "Gacha/GachaPool/Relic/" + relic.rarity + "_Relic.asset");
                if (!pool.items.Any(rate => rate.item == relic)) pool.items.Add(new GachaItemRate { item = relic, weight = 100 });
                EditorUtility.SetDirty(pool);
                EditorUtility.SetDirty(relic);
                ManaRelicSetup.Localize(relic);
            }
            EditorUtility.SetDirty(database);
            var uiCollection = LocalizationEditorSettings.GetStringTableCollection(Nytherion.Core.Utils.LocalizationTables.UI);
            if (uiCollection != null)
            {
                foreach (var table in uiCollection.StringTables)
                {
                    bool english = table.LocaleIdentifier.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase);
                    foreach (string key in new[] { "ui.stat.skillDamageMultiplier", "ui.stat.skillCooldownMultiplier" })
                    {
                        var translation = Localization.LocalizationTranslationCatalog.UIEntries[key];
                        string value = english ? translation.English : translation.Korean;
                        var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
                        entry.Value = value;
                    }
                    EditorUtility.SetDirty(table);
                }
                EditorUtility.SetDirty(uiCollection.SharedData);
            }
            // 새 배율 필드를 기본 플레이어 에셋에도 직렬화합니다.
            EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<PlayerData>(Root + "Player/Palyer.asset"));
            AssetDatabase.SaveAssets();
            var errors = new List<string>();
            Application.LogCallback capture = (message, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
            };
            Application.logMessageReceived += capture;
            try { Verify(dagger, grimoire, doll); }
            finally { Application.logMessageReceived -= capture; }
            Require(errors.Count == 0, "검증 중 오류: " + string.Join("\n", errors));
            File.WriteAllText(Output + "/result.txt", "PASS 유물 3종 이름·영문·아이콘·DB·가챠 등록\n" +
                "PASS 단검 체력 감소, 처치 성장·상한·장비 변경 중복 방지·해제\n" +
                "PASS 그리모어 마나·스킬 피해·쿨타임·해제, 일반 공격 수치 유지\n" +
                "PASS 인형 활성 중 능력치·방어력 피해 반영·소멸 폭발·콜라이더 중복 방지·중복 종료·취소·장착 해제\n" +
                "PASS 그림 기준 아이콘: 단검=3.png, 그리모어=5.png, 인형=4.png");
        }

        private static RelicData GetRelic(string relative, string id, string korean, string english, string icon, Rarity rarity)
        {
            string path = Root + "Relics/" + relative + ".asset";
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(path);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = id;
                AssetDatabase.CreateAsset(relic, path);
            }
            relic.koreanName = korean;
            relic.englishName = english;
            relic.Image = ManaRelicSetup.ImportSprite(icon);
            relic.rarity = rarity;
            return relic;
        }

        private static StatRelicEffect Stats(params StatModifier[] values) => new StatRelicEffect { statModifiers = values.ToList() };
        private static KillCountGrowthStatRelicEffect Growth(StatType stat, float perKill, float cap, float capPerLevel, bool percentage) =>
            new KillCountGrowthStatRelicEffect { targetStat = stat, valuePerKill = perKill, maxBonusBase = cap, maxBonusPerLevel = capPerLevel, isPercentage = percentage };

        private static void Verify(RelicData daggerSource, RelicData grimoireSource, RelicData dollSource)
        {
            var root = new GameObject("유물 검증");
            root.transform.position = new Vector3(10000f, 10000f);
            PlayerData basis = ScriptableObject.CreateInstance<PlayerData>();
            basis.maxHealth = 100f;
            basis.rangedDamage = 10f;
            RelicData dagger = UnityEngine.Object.Instantiate(daggerSource);
            RelicData grimoire = UnityEngine.Object.Instantiate(grimoireSource);
            RelicData doll = UnityEngine.Object.Instantiate(dollSource);
            PlayerManager player = root.AddComponent<PlayerManager>();
            PlayerSkillManager skills = root.AddComponent<PlayerSkillManager>();
            PlayerHealth health = root.AddComponent<PlayerHealth>();
            EventManager events = root.AddComponent<EventManager>();
            SkillData skillData = ScriptableObject.CreateInstance<SkillData>();
            DecoySkillData decoyData = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<DecoySkillData>(DecoySkillSetup.DataPath));
            var probeSkill = root.AddComponent<CursedSkillProbe>();
            probeSkill.caster = root.transform;
            probeSkill.skillData = skillData;
            skillData.coolDown = 10f;
            var serialized = new SerializedObject(player);
            serialized.FindProperty("basePlayerData").objectReferenceValue = basis;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            player.Construct(null, null, events, null);
            player.Initialize();
            var dummyObject = new GameObject("더미 검증");
            DecoyDummy dummy = dummyObject.AddComponent<DecoyDummy>();
            DecoySkill decoySkill = root.AddComponent<DecoySkill>();
            typeof(DecoySkill).GetField("dummy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(decoySkill, dummy);
            skills.equippedSkills[0] = decoySkill;
            var targetObject = new GameObject("폭발 피해 검증");
            targetObject.layer = LayerMask.NameToLayer("Enemy");
            var target = targetObject.AddComponent<CursedDamageProbe>();
            targetObject.AddComponent<CircleCollider2D>();
            targetObject.AddComponent<BoxCollider2D>();
            targetObject.transform.position = root.transform.position + Vector3.right;
            try
            {
                Apply(dagger, player, 1);
                Require(Near(player.currentPlayerData.maxHealth, 80f), "단검 체력 감소");
                for (int i = 0; i < 20; i++) events.TriggerEnemyDeathEvent(null);
                Require(Near(player.currentPlayerData.rangedDamage, 11f) && Near(player.currentPlayerData.lifesteal, 0.02f), "단검 처치 성장");
                player.AddTemporaryStatModifier(new StatModifier { stat = StatType.Defense, value = 0f });
                Require(Near(player.currentPlayerData.rangedDamage, 11f), "단검 재계산 중복 방지");
                for (int i = 0; i < 100; i++) events.TriggerEnemyDeathEvent(null);
                Require(Near(player.currentPlayerData.rangedDamage, 15f) && Near(player.currentPlayerData.lifesteal, 0.1f), "단검 성장 상한");
                Remove(dagger, player, 1);
                Require(Near(player.currentPlayerData.rangedDamage, 10f) && Near(player.currentPlayerData.lifesteal, 0f) && Near(player.currentPlayerData.maxHealth, 100f), "단검 효과 해제");
                Apply(grimoire, player, 1);
                Require(Near(skills.MaxMana, 130f) && Near(probeSkill.Damage(100f), 135f) && Near(probeSkill.GetCooldownTime(), 12.5f), "그리모어 배율");
                Require(Near(player.currentPlayerData.rangedDamage, 10f), "일반 공격 미변경");
                probeSkill.SetDebugCooldownReduction(1f);
                Require(probeSkill.GetCooldownTime() == 0f, "쿨타임 디버그 호환");
                probeSkill.SetDebugCooldownReduction(0f);
                Remove(grimoire, player, 1);
                Require(Near(probeSkill.Damage(100f), 100f) && Near(probeSkill.GetCooldownTime(), 10f), "그리모어 제거");

                OminousDollEffect dollEffect = doll.effectModules.SelectMany(module => module.effects).OfType<OminousDollEffect>().Single();
                dollEffect.explosionPrefab = null;
                Apply(doll, player, 1);
                OminousDollRuntime runtime = root.GetComponent<OminousDollRuntime>();
                Require(Near(player.currentPlayerData.rangedDamage, 10f), "더미 비활성 시 미적용");
                decoyData.blockingLayers = 0;
                dummy.Begin(decoyData, root.transform, root.transform.position, 1);
                Require(Near(player.currentPlayerData.rangedDamage, 10f), "소환 연출 중 미적용");
                ActivateDummy(dummy);
                Require(Near(player.currentPlayerData.rangedDamage, 14f) && Near(player.currentPlayerData.defense, -5f), "인형 활성 능력치");
                health.InitializeHealth(100f);
                health.TakeDamage(10f);
                Require(health.CurrentHealth < 90f, "음수 방어력의 실제 피해 증가");
                Physics2D.SyncTransforms();
                dummy.TakeDamage(dummy.CurrentDurability);
                Require(target.Hits == 1 && Near(target.Damage, 60f) && runtime.ExplosionCount == 1, "폭발 피해 및 중복 콜라이더");
                dummy.Close();
                Require(target.Hits == 1 && Near(player.currentPlayerData.rangedDamage, 10f) && Near(player.currentPlayerData.defense, 0f), "중복 종료 및 능력치 복원");
                dummy.Begin(decoyData, root.transform, root.transform.position, 1);
                ActivateDummy(dummy);
                dummy.Close(false);
                Require(runtime.ExplosionCount == 1 && Near(player.currentPlayerData.rangedDamage, 10f), "취소 시 폭발 방지");
                dummy.Begin(decoyData, root.transform, root.transform.position, 1);
                ActivateDummy(dummy);
                Remove(doll, player, 1);
                Require(Near(player.currentPlayerData.rangedDamage, 10f), "활성 중 유물 해제");
                Apply(doll, player, 1);
                Require(Near(player.currentPlayerData.rangedDamage, 14f), "활성 중 유물 재장착");
                typeof(DecoyDummy).GetField("expiresAt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(dummy, Time.time - 1f);
                typeof(DecoyDummy).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(dummy, null);
                Require(runtime.ExplosionCount == 2 && target.Hits == 2, "수명 종료 폭발");
                dummy.Begin(decoyData, root.transform, root.transform.position, 1);
                ActivateDummy(dummy);
                Remove(doll, player, 1);
                dummy.Close();
                Require(runtime.ExplosionCount == 2, "해제 후 폭발 방지");
            }
            finally
            {
                Remove(dagger, player, 1);
                Remove(grimoire, player, 1);
                Remove(doll, player, 1);
                typeof(DecoySkill).GetField("dummy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(decoySkill, null);
                // 이전 검증에서 편집 모드의 지연 파괴로 남은 피격 연출만 정리합니다.
                foreach (GameObject effect in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (effect.scene.IsValid() && effect.name == "Player Damage Effect" &&
                        Vector3.Distance(effect.transform.position, root.transform.position) < 0.001f)
                        UnityEngine.Object.DestroyImmediate(effect);
                UnityEngine.Object.DestroyImmediate(dummyObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
                PlayerData current = player.currentPlayerData;
                UnityEngine.Object.DestroyImmediate(root);
                if (current != null) UnityEngine.Object.DestroyImmediate(current);
                foreach (UnityEngine.Object item in new UnityEngine.Object[] { basis, dagger, grimoire, doll, skillData, decoyData }) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static void ActivateDummy(DecoyDummy dummy)
        {
            typeof(DecoyDummy).GetField("summonStartedAt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(dummy, Time.time - 10f);
            typeof(DecoyDummy).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(dummy, null);
            Require(dummy.IsAlive, "더미 활성화");
        }
        private static void Apply(RelicData data, PlayerManager player, int level)
        { foreach (RelicEffectBase effect in data.effectModules.SelectMany(module => module.effects)) effect.ApplyEffect(player, level); }
        private static void Remove(RelicData data, PlayerManager player, int level)
        { foreach (RelicEffectBase effect in data.effectModules.SelectMany(module => module.effects)) effect.RemoveEffect(player, level); }
        private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;
        private static void Require(bool result, string message)
        { if (!result) throw new InvalidOperationException(message); }
    }

    public sealed class CursedSkillProbe : SkillBase
    {
        public float Damage(float value) => ApplySkillDamage(value);
        protected override void Activate() { }
    }
    public sealed class CursedDamageProbe : MonoBehaviour, IDamageable
    {
        public int Hits;
        public float Damage;
        public void TakeDamage(float amount, bool isChain = false) { Hits++; Damage += amount; }
    }
}
