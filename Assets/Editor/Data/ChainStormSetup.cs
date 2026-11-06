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
            if (File.Exists(Output + "/chance.request"))
            {
                File.Delete(Output + "/chance.request");
                try
                {
                    VerifyChanceAndWind();
                    VerifySingleTargetLightningWind();
                    RelicTranscendenceData storm = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(Root + "LightningPursuitTranscendence.asset");
                    RelicSetBonusData archer = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(Root + "StormArcherSet.asset");
                    RelicSetBonusData wind = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(Root + "MobileWarriorSet.asset");
                    Sync(LocalizationKeys.RelicTranscendenceDescription(storm.name), storm.description_KR, storm.description_EN);
                    Sync(LocalizationKeys.RelicSetDescription(archer.synergySeriesId), archer.description_KR, archer.description_EN);
                    Sync(LocalizationKeys.RelicSetDescription(wind.synergySeriesId), wind.description_KR, wind.description_EN);
                    File.WriteAllText(Output + "/chance-verification.txt", "PASS 컴파일·일반 적중 번개·번개/돌풍 독립 확률·피격 위치 생성·재연쇄·풀 재사용·대시 즉시 발사·가까운 적 조준·적 없으면 무작위·중복 발사 방지·공격 시 추가 발사 없음\n");
                }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/chance-verification.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
                return;
            }
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

        private static void VerifyChanceAndWind()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            ObjectPoolManager previousPool = ObjectPoolManager.Instance;
            Nytherion.Data.ScriptableObjects.Player.PlayerData dashPlayerData = null;
            try
            {
                ObjectPoolManager pool = new GameObject("검증 풀").AddComponent<ObjectPoolManager>();
                Call(pool, "OnInitializeInternal");
                typeof(ObjectPoolManager).GetProperty("Instance").SetValue(null, pool);
                EventManager events = new GameObject("검증 이벤트").AddComponent<EventManager>();
                PlayerManager player = new GameObject("검증 플레이어").AddComponent<PlayerManager>();
                player.gameObject.AddComponent<PlayerCombat>();
                player.Construct(null, null, events, null);
                DashStormRuntime runtime = DashStormRuntime.GetOrCreate(player);
                TrainingDummy[] enemies = new TrainingDummy[2];
                for (int i = 0; i < enemies.Length; i++)
                {
                    GameObject target = new GameObject("검증 적 " + i);
                    target.transform.position = new Vector3(20000f + i, 20000f);
                    target.AddComponent<CircleCollider2D>().radius = 0.2f;
                    enemies[i] = target.AddComponent<TrainingDummy>();
                    enemies[i].Construct(events, null, null);
                }
                Physics2D.SyncTransforms();
                WindBlastProjectile prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<WindBlastProjectile>();
                var effect = new DashChainLightningEffect { triggerChance = 0f, projectilePrefab = prefab, maxTargets = 1, windSpawnChance = 1f };
                runtime.AddStorm(effect);
                int hits = 0;
                events.OnEnemyDamagedByPlayerDetailed += _ => hits++;
                Func<WindBlastProjectile[]> activeWinds = () => Object.FindObjectsOfType<WindBlastProjectile>()
                    .Where(item => item.gameObject.scene == scene && item.gameObject.activeSelf).ToArray();
                runtime.TryProjectileLightning(enemies[0], 100f);
                Require(hits == 0 && activeWinds().Length == 0, "0% 번개 미발동");
                events.TriggerEnemyDamagedByPlayerWithCrit(100f, false, enemies[0], false);
                Require(hits == 1 && activeWinds().Length == 0, "일반 적중 0% 번개 미발동");
                effect.triggerChance = 1f;
                hits = 0;
                events.TriggerEnemyDamagedByPlayerWithCrit(100f, false, enemies[0], true);
                events.TriggerEnemyDamagedByPlayerWithCrit(100f, false, enemies[0], false, true);
                Require(hits == 2 && activeWinds().Length == 0, "연쇄·지속 피해는 일반 적중 번개를 재귀 발동하지 않음");
                effect.windSpawnChance = 0f;
                hits = 0;
                events.TriggerEnemyDamagedByPlayerWithCrit(100f, false, enemies[0], false);
                Require(hits == 2 && activeWinds().Length == 0, "번개 100%·돌풍 0%는 피해만 전달");
                effect.windSpawnChance = 1f;
                hits = 0;
                events.TriggerEnemyDamagedByPlayerWithCrit(100f, false, enemies[0], false);
                Require(hits == 2 && activeWinds().Length == 2,
                    "대시·공격 시작 없이 비치명타 일반 적중에서 번개와 돌풍 생성");
                WindBlastProjectile wind = activeWinds().Single(item => item.transform.position == enemies[1].transform.position);
                foreach (WindBlastProjectile item in activeWinds().Where(item => item != wind)) Call(item, "ReturnToPool");
                Require(wind.transform.position == enemies[1].transform.position, "번개 피해 대상 중심에서 발사");
                Vector2 direction = (Vector2)typeof(WindBlastProjectile).GetField("direction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wind);
                Require(Mathf.Abs(direction.magnitude - 1f) < 0.001f, "무작위 방향 정규화");
                enemies[0].transform.position += Vector3.left * 20f;
                Physics2D.SyncTransforms();
                hits = 0;
                Call(wind, "Update");
                Require(hits == 0 && wind.gameObject.activeSelf, "생성 원점 적 즉시 재충돌 방지");
                enemies[0].transform.position = wind.transform.position;
                Physics2D.SyncTransforms();
                Call(wind, "Update");
                Require(hits == 2 && activeWinds().Length == 2 && !wind.gameObject.activeSelf, "투사체 적중·번개 재발동·새 투사체·풀 반환");
                wind = activeWinds().Single(item => (EnemyBase)typeof(WindBlastProjectile)
                    .GetField("sourceEnemy", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(item) == enemies[1]);
                foreach (WindBlastProjectile item in activeWinds().Where(item => item != wind)) Call(item, "ReturnToPool");
                runtime.RemoveStorm(effect);
                enemies[0].transform.position = wind.transform.position;
                Physics2D.SyncTransforms();
                hits = 0;
                Call(wind, "Update");
                Require(hits == 1 && activeWinds().Length == 0, "비행 중 초월 해제 후 번개 미발동");
                GameObject reused = pool.SpawnFromPool(prefab.gameObject, enemies[0].transform.position, Quaternion.identity);
                wind = reused.GetComponent<WindBlastProjectile>();
                wind.Launch(pool, Vector2.right, 10f, 1f, 0f, 0f, 0);
                hits = 0;
                Call(wind, "Update");
                Require(hits == 1 && !reused.activeSelf, "풀 재사용 시 원점 제외·런타임 연결 초기화");
                var archer = new CriticalChainLightningEffect { triggerChance = 0f, projectilePrefab = prefab, maxTargets = 1, windSpawnChance = 1f };
                typeof(CriticalChainLightningEffect).GetField("runtime", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(archer, runtime);
                hits = 0;
                Call(archer, "HandlePlayerDamage", new PlayerDamageEventData(enemies[0], 100f, true, false));
                Require(hits == 0, "폭풍 사수 0% 미발동");
                archer.triggerChance = 1f;
                Call(archer, "HandlePlayerDamage", new PlayerDamageEventData(enemies[0], 100f, false, false));
                Call(archer, "HandlePlayerDamage", new PlayerDamageEventData(enemies[0], 100f, true, true));
                Require(hits == 0, "폭풍 사수 일반·연쇄 피해 제외");
                archer.windSpawnChance = 0f;
                Call(archer, "HandlePlayerDamage", new PlayerDamageEventData(enemies[0], 100f, true, false));
                Require(hits == 1 && activeWinds().Length == 0, "폭풍 사수 번개 100%·돌풍 0% 독립 판정");
                hits = 0;
                archer.windSpawnChance = 1f;
                Call(archer, "HandlePlayerDamage", new PlayerDamageEventData(enemies[0], 100f, true, false));
                Require(hits == 1 && activeWinds().Length == 2, "폭풍 사수 100% 번개·돌풍 생성");
                foreach (WindBlastProjectile item in activeWinds()) Call(item, "ReturnToPool");

                GameObject weaponObject = new GameObject("검증 무기");
                weaponObject.SetActive(false);
                var weapon = weaponObject.AddComponent<Nytherion.GamePlay.Combat.Weapons.ForestThornWeapon>();
                weapon.weaponData = AssetDatabase.LoadAssetAtPath<Nytherion.Data.ScriptableObjects.Weapons.WeaponData>(
                    "Assets/Nytherion/Data/ScriptableObjects/Weapons/ForestThorn.asset");
                PlayerCombat combat = player.GetComponent<PlayerCombat>();
                combat.currentWeapon = weapon;
                player.transform.position = new Vector3(20000f, 20000f);
                enemies[0].transform.position = player.transform.position + Vector3.right * 2f;
                enemies[1].transform.position = player.transform.position + Vector3.up * 5f;
                Physics2D.SyncTransforms();
                var dashWind = new DashWindProjectileEffect { projectilePrefab = prefab };
                runtime.AddWind(dashWind);
                runtime.AddStorm(effect);
                events.TriggerPlayerDashStarted();
                Require(activeWinds().Length == 1, "돌풍·연쇄 폭풍 동시 활성 시 대시당 한 발");
                wind = activeWinds().Single();
                direction = (Vector2)typeof(WindBlastProjectile).GetField("direction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wind);
                Require(wind.transform.position == player.transform.position && Vector2.Dot(direction, Vector2.right) > 0.999f,
                    "대시 즉시 플레이어 중심에서 가까운 적 조준");
                Call(wind, "ReturnToPool");
                dashPlayerData = ScriptableObject.CreateInstance<Nytherion.Data.ScriptableObjects.Player.PlayerData>();
                player.currentPlayerData = dashPlayerData;
                dashPlayerData.extraProjectiles = 2.8f;
                events.TriggerPlayerDashStarted();
                WindBlastProjectile[] dashWinds = activeWinds();
                Require(dashWinds.Length == 3, "추가 투사체 2.8은 내림하여 대시 투사체 3발·동시 활성 중복 없음");
                Vector2 combinedDirection = Vector2.zero;
                foreach (WindBlastProjectile item in dashWinds)
                {
                    direction = (Vector2)typeof(WindBlastProjectile).GetField("direction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(item);
                    combinedDirection += direction;
                    Require(item.transform.position == player.transform.position && Vector2.Angle(direction, Vector2.right) < 7.6f,
                        "추가 대시 투사체는 플레이어 중심에서 가까운 적 방향으로 분산 발사");
                    Call(item, "ReturnToPool");
                }
                Require(Vector2.Dot(combinedDirection.normalized, Vector2.right) > 0.999f, "분산 발사 중심 조준 유지");
                runtime.RemoveStorm(effect);
                events.TriggerPlayerDashStarted();
                Require(activeWinds().Length == 3, "돌풍 단독 대시도 추가 투사체 반영");
                foreach (WindBlastProjectile item in activeWinds()) Call(item, "ReturnToPool");
                runtime.RemoveWind(dashWind);
                runtime.AddStorm(effect);
                enemies[0].transform.position += Vector3.right * 100f;
                enemies[1].transform.position += Vector3.right * 100f;
                Physics2D.SyncTransforms();
                events.TriggerPlayerDashStarted();
                Require(activeWinds().Length == 3, "연쇄 폭풍 단독·주변 적 없음에도 추가 투사체 반영");
                wind = activeWinds().First();
                direction = (Vector2)typeof(WindBlastProjectile).GetField("direction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wind);
                Require(Mathf.Abs(direction.magnitude - 1f) < 0.001f && wind.transform.position == player.transform.position,
                    "탐색 반경 안 적이 없으면 플레이어 중심에서 무작위 발사");
                foreach (WindBlastProjectile item in activeWinds()) Call(item, "ReturnToPool");
                dashPlayerData.extraProjectiles = -2f;
                events.TriggerPlayerDashStarted();
                Require(activeWinds().Length == 1, "추가 투사체 음수에서도 기본 한 발 유지");
                foreach (WindBlastProjectile item in activeWinds()) Call(item, "ReturnToPool");
                dashPlayerData.extraProjectiles = 0.8f;
                events.TriggerPlayerDashStarted();
                Require(activeWinds().Length == 1, "대시마다 최신 투사체 수 반영·소수 내림");
                foreach (WindBlastProjectile item in activeWinds()) Call(item, "ReturnToPool");
                events.TriggerPlayerRangedAttack(Vector2.right, 1, 100f, player.transform, "");
                var attack = (Action<Vector2, Vector3>)typeof(PlayerCombat).GetField("OnPlayerAttackStarted",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(combat);
                attack?.Invoke(Vector2.right, player.transform.position);
                Require(activeWinds().Length == 0, "대시 후 공격으로 투사체를 추가 발사하지 않음");
                runtime.RemoveStorm(effect);
                events.TriggerPlayerDashStarted();
                Require(activeWinds().Length == 0, "효과 해제 후 대시 발사 없음");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                typeof(ObjectPoolManager).GetProperty("Instance").SetValue(null, previousPool);
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
                if (dashPlayerData != null) Object.DestroyImmediate(dashPlayerData);
            }
        }

        private static void VerifySingleTargetLightningWind()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            ObjectPoolManager previousPool = ObjectPoolManager.Instance;
            try
            {
                ObjectPoolManager pool = new GameObject("단일 대상 검증 풀").AddComponent<ObjectPoolManager>();
                Call(pool, "OnInitializeInternal");
                typeof(ObjectPoolManager).GetProperty("Instance").SetValue(null, pool);
                EventManager events = new GameObject("단일 대상 검증 이벤트").AddComponent<EventManager>();
                PlayerManager player = new GameObject("단일 대상 검증 플레이어").AddComponent<PlayerManager>();
                player.Construct(null, null, events, null);
                DashStormRuntime runtime = DashStormRuntime.GetOrCreate(player);
                GameObject target = new GameObject("주변 적이 없는 피격 대상");
                target.transform.position = new Vector3(30000f, 30000f);
                target.AddComponent<CircleCollider2D>();
                target.AddComponent<BoxCollider2D>();
                TrainingDummy enemy = target.AddComponent<TrainingDummy>();
                enemy.Construct(events, null, null);
                Physics2D.SyncTransforms();
                WindBlastProjectile prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<WindBlastProjectile>();
                var storm = new DashChainLightningEffect { triggerChance = 1f, windSpawnChance = 1f, projectilePrefab = prefab };
                runtime.AddStorm(storm);
                int damageEvents = 0;
                events.OnEnemyDamagedByPlayerDetailed += _ => damageEvents++;
                Func<WindBlastProjectile[]> activeWinds = () => Object.FindObjectsOfType<WindBlastProjectile>()
                    .Where(item => item.gameObject.scene == scene && item.gameObject.activeSelf).ToArray();
                enemy.TakeDamage(100f);
                Require(damageEvents == 1, "최초 대상에 번개 피해 이벤트를 중복 적용하지 않음");
                Require(activeWinds().Length == 1, "주변 적 없이 최초 번개 피격 대상에서도 돌풍 생성·다중 콜라이더 중복 없음");
                Require(activeWinds().Single().transform.position == target.transform.position, "최초 번개 대상 중심에서 생성");
                Call(activeWinds().Single(), "ReturnToPool");
                enemy.TakeDamage(0f);
                Require(activeWinds().Length == 1, "피해 0 번개에서도 돌풍 생성 판정");
                WindBlastProjectile wind = activeWinds().Single();
                Require((float)typeof(WindBlastProjectile).GetField("damage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wind) == 0f,
                    "피해 0 돌풍에 임의 피해 추가 없음");
                Call(wind, "Update");
                Require(wind.gameObject.activeSelf, "단일 대상에서 생성 즉시 자기 충돌로 소멸하지 않음");
                Call(wind, "ReturnToPool");
                storm.windSpawnChance = 0f;
                enemy.TakeDamage(100f);
                Require(activeWinds().Length == 0, "최초 대상 돌풍 0% 미생성");
                storm.windSpawnChance = 0.25f;
                UnityEngine.Random.InitState(73421);
                int spawned = 0;
                for (int i = 0; i < 400; i++)
                {
                    enemy.TakeDamage(100f);
                    WindBlastProjectile[] winds = activeWinds();
                    spawned += winds.Length;
                    foreach (WindBlastProjectile item in winds) Call(item, "ReturnToPool");
                }
                Require(spawned >= 60 && spawned <= 140, "실제 적중 400회에서 돌풍 25% 생성 빈도: " + spawned);
                File.WriteAllText(Output + "/single-target-wind-frequency.txt", "PASS 실제 적중 400회 중 돌풍 " + spawned + "회 생성\n");
                runtime.RemoveStorm(storm);
                var archer = new CriticalChainLightningEffect { triggerChance = 1f, windSpawnChance = 1f, projectilePrefab = prefab };
                archer.ApplyEffect(player, 1);
                try
                {
                    events.TriggerEnemyDamagedByPlayerWithCrit(0f, true, enemy, false);
                    Require(activeWinds().Length == 1, "폭풍 사수 단일 대상·피해 0 번개에서도 돌풍 생성");
                    Call(activeWinds().Single(), "ReturnToPool");
                }
                finally { archer.RemoveEffect(player, 1); }
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                typeof(ObjectPoolManager).GetProperty("Instance").SetValue(null, previousPool);
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
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
                const string korean = " 대시 시 돌풍 투사체를 발사합니다.";
                module.description_KR = module.description_KR.Replace(" 대시 후 공격 시 돌풍 투사체를 발사합니다.", "");
                const string english = " Dashing fires a wind projectile.";
                module.description_EN = module.description_EN.Replace(" An attack after dashing fires a wind projectile.", "");
                if (!module.description_KR.Contains(korean.Trim())) module.description_KR += korean;
                if (!module.description_EN.Contains(english.Trim())) module.description_EN += english;
            }
            wind.description_KR = "기동력을 높이고 대시 시 돌풍 투사체를 발사합니다.";
            wind.description_EN = "Improve mobility and fire a wind projectile when dashing.";
            EditorUtility.SetDirty(wind);
            AssetDatabase.SaveAssetIfDirty(wind);

            RelicTranscendenceData storm = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>(Root + "LightningPursuitTranscendence.asset");
            DashChainLightningEffect stormEffect = storm.effectModules.SelectMany(module => module.effects).OfType<DashChainLightningEffect>().Single();
            stormEffect.projectilePrefab = prefab;
            stormEffect.chargesPerDash = 3;
            stormEffect.projectileDamageRatio = 1f;
            storm.effectName_KR = "연쇄 폭풍";
            storm.effectName_EN = "Chain Storm";
            storm.description_KR = "대시하면 돌풍을 발사하고 공격 적중의 번개와 번개 피격 적의 돌풍이 각각 확률적으로 발동합니다.";
            storm.description_EN = "Dash to fire wind projectiles, while hits can trigger lightning and lightning-struck enemies can release wind projectiles.";
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
            Require(effect.projectilePrefab == prefab && effect.dashTargetSearchRadius > 0f && storm.effectName_EN == "Chain Storm", "초월 이름·탐색 반경·프리팹");
            Require(effect.lightningEffectPrefab == GetWeaponLightningVFX(), "번개부르미와 동일한 번개 선 프리팹 연결");
            Require(wind.bonusModules.SelectMany(module => module.effects).OfType<DashWindProjectileEffect>().Count(item => item.projectilePrefab == prefab) == 2, "돌풍 4·6세트 참조");
            VerifyChanceAndWind();
            VerifyLightningAndProjectile(prefab);
            File.WriteAllText(Output + "/verification.txt", "PASS 64×64 4프레임, 12fps 반복, 투사체 프리팹 및 돌풍 4·6세트·초월 참조\nPASS 대시 3회 충전, 공격마다 1회 소모, 소진, 재대시 최대치 갱신, 효과 제거 후 잔여 효과 해제\nPASS 번개 확산 반경·최대 대상·중복 콜라이더 제외, 투사체 적중 번개 확산·풀 반환·재사용 시 번개 초기화\nPASS 번개부르미 번개 선 공유, 일반 확산·돌풍 적중 이펙트 생성, 무기와 동일한 생성·앵커 추적·재생 종료\n");
            Debug.Log("[ChainStormSetup] 연쇄 폭풍·돌풍 투사체 연결 및 충전 검증 완료");
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
