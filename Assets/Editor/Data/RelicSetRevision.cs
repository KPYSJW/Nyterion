using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>세트 표시 변경 시 아이콘의 GUID를 보존하고 지속 피해 초월을 검증한다.</summary>
    [InitializeOnLoad]
    public static class RelicSetRevision
    {
        private const string Root = "Assets/Nytherion/Data/ScriptableObjects/Relics/";
        private const string Icons = "Assets/Nytherion/Art/UI/Relics/";
        private const string Output = "output/relic-set-revision";
        private const string Pending = "Nytherion.RelicSetRevision.Verify";
        private static readonly List<string> results = new List<string>();
        private static readonly List<Object> temporary = new List<Object>();
        private static double readyAt;

        private static readonly (string file, string icon, string korean, string english)[] Renames =
        {
            ("GoldenRushTranscendence", "GoldenRush", "골드 러쉬", "Gold Rush"),
            ("GoldHunterSet", "GoldHunter", "탐욕", "Greed"),
            ("MobileWarriorSet", "MobileWarrior", "돌풍", "WindBlast"),
            ("LightningPursuitTranscendence", "LightningPursuit", "연쇄 폭풍", "Chain Storm"),
            ("FrostWardenSet", "FrostWarden", "만년서리", "Everfrost"),
            ("TrickshotEngineerSet", "TrickshotEngineer", "난반사", "Ricochet"),
            ("IceSplinterTranscendence", "IceSplinter", "얼음 파편", "Ice Splinter")
        };

        static RelicSetRevision()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 4d;
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/apply.request"))
            {
                File.Delete(Output + "/apply.request");
                try { Apply(); }
                catch (Exception error) { File.WriteAllText(Output + "/apply.txt", "FAIL " + error); Debug.LogException(error); }
            }
            if (File.Exists(Output + "/verify.request"))
            {
                File.Delete(Output + "/verify.request");
                SessionState.SetBool(Pending, true);
                SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
                readyAt = EditorApplication.timeSinceStartup + 4d;
                if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
                EditorApplication.timeSinceStartup < readyAt) return;
            SessionState.SetBool(Pending, false);
            results.Clear();
            try { VerifyDamage(); }
            catch (Exception error) { results.Add("FAIL " + error); Debug.LogException(error); }
            finally
            {
                for (int i = temporary.Count - 1; i >= 0; i--)
                    if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
                temporary.Clear();
                File.WriteAllLines(Output + "/verification.txt", results);
                if (SessionState.GetBool(Pending + ".Exit", false)) EditorApplication.isPlaying = false;
            }
        }

        [MenuItem("Tools/Nytherion/Relics/세트 이름·아이콘 갱신")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            results.Clear();
            // 전체 대상과 충돌 여부를 먼저 확인한 뒤 Unity API로 이름을 바꾼다.
            foreach (var entry in Renames)
            {
                Object data = AssetDatabase.LoadMainAssetAtPath(Root + entry.file + ".asset");
                Sprite icon = data is RelicSetBonusData set ? set.statusIcon : ((RelicTranscendenceData)data).statusIcon;
                string source = AssetDatabase.GetAssetPath(icon);
                string destination = Icons + entry.english.Replace(" ", "") + "Icon.png";
                Require(icon != null && (source == Icons + entry.icon + "Icon.png" || source == destination), entry.file + " 아이콘 참조 확인");
                Require(source == destination || !File.Exists(destination), entry.file + " 아이콘 이름 충돌 없음");
            }
            foreach (var entry in Renames)
            {
                Object data = AssetDatabase.LoadMainAssetAtPath(Root + entry.file + ".asset");
                Sprite icon = data is RelicSetBonusData set ? set.statusIcon : ((RelicTranscendenceData)data).statusIcon;
                string source = AssetDatabase.GetAssetPath(icon);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(icon, out string guid, out long localId);
                string newName = entry.english.Replace(" ", "") + "Icon";
                if (Path.GetFileNameWithoutExtension(source) != newName)
                {
                    string error = AssetDatabase.RenameAsset(source, newName);
                    Require(string.IsNullOrEmpty(error), entry.file + " 아이콘 이름 변경: " + error);
                }
                Sprite renamed = AssetDatabase.LoadAssetAtPath<Sprite>(Icons + newName + ".png");
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renamed, out string newGuid, out long newLocalId);
                Require(guid == newGuid && localId == newLocalId && renamed.name == newName,
                    newName + " GUID·스프라이트 ID·이름 보존 확인");
                if (data is RelicSetBonusData renamedSet)
                    Require(renamedSet.setName_KR == entry.korean && renamedSet.setName_EN == entry.english && renamedSet.statusIcon == renamed,
                        entry.korean + " 한영 이름·Inspector 참조 확인");
                else
                {
                    var transcendence = (RelicTranscendenceData)data;
                    Require(transcendence.effectName_KR == entry.korean && transcendence.effectName_EN == entry.english && transcendence.statusIcon == renamed,
                        entry.korean + " 한영 이름·Inspector 참조 확인");
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:RelicSetBonusData", new[] { Root.TrimEnd('/') }))
            {
                var set = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(AssetDatabase.GUIDToAssetPath(guid));
                Require(set.bonusModules.All(module => module != null && module.condition != null && module.effects.All(effect => effect != null)),
                    set.setName_KR + " 세트 모듈 역직렬화 확인");
                SyncEntry(LocalizationKeys.RelicSetName(set.synergySeriesId), set.setName_KR, set.setName_EN);
                SyncEntry(LocalizationKeys.RelicSetDescription(set.synergySeriesId), set.description_KR, set.description_EN);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:RelicTranscendenceData", new[] { Root.TrimEnd('/') }))
            {
                var effect = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(AssetDatabase.GUIDToAssetPath(guid));
                Require(effect.requirements.All(requirement => requirement.setBonusData != null) &&
                    effect.effectModules.All(module => module != null && module.effects.All(item => item != null)),
                    effect.effectName_KR + " 초월 조건·효과 역직렬화 확인");
                SyncEntry(LocalizationKeys.RelicTranscendenceName(effect.name), effect.effectName_KR, effect.effectName_EN);
                SyncEntry(LocalizationKeys.RelicTranscendenceDescription(effect.name), effect.description_KR, effect.description_EN);
            }
            File.WriteAllLines(Output + "/apply.txt", results);
        }

        private static void SyncEntry(string key, string korean, string english)
        {
            // 기존 테이블이 있는 프로젝트에서만 해당 항목을 동기화한다. 전체 현지화 마이그레이션은 실행하지 않는다.
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationTables.Relics);
            if (collection == null) return;
            foreach (StringTable table in collection.StringTables)
            {
                string locale = table.LocaleIdentifier.Code;
                if (locale != "ko" && locale != "en") continue;
                string value = locale == "ko" ? korean : english;
                var entry = table.GetEntry(key);
                if (entry == null) table.AddEntry(key, value);
                else entry.Value = value;
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssetIfDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssetIfDirty(collection.SharedData);
        }

        private static void VerifyDamage()
        {
            PlayerManager player = Track(new GameObject("[부식성 화염 검증] 플레이어")).AddComponent<PlayerManager>();
            EventManager events = Track(new GameObject("[부식성 화염 검증] 이벤트")).AddComponent<EventManager>();
            PlayerData data = Track(ScriptableObject.CreateInstance<PlayerData>());
            data.critChance = -1f;
            player.currentPlayerData = data;
            GameObject target = Track(new GameObject("[부식성 화염 검증] 대상"));
            TrainingDummy enemy = target.AddComponent<TrainingDummy>();
            EnemyData enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.maxHealth = 1000000f;
            enemy.Construct(events, null, player);
            enemy.Initialize(enemyData);
            StatusEffectManager status = target.GetComponent<StatusEffectManager>();
            status.ConfigureCombatContext(player);
            var asset = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(Root + "CorrosiveFlameTranscendence.asset");
            var effect = (CorrosiveFlameEffect)asset.effectModules[0].effects[0];
            Require(effect.tickDamageMultiplier == 1.25f && effect.tickIntervalMultiplier == 0.75f, "실제 에셋의 피해 +25%·간격 -25%");
            effect.ApplyEffect(player, 1);
            var fire = new FireEffect(100f, 10f);
            status.ApplyEffect(fire);
            Require(fire.TickDamage == 100f && fire.TickInterval == 0.5f, "화상 단독에는 강화 없음");
            SetNextTick(fire, Time.time + 0.2f);
            var poison = new PoisonEffect(80f, 10f);
            status.ApplyEffect(poison);
            Require(fire.TickDamage == 125f && fire.TickInterval == 0.375f && poison.TickDamage == 100f && poison.TickInterval == 0.75f,
                "화상 후 중독 적용 즉시 두 지속 피해 강화");
            Require(Mathf.Abs(GetNextTick(fire) - Time.time - 0.15f) < 0.001f, "이미 진행 중인 화상 틱의 남은 비율 유지");
            float damage = 0f;
            int hits = 0;
            events.OnEnemyDamagedByPlayerDetailed += hit => { damage = hit.DamageAmount; hits++; };
            SetNextTick(fire, Time.time - 1f);
            fire.OnUpdate(0f);
            Require(damage == 125f && hits == 1 && Mathf.Abs(GetNextTick(fire) - Time.time - 0.375f) < 0.001f,
                "실제 화상 틱 피해 125·다음 간격 0.375초");
            SetNextTick(poison, Time.time - 1f);
            poison.OnUpdate(0f);
            Require(damage == 100f && hits == 2 && Mathf.Abs(GetNextTick(poison) - Time.time - 0.75f) < 0.001f,
                "실제 중독 틱 피해 100·다음 간격 0.75초");
            effect.ApplyEffect(player, 1);
            status.ConfigureCombatContext(player);
            Require(fire.TickDamage == 125f && poison.TickDamage == 100f, "중복 활성화 시 배율 누적 없음");
            effect.RemoveEffect(player, 1);
            status.ConfigureCombatContext(player);
            Require(fire.TickDamage == 100f && fire.TickInterval == 0.5f && poison.TickDamage == 80f && poison.TickInterval == 1f,
                "초월 해제 시 기존 지속 피해 원복");
            effect.ApplyEffect(player, 1);
            status.ConfigureCombatContext(player);
            poison.UpdateTimer(11f);
            status.ConfigureCombatContext(player);
            Require(fire.TickDamage == 100f && fire.TickInterval == 0.5f, "중독 만료 시 화상 강화 해제");
            status.ClearAllEffects();
            poison = new PoisonEffect(80f, 10f);
            status.ApplyEffect(poison);
            Require(poison.TickDamage == 80f && poison.TickInterval == 1f, "중독 단독에는 강화 없음");
            fire = new FireEffect(100f, 10f);
            status.ApplyEffect(fire);
            Require(fire.TickDamage == 125f && poison.TickDamage == 100f, "중독 후 화상 적용 순서도 동일");
            status.ApplyEffect(new FireEffect(100f, 10f));
            status.ApplyEffect(new PoisonEffect(80f, 10f));
            Require(fire.TickDamage == 125f && poison.TickDamage == 200f, "재적용·중첩 시 초월 배율이 중복되지 않음");
            fire.UpdateTimer(11f);
            status.ConfigureCombatContext(player);
            Require(poison.TickDamage == 160f && poison.TickInterval == 1f, "화상 만료 시 중독 중첩은 유지하고 강화 해제");
            status.ClearAllEffects();
            player.Construct(null, null, events, null);
            var fireSet = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(Root + "PyromancerSet.asset");
            var poisonSet = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(Root + "PlagueAlchemistSet.asset");
            var fireBonus = (FireSetBonusEffect)fireSet.bonusModules[1].effects[0];
            var poisonBonus = (PoisonSetBonusEffect)poisonSet.bonusModules[1].effects[0];
            fireBonus.ApplyEffect(player, 1);
            poisonBonus.ApplyEffect(player, 1);
            fire = new FireEffect(100f, 10f);
            poison = new PoisonEffect(80f, 10f);
            status.ApplyEffect(fire);
            status.ApplyEffect(poison);
            Require(Mathf.Approximately(fire.TickDamage, 143.75f) && Mathf.Approximately(fire.TickInterval, 0.31875f) &&
                Mathf.Approximately(poison.TickDamage, 118f) && Mathf.Approximately(poison.TickInterval, 0.75f),
                "실제 화염술사·역병술사 4세트와 피해·주기 배율 함께 적용");
            status.ApplyEffect(new FireEffect(100f, 10f));
            Require(fire.StackCount == 2 && Mathf.Approximately(fire.TickDamage, 287.5f) &&
                Mathf.Approximately(fire.TickInterval, 0.31875f), "화상 2중첩 시 세트·초월 배율 중복 없음");
            effect.RemoveEffect(player, 1);
            status.ConfigureCombatContext(player);
            Require(Mathf.Approximately(fire.TickDamage, 230f) && Mathf.Approximately(fire.TickInterval, 0.425f) &&
                Mathf.Approximately(poison.TickDamage, 94.4f) && poison.TickInterval == 1f,
                "초월만 해제하면 화염·독 세트 강화와 중첩 유지");
            fireBonus.RemoveEffect(player, 1);
            poisonBonus.RemoveEffect(player, 1);
            status.ClearAllEffects();
            effect.RemoveEffect(player, 1);
            Require(player.GetComponent<CorrosiveFlameRuntime>().TickDamageMultiplier == 1f, "초월 제거 후 런타임 배율 초기화");
        }

        private static void SetNextTick(object effect, float time) => effect.GetType().GetField("nextTickTime", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, time);
        private static float GetNextTick(object effect) => (float)effect.GetType().GetField("nextTickTime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(effect);
        private static T Track<T>(T value) where T : Object { temporary.Add(value); return value; }
        private static void Require(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
            results.Add("PASS " + message);
        }
    }
}
