using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 사용자 입력과 렌더 직전 상태만 기록하며 게임 상태를 변경하지 않습니다.</summary>
    [InitializeOnLoad]
    public static class ChainStormLiveTrace
    {
        private const string Output = "output/chain-storm/live-trace.txt";
        private const string Enabled = "Nytherion.ChainStorm.LiveTrace";
        private static readonly Queue<string> history = new Queue<string>();
        private static readonly Dictionary<int, string> rendererStates = new Dictionary<int, string>();
        private static PlayerManager player;
        private static PlayerCombat combat;
        private static EventManager events;
        private static bool wasPlaying;
        private static bool dirty;

        static ChainStormLiveTrace()
        {
            EditorApplication.update += Update;
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            AssemblyReloadEvents.beforeAssemblyReload += Detach;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists("output/chain-storm/snapshot.request"))
            {
                File.Delete("output/chain-storm/snapshot.request");
                SnapshotReferences();
            }
            if (dirty)
            {
                Directory.CreateDirectory("output/chain-storm");
                File.WriteAllLines(Output, history);
                dirty = false;
            }
            if (File.Exists("output/chain-storm/trace.request"))
            {
                File.Delete("output/chain-storm/trace.request");
                SessionState.SetBool(Enabled, true);
                history.Clear();
                rendererStates.Clear();
                Record("진단 시작: 실제 사용자 입력만 관찰. 게임 효과·충전·씬·카메라는 변경하지 않음.");
            }
            if (File.Exists("output/chain-storm/trace-stop.request"))
            {
                File.Delete("output/chain-storm/trace-stop.request");
                SessionState.SetBool(Enabled, false);
                Detach();
            }
            if (!SessionState.GetBool(Enabled, false)) return;
            if (!EditorApplication.isPlaying)
            {
                if (wasPlaying) { Record("플레이 종료"); Detach(); }
                wasPlaying = false;
                return;
            }
            wasPlaying = true;
            PlayerManager current = Object.FindObjectOfType<PlayerManager>();
            if (current != null && (current != player || events == null))
            {
                Detach();
                player = current;
                combat = player.PlayerCombat;
                events = player.EventManager;
                if (combat == null || events == null) { player = null; return; }
                events.OnPlayerDashStarted += Dash;
                events.OnPlayerRangedAttack += Ranged;
                events.OnEnemyDamagedByPlayerDetailed += Damage;
                combat.OnPlayerAttackStarted += Attack;
                Record("실제 플레이어 연결: " + player.name + " / " + RuntimeState());
            }
        }

        private static void Dash() => Record("대시: " + RuntimeState());
        private static void Attack(Vector2 direction, Vector3 target) => Record($"공격 시작 방향={direction}: {RuntimeState()}");
        private static void Ranged(Vector2 direction, int count, float damage, Transform point, string tag) => Record($"원거리 발사 {tag}, 탄 수={count}, 피해량={damage}: {RuntimeState()}");
        private static void Damage(PlayerDamageEventData data)
        {
            string targets = data.Target == null ? "" : string.Join(",", Physics2D.OverlapCircleAll(data.Target.transform.position, 6f)
                .Select(hit => hit.GetComponentInParent<EnemyBase>()).Where(enemy => enemy != null).Distinct()
                .Select(enemy => enemy.name + (enemy.isDead ? "(사망)" : "(생존)")));
            Record($"피해 대상={data.Target?.name}, 피해량={data.DamageAmount}, 지속피해={data.IsDamageOverTime}, 치명타={data.IsCritical}, 연쇄={data.IsChainDamage}, 주변=[{targets}]: {RuntimeState()}");
            ObserveRenderers(Camera.main, "적중 이벤트 종료");
        }

        private static string RuntimeState()
        {
            DashStormRuntime runtime = player != null ? player.GetComponent<DashStormRuntime>() : null;
            string state = runtime == null ? "폭풍 런타임 없음" :
                $"폭풍 모듈={DictionaryCount(runtime, "stormEffects")}, 돌풍 모듈={DictionaryCount(runtime, "windEffects")}";
            WindBlastProjectile[] winds = Object.FindObjectsOfType<WindBlastProjectile>();
            return state + "; 돌풍=[" + string.Join(",", winds.Select(wind =>
                $"{wind.GetInstanceID()} 피해량={Field(wind, "damage")} 번개배율={Field(wind, "chainRatio")} 대상수={Field(wind, "chainTargets")} 프리팹={Field(wind, "lightningPrefab")}")) + "]";
        }

        private static void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!SessionState.GetBool(Enabled, false) || !Application.isPlaying || camera != Camera.main) return;
            ObserveRenderers(camera, "실제 메인 카메라 렌더 직전");
        }

        private static void ObserveRenderers(Camera camera, string phase)
        {
            foreach (ChainLightningVFX effect in Resources.FindObjectsOfTypeAll<ChainLightningVFX>()
                .Where(item => item.gameObject.scene.IsValid() && item.gameObject.scene.isLoaded))
            {
                LineRenderer line = effect.GetComponent<LineRenderer>();
                Material material = line.sharedMaterial;
                Vector3 first = line.positionCount > 0 ? line.GetPosition(0) : Vector3.zero;
                Vector3 last = line.positionCount > 0 ? line.GetPosition(line.positionCount - 1) : Vector3.zero;
                bool cameraLayer = camera != null && (camera.cullingMask & (1 << effect.gameObject.layer)) != 0;
                bool inFrustum = camera != null && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), line.bounds);
                string state = $"{effect.name} id={effect.GetInstanceID()}, 객체활성={effect.gameObject.activeSelf}/{effect.gameObject.activeInHierarchy}, 컴포넌트활성={effect.enabled}, 활성={line.enabled}, 정점={line.positionCount}, " +
                    $"시작={first}, 끝={last}, scale={line.transform.lossyScale}, width={line.widthMultiplier}, " +
                    $"재질={material?.name}, shader={material?.shader?.name}, queue={material?.renderQueue}, " +
                    $"sort={line.sortingLayerName}/{line.sortingOrder}, 카메라 레이어={cameraLayer}, 카메라 영역={inFrustum}, bounds={line.bounds}";
                if (!rendererStates.TryGetValue(effect.GetInstanceID(), out string old) || old != state)
                {
                    rendererStates[effect.GetInstanceID()] = state;
                    Record(phase + ": " + state);
                }
            }
        }

        private static void SnapshotReferences()
        {
            RelicTranscendenceData storm = AssetDatabase.LoadAssetAtPath<RelicTranscendenceData>("Assets/Nytherion/Data/ScriptableObjects/Relics/LightningPursuitTranscendence.asset");
            DashChainLightningEffect effect = storm.effectModules.SelectMany(module => module.effects).OfType<DashChainLightningEffect>().Single();
            GameObject weapon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Combat/Proj/StormbringerProj.prefab");
            ChainLightningVFX weaponEffect = new SerializedObject(weapon.GetComponent<LightningProj>()).FindProperty("lightningEffectPrefab").objectReferenceValue as ChainLightningVFX;
            List<string> lines = new List<string>
            {
                "연쇄 폭풍: " + Describe(effect.lightningEffectPrefab),
                "번개부르미: " + Describe(weaponEffect),
                "동일 참조=" + (effect.lightningEffectPrefab == weaponEffect)
            };
            lines.AddRange(Resources.FindObjectsOfTypeAll<ChainLightningVFX>()
                .Where(item => item.gameObject.scene.IsValid()).Select(item => "씬 객체: " + Describe(item)));
            File.WriteAllLines("output/chain-storm/reference-snapshot.txt", lines);
        }

        private static string Describe(ChainLightningVFX effect)
        {
            if (effect == null) return "없음";
            string path = AssetDatabase.GetAssetPath(effect);
            return $"id={effect.GetInstanceID()}, 이름={effect.name}, 경로={path}, GUID={AssetDatabase.AssetPathToGUID(path)}, " +
                $"에셋={EditorUtility.IsPersistent(effect)}, 객체활성={effect.gameObject.activeSelf}/{effect.gameObject.activeInHierarchy}, " +
                $"컴포넌트활성={effect.enabled}, 씬={effect.gameObject.scene.name}, 선 활성={effect.GetComponent<LineRenderer>()?.enabled}";
        }

        private static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(value);
        private static int DictionaryCount(object value, string name)
        {
            object collection = Field(value, name);
            return collection != null ? (int)collection.GetType().GetProperty("Count").GetValue(collection) : 0;
        }
        private static void Record(string message)
        {
            history.Enqueue($"frame={Time.frameCount} t={Time.time:F3} {message}");
            while (history.Count > 400) history.Dequeue();
            dirty = true;
        }

        private static void Detach()
        {
            if (events != null)
            {
                events.OnPlayerDashStarted -= Dash;
                events.OnPlayerRangedAttack -= Ranged;
                events.OnEnemyDamagedByPlayerDetailed -= Damage;
            }
            if (combat != null) combat.OnPlayerAttackStarted -= Attack;
            player = null;
            combat = null;
            events = null;
        }
    }
}
