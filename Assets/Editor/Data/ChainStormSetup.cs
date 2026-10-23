using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ChainStormSetup
    {
        private const string Output = "output/chain-storm";
        private const string Root = "Assets/Nytherion/Data/ScriptableObjects/Relics/";
        private const string Sheet = "Assets/Nytherion/Art/Combat/WindBlastProj.png";
        private const string Clip = "Assets/Nytherion/Art/Combat/VFX/Animations/WindBlastProjectile.anim";
        private const string Controller = "Assets/Nytherion/Art/Combat/VFX/Animations/WindBlastProjectile.controller";
        private const string Prefab = "Assets/Prefabs/Gameplay/Combat/Projectiles/WindBlastProjectile.prefab";

        static ChainStormSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/recompile.request"))
            {
                File.Delete(Output + "/recompile.request");
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(UnityEditor.Compilation.RequestScriptCompilationOptions.CleanBuildCache);
                return;
            }
            if (File.Exists(Output + "/archer-vfx.request"))
            {
                File.Delete(Output + "/archer-vfx.request");
                try
                {
                    ConnectStormArcherVFX();
                    File.WriteAllText(Output + "/archer-reference-verification.txt", "PASS 폭풍사수 4·6세트에 번개부르미와 동일한 번개 선 프리팹 연결\n");
                }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/archer-reference-verification.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
                return;
            }
            if (File.Exists(Output + "/vfx.request"))
            {
                File.Delete(Output + "/vfx.request");
                try { ConnectLightningVFX(); Verify(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
                return;
            }
            if (File.Exists(Output + "/rename.request"))
            {
                File.Delete(Output + "/rename.request");
                try { RenameStormIcon(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/rename.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
                return;
            }
            if (!File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        private static void RenameStormIcon()
        {
            RelicTranscendenceData storm = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(Root + "LightningPursuitTranscendence.asset");
            Sprite icon = storm.statusIcon;
            string source = AssetDatabase.GetAssetPath(icon);
            const string destination = "Assets/Nytherion/Art/UI/Relics/ChainStormIcon.png";
            Require(icon != null && (source == "Assets/Nytherion/Art/UI/Relics/ChainLightningIcon.png" || source == destination), "연쇄 폭풍 아이콘 경로");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(icon, out string guid, out long localId);
            if (source != destination)
            {
                Require(!File.Exists(destination), "아이콘 이름 충돌 없음");
                string error = AssetDatabase.RenameAsset(source, "ChainStormIcon");
                Require(string.IsNullOrEmpty(error), "아이콘 이름 변경: " + error);
            }
            Sprite renamed = AssetDatabase.LoadAssetAtPath<Sprite>(destination);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renamed, out string newGuid, out long newLocalId);
            Require(guid == newGuid && localId == newLocalId && storm.statusIcon == renamed, "아이콘 GUID·로컬 ID·초월 참조 보존");
            File.WriteAllText(Output + "/rename.txt", "PASS ChainLightningIcon.png → ChainStormIcon.png\nPASS GUID·로컬 ID·연쇄 폭풍 아이콘 참조 보존\n");
        }

        [MenuItem("Tools/Nytherion/유물/연쇄 폭풍·돌풍 투사체 설정")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);
            if (sheet == null || sheet.width != 256 || sheet.height != 64) throw new InvalidOperationException("돌풍 시트는 64×64 프레임 4장이어야 합니다.");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(64, 64), 64f);
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clip);
            if (clip == null)
            {
                clip = new AnimationClip { name = "WindBlastProjectile" };
                AssetDatabase.CreateAsset(clip, Clip);
            }
            clip.frameRate = 12f;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"),
                frames.Select((sprite, index) => new ObjectReferenceKeyframe { time = index / 12f, value = sprite }).ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.startTime = 0f;
            settings.stopTime = 4f / 12f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(Controller);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == "WindBlast");
            if (state == null) state = machine.AddState("WindBlast");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            string folder = Path.GetDirectoryName(Prefab).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Gameplay/Combat", "Projectiles");
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (existing != null && existing.GetComponent<WindBlastProjectile>() == null) throw new InvalidOperationException("다른 용도의 프리팹이 있습니다.");
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            WindBlastProjectile prefab;
            try
            {
                root = existing != null ? PrefabUtility.LoadPrefabContents(Prefab) : new GameObject("WindBlastProjectile");
                if (existing == null) SceneManager.MoveGameObjectToScene(root, preview);
                WindBlastProjectile projectile = root.GetComponent<WindBlastProjectile>() ?? root.AddComponent<WindBlastProjectile>();
                SerializedObject serialized = new SerializedObject(projectile);
                serialized.FindProperty("obstacleLayers").intValue = LayerMask.GetMask("Wall", "Obstacle", "map");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sortingOrder = 100;
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                root.GetComponent<Animator>().runtimeAnimatorController = controller;
                root.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefab).GetComponent<WindBlastProjectile>();
            }
            finally
            {
                if (existing != null && root != null) PrefabUtility.UnloadPrefabContents(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }

            RelicSetBonusData wind = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(Root + "MobileWarriorSet.asset");
            foreach (RelicEffectModule module in wind.bonusModules)
            {
                ChainSynergyCondition condition = module.condition as ChainSynergyCondition;
                if (condition == null || condition.requiredChainLength < 4) continue;
                DashWindProjectileEffect effect = module.effects.OfType<DashWindProjectileEffect>().FirstOrDefault();
                if (effect == null) { effect = new DashWindProjectileEffect(); module.effects.Add(effect); }
                effect.projectilePrefab = prefab;
                effect.chargesPerDash = 1;
                effect.damageRatio = condition.requiredChainLength >= 6 ? 1f : 0.75f;
                // 기존 단계 능력치 설명은 보존하고 추가 효과를 표시합니다.
                const string korean = " 대시 후 공격 시 돌풍 투사체를 발사합니다.";
                const string english = " An attack after dashing fires a wind projectile.";
                if (!module.description_KR.Contains(korean.Trim())) module.description_KR += korean;
                if (!module.description_EN.Contains(english.Trim())) module.description_EN += english;
            }
            wind.description_KR = "기동력을 높이고 대시 후 공격을 강화하며 돌풍 투사체를 발사합니다.";
            wind.description_EN = "Improve mobility and empower attacks after dashing with a wind projectile.";
            EditorUtility.SetDirty(wind);
            AssetDatabase.SaveAssetIfDirty(wind);

            RelicTranscendenceData storm = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(Root + "LightningPursuitTranscendence.asset");
            DashChainLightningEffect stormEffect = storm.effectModules.SelectMany(module => module.effects).OfType<DashChainLightningEffect>().Single();
            stormEffect.projectilePrefab = prefab;
            stormEffect.chargesPerDash = 3;
            stormEffect.projectileDamageRatio = 1f;
            storm.effectName_KR = "연쇄 폭풍";
            storm.effectName_EN = "Chain Storm";
            storm.description_KR = "대시로 충전한 공격은 주변에 번개를 퍼뜨리고, 적중 시 번개가 확산되는 돌풍 투사체를 발사합니다.";
            storm.description_EN = "Dashing charges attacks that spread lightning and fire a wind projectile that also spreads lightning on impact.";
            foreach (RelicEffectModule module in storm.effectModules)
            {
                module.description_KR = storm.description_KR;
                module.description_EN = storm.description_EN;
            }
            EditorUtility.SetDirty(storm);
            AssetDatabase.SaveAssetIfDirty(storm);
            Sync(LocalizationKeys.RelicTranscendenceName(storm.name), storm.effectName_KR, storm.effectName_EN);
            Sync(LocalizationKeys.RelicTranscendenceDescription(storm.name), storm.description_KR, storm.description_EN);
            Sync(LocalizationKeys.RelicSetDescription(wind.synergySeriesId), wind.description_KR, wind.description_EN);
            ConnectLightningVFX();
            Verify();
        }

        private static ChainLightningVFX GetWeaponLightningVFX()
        {
            GameObject weaponProjectile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Combat/Proj/StormbringerProj.prefab");
            LightningProj lightning = weaponProjectile != null ? weaponProjectile.GetComponent<LightningProj>() : null;
            Require(lightning != null, "번개부르미 투사체 참조");
            ChainLightningVFX effect = new SerializedObject(lightning).FindProperty("lightningEffectPrefab").objectReferenceValue as ChainLightningVFX;
            Require(effect != null && effect.GetComponent<LineRenderer>() != null, "번개부르미 번개 선 프리팹");
            return effect;
        }

        private static void ConnectLightningVFX()
        {
            RelicTranscendenceData storm = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(Root + "LightningPursuitTranscendence.asset");
            DashChainLightningEffect effect = storm.effectModules.SelectMany(module => module.effects).OfType<DashChainLightningEffect>().Single();
            effect.lightningEffectPrefab = GetWeaponLightningVFX();
            EditorUtility.SetDirty(storm);
            AssetDatabase.SaveAssetIfDirty(storm);
        }

        private static void ConnectStormArcherVFX()
        {
            RelicSetBonusData archer = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(Root + "StormArcherSet.asset");
            CriticalChainLightningEffect[] effects = archer.bonusModules.SelectMany(module => module.effects)
                .OfType<CriticalChainLightningEffect>().ToArray();
            Require(effects.Length == 2, "폭풍사수 4·6세트 치명타 번개 모듈");
            ChainLightningVFX prefab = GetWeaponLightningVFX();
            foreach (CriticalChainLightningEffect effect in effects) effect.lightningEffectPrefab = prefab;
            EditorUtility.SetDirty(archer);
            AssetDatabase.SaveAssetIfDirty(archer);
            Require(effects.All(effect => effect.lightningEffectPrefab == prefab), "폭풍사수 번개부르미 이펙트 참조");
        }

        private static void Sync(string key, string korean, string english)
        {
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

        public static void Verify()
        {
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet));
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clip);
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"));
            WindBlastProjectile prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<WindBlastProjectile>();
            RelicTranscendenceData storm = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(Root + "LightningPursuitTranscendence.asset");
            DashChainLightningEffect effect = storm.effectModules.SelectMany(module => module.effects).OfType<DashChainLightningEffect>().Single();
            RelicSetBonusData wind = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(Root + "MobileWarriorSet.asset");
            Require(frames.Length == 4 && keys.Length == 4 && keys.Select((key, index) => key.value == frames[index]).All(value => value), "4프레임 연결");
            Require(AnimationUtility.GetAnimationClipSettings(clip).loopTime && Mathf.Abs(clip.length - 4f / 12f) < 0.001f, "12fps 반복 애니메이션");
            Require(effect.projectilePrefab == prefab && effect.chargesPerDash == 3 && storm.effectName_EN == "Chain Storm", "초월 이름·충전·프리팹");
            Require(effect.lightningEffectPrefab == GetWeaponLightningVFX(), "번개부르미와 동일한 번개 선 프리팹 연결");
            Require(wind.bonusModules.SelectMany(module => module.effects).OfType<DashWindProjectileEffect>().Count(item => item.projectilePrefab == prefab) == 2, "돌풍 4·6세트 참조");
            VerifyCharges();
            VerifyLightningAndProjectile(prefab);
            File.WriteAllText(Output + "/verification.txt", "PASS 64×64 4프레임, 12fps 반복, 투사체 프리팹 및 돌풍 4·6세트·초월 참조\nPASS 대시 3회 충전, 공격마다 1회 소모, 소진, 재대시 최대치 갱신, 효과 제거 후 잔여 효과 해제\nPASS 번개 확산 반경·최대 대상·중복 콜라이더 제외, 투사체 적중 번개 확산·풀 반환·재사용 시 번개 초기화\nPASS 번개부르미 번개 선 공유, 일반 확산·돌풍 적중 이펙트 생성, 무기와 동일한 생성·앵커 추적·재생 종료\n");
            Debug.Log("[ChainStormSetup] 연쇄 폭풍·돌풍 투사체 연결 및 충전 검증 완료");
        }

        private static void VerifyCharges()
        {
            GameObject test = new GameObject("[연쇄 폭풍 충전 검증]");
            GameObject combatObject = new GameObject("[연쇄 폭풍 비활성 전투 객체]");
            combatObject.SetActive(false);
            try
            {
                PlayerCombat combat = combatObject.AddComponent<PlayerCombat>();
                DashStormRuntime runtime = test.AddComponent<DashStormRuntime>();
                typeof(DashStormRuntime).GetField("combat", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(runtime, combat);
                var charges = (System.Collections.Generic.Dictionary<DashChainLightningEffect, int>)typeof(DashStormRuntime)
                    .GetField("stormCharges", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime);
                var windCharges = (System.Collections.Generic.Dictionary<DashWindProjectileEffect, int>)typeof(DashStormRuntime)
                    .GetField("windCharges", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime);
                DashChainLightningEffect effect = new DashChainLightningEffect();
                DashWindProjectileEffect wind = new DashWindProjectileEffect();
                charges.Add(effect, 0);
                windCharges.Add(wind, 0);
                Call(runtime, "Recharge");
                Require(runtime.RemainingStormCharges == 3 && windCharges[wind] == 1, "대시 충전");
                Call(runtime, "HandleAttack", Vector2.right, Vector3.zero);
                Require(runtime.RemainingStormCharges == 2 && windCharges[wind] == 0, "공격당 충전 하나 소모");
                Call(runtime, "HandleAttack", Vector2.right, Vector3.zero);
                Call(runtime, "HandleAttack", Vector2.right, Vector3.zero);
                Call(runtime, "HandleAttack", Vector2.right, Vector3.zero);
                Require(runtime.RemainingStormCharges == 0, "충전 소진 및 음수 방지");
                Call(runtime, "Recharge");
                Call(runtime, "Recharge");
                Require(runtime.RemainingStormCharges == 3, "재대시 충전 누적 방지");
                Call(runtime, "HandleAttack", Vector2.right, Vector3.zero);
                runtime.RemoveStorm(effect);
                runtime.RemoveWind(wind);
                Require(runtime.RemainingStormCharges == 0 && ((System.Collections.ICollection)typeof(DashStormRuntime)
                    .GetField("pendingHits", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime)).Count == 0, "효과 해제 후 잔여 상태 제거");
                test.SetActive(false);
                charges.Add(effect, 0);
                Call(runtime, "Recharge");
                Require(runtime.RemainingStormCharges == 0, "비활성 상태 충전 방지");
            }
            finally { Object.DestroyImmediate(test); Object.DestroyImmediate(combatObject); }
        }

        private static void VerifyLightningAndProjectile(WindBlastProjectile prefab)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                GameObject eventObject = new GameObject("검증 이벤트");
                EventManager events = eventObject.AddComponent<EventManager>();
                TrainingDummy[] enemies = new TrainingDummy[4];
                for (int i = 0; i < enemies.Length; i++)
                {
                    GameObject target = new GameObject("검증 대상 " + i);
                    target.transform.position = new Vector3(20000f + (i == 3 ? 20f : i), 20000f);
                    target.AddComponent<CircleCollider2D>().radius = 0.2f;
                    enemies[i] = target.AddComponent<TrainingDummy>();
                    enemies[i].Construct(events, null, null);
                }
                enemies[1].gameObject.AddComponent<BoxCollider2D>().size = Vector2.one * 0.2f;
                Physics2D.SyncTransforms();
                int hits = 0;
                bool onlySecondary = true;
                float total = 0f;
                events.OnEnemyDamagedByPlayerDetailed += data =>
                {
                    hits++;
                    total += data.DamageAmount;
                    onlySecondary &= data.IsChainDamage;
                };
                DashStormRuntime.SpreadLightning(enemies[0], 45f, 6f, 2);
                Require(hits == 2 && total == 90f && onlySecondary, "번개 반경·중복 콜라이더·최대 대상·연쇄 피해 플래그");
                hits = 0;
                total = 0f;
                DashStormRuntime.SpreadLightning(enemies[0], 45f, 6f, 1);
                Require(hits == 1 && total == 45f, "번개 최대 대상 하나");

                ObjectPoolManager pool = new GameObject("검증 풀").AddComponent<ObjectPoolManager>();
                Call(pool, "OnInitializeInternal");
                ChainLightningVFX lightningPrefab = GetWeaponLightningVFX();
                DashStormRuntime.SpreadLightning(enemies[0], 45f, 6f, 2, lightningPrefab);
                VerifyTrackingLightning(enemies[0].transform, enemies[2].transform);
                hits = 0;
                total = 0f;
                DashStormRuntime.SpreadLightning(enemies[0], 0f, 6f, 2, lightningPrefab);
                Require(hits == 0 && total == 0f, "피해 0 번개는 추가 피해 이벤트 없이 연출 재생");
                VerifyTrackingLightning(enemies[0].transform, enemies[2].transform);
                DashStormRuntime.SpreadLightning(enemies[0], 0f, 6f, 0, lightningPrefab);
                Require(!Object.FindObjectsOfType<ChainLightningVFX>().Any(item => item.name == "ChainStormLightningVFX" && item.IsVisible), "초월 대상 수 0이면 피해량과 무관하게 번개 미발동");
                GameObject instance = pool.SpawnFromPool(prefab.gameObject, enemies[0].transform.position, Quaternion.identity, 1);
                WindBlastProjectile projectile = instance.GetComponent<WindBlastProjectile>();
                projectile.Launch(pool, Vector2.right, 100f, 1f, 0.45f, 6f, 2, lightningPrefab);
                hits = 0;
                total = 0f;
                Physics2D.SyncTransforms();
                Call(projectile, "Update");
                Require(hits == 3 && total == 190f && !instance.activeSelf && onlySecondary, "투사체 직접 적중 및 번개 확산 후 풀 반환");
                VerifyTrackingLightning(enemies[0].transform, enemies[2].transform);
                instance = pool.SpawnFromPool(prefab.gameObject, enemies[0].transform.position, Quaternion.identity, 1);
                projectile = instance.GetComponent<WindBlastProjectile>();
                projectile.Launch(pool, Vector2.right, 100f, 1f, 0f, 0f, 0);
                hits = 0;
                total = 0f;
                Physics2D.SyncTransforms();
                Call(projectile, "Update");
                Require(hits == 1 && total == 100f && !instance.activeSelf, "풀 재사용 시 초월 번개 잔류 방지");
                Require(!Object.FindObjectsOfType<ChainLightningVFX>().Any(item => item.name == "ChainStormLightningVFX" && item.IsVisible), "일반 돌풍에서 번개 선 잔류 없음");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void VerifyTrackingLightning(Transform origin, Transform target)
        {
            ChainLightningVFX effect = Object.FindObjectsOfType<ChainLightningVFX>()
                .Single(item => item.name == "ChainStormLightningVFX" && item.IsVisible);
            LineRenderer line = effect.GetComponent<LineRenderer>();
            VerifyLightningRendering(effect, line);
            Vector3 start = origin.position;
            Vector3 end = target.position;
            Require(effect.IsVisible && line.positionCount >= 3 && line.GetPosition(0) == start &&
                line.GetPosition(line.positionCount - 1) == end, "번개부르미와 동일한 앵커 경로");
            origin.position += Vector3.up;
            target.position += Vector3.up;
            try
            {
                Call(effect, "UpdateTargetsPositions");
                Require(line.GetPosition(0) == origin.position && line.GetPosition(line.positionCount - 1) == target.position,
                    "번개부르미와 동일한 적 위치 추적");
            }
            finally { origin.position = start; target.position = end; }
            typeof(ChainLightningVFX).GetField("elapsed", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, effect.duration + 1f);
            Call(effect, "Update");
            Require(effect.gameObject.activeSelf && effect.IsVisible, "적중 프레임 첫 렌더 유지");
            typeof(ChainLightningVFX).GetField("setupFrame", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, Time.frameCount - 1);
            Call(effect, "Update");
            Require(!effect.IsVisible, "번개부르미와 동일한 재생 종료");
            Object.DestroyImmediate(effect.gameObject);
        }
        private static void VerifyLightningRendering(ChainLightningVFX effect, LineRenderer line)
        {
            GameObject cameraObject = new GameObject("[번개 렌더 검증 카메라]");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;
            camera.transform.position = (line.GetPosition(0) + line.GetPosition(line.positionCount - 1)) * 0.5f + Vector3.back * 10f;
            RenderTexture buffer = RenderTexture.GetTemporary(256, 128, 24, RenderTextureFormat.ARGB32);
            Texture2D readback = new Texture2D(256, 128, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            Transform[] transforms = effect.GetComponentsInChildren<Transform>(true);
            int[] originalLayers = transforms.Select(item => item.gameObject.layer).ToArray();
            try
            {
                foreach (Transform item in transforms) item.gameObject.layer = 30;
                camera.targetTexture = buffer;
                int after = CountRenderedPixels(camera, buffer, readback);
                File.WriteAllText(Output + "/render-visibility.txt", $"PASS 실제 URP 번개 표시 픽셀={after}\n");
                Require(after > 10, "실제 URP 렌더러에서 번개 픽셀 출력");
            }
            finally
            {
                for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = originalLayers[i];
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(buffer);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static int CountRenderedPixels(Camera camera, RenderTexture buffer, Texture2D readback)
        {
            camera.Render();
            RenderTexture.active = buffer;
            readback.ReadPixels(new Rect(0, 0, buffer.width, buffer.height), 0, 0);
            readback.Apply();
            return readback.GetPixels32().Count(pixel => pixel.r > 15 || pixel.g > 15 || pixel.b > 15);
        }
        private static void Call(object target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        private static void Require(bool success, string label)
        {
            if (!success) throw new InvalidOperationException("연쇄 폭풍 검증 실패: " + label);
        }
    }
}
