using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 포탈 프리팹과 Physics2D로 벽 배치 및 F 상호작용 경로를 검증합니다.</summary>
    [InitializeOnLoad]
    public static class PortalGateSkillVerification
    {
        private const string Output = "output/portal-gate";
        private const string Pending = "Nytherion.PortalGate.Verify";
        private static readonly Vector3 Origin = new Vector3(1000f, 1000f, 0f);
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static readonly List<string> checks = new List<string>();
        private static PortalGateSkillData data;
        private static IEnumerator routine;
        private static double readyAt;
        private static float nextAt;
        private static bool exitAfter;
        private static Camera previousCamera;

        static PortalGateSkillVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 3d;
                if (state == PlayModeStateChange.ExitingPlayMode && routine != null)
                    Finish(new Exception("플레이 모드 검증이 중단되었습니다."));
            };
        }

        [MenuItem("Tools/Nytherion/Portal Gate/Verify In Play Mode")]
        public static void Start()
        {
            if (routine != null || SessionState.GetBool(Pending, false)) return;
            if (!EditorApplication.isPlaying && AssetDatabase.LoadAssetAtPath<PortalGateSkillData>(PortalGateSkillSetup.DataPath) == null)
                PortalGateSkillSetup.CreateAssets();
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Pending + ".ExitAfter", !EditorApplication.isPlaying);
            SessionState.SetBool(Pending, true);
            if (EditorApplication.isPlaying) readyAt = EditorApplication.timeSinceStartup + 0.2d;
            else EditorApplication.isPlaying = true;
        }

        // 별도 프로젝트 복사본에서 호출해 사용자 씬과 저장 파일을 보존합니다.
        public static void RunBatch()
        {
            PortalGateSkillSetup.UpdateCameraPlacementAssets();
            SessionState.SetBool(Pending + ".Batch", true);
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            Start();
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/verify.request"))
                {
                    File.Delete(Output + "/verify.request");
                    Start();
                }
                if (SessionState.GetBool(Pending, false) && EditorApplication.isPlaying &&
                    readyAt > 0d && EditorApplication.timeSinceStartup >= readyAt)
                {
                    SessionState.SetBool(Pending, false);
                    exitAfter = SessionState.GetBool(Pending + ".ExitAfter", false);
                    checks.Clear();
                    data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PortalGateSkillData>(PortalGateSkillSetup.DataPath));
                    routine = Verify();
                    nextAt = Time.time;
                }
                if (routine == null || EditorApplication.isPaused || Time.time < nextAt) return;
                if (!routine.MoveNext()) { Finish(null); return; }
                nextAt = Time.time + (float)routine.Current;
            }
            catch (Exception error) { Finish(error); }
        }

        private static IEnumerator Verify()
        {
            Require(data != null && data.portalPrefab != null && data.skillPrefab != null && data.icon != null,
                "스킬 데이터·포탈 프리팹·시전 프리팹·아이콘 참조");
            Require(data.animationFrames.Length == 16 && data.animationFrames.All(frame => frame.rect.size == Vector2.one * 32f),
                "첨부 512×32 시트의 32×32 애니메이션 16프레임");
            Require(data.coolDown == 45f && data.lifetime == 15f && data.maxTraversals == 6,
                "기본 쿨타임 45초·지속 15초·두 포탈 합산 6회");
            Require(data.portalPrefab.gameObject.layer == LayerMask.NameToLayer("Interactable") &&
                data.portalPrefab.GetComponent<CircleCollider2D>().isTrigger, "F 상호작용 레이어·비충돌 트리거");
            SkillData source = AssetDatabase.LoadAssetAtPath<SkillData>(PortalGateSkillSetup.DataPath);
            Require(AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>(
                "Assets/Nytherion/Data/ScriptableObjects/Skill/SkillDatabaseSO.asset").GetSkillById("skill_portal_gate") == source,
                "기존 저장 ID 복원에 사용하는 스킬 DB 등록");
            Require(AssetDatabase.LoadAssetAtPath<GachaPoolSO>(
                "Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Skill/SkillGachaPool.asset").items.Any(entry => entry.item == source),
                "기존 스킬 가챠 획득 경로 연결");
            Require(File.ReadAllText("Assets/Input/PlayerAction.inputactions").Contains("<Keyboard>/f"),
                "Input Actions 원본의 기존 F키 바인딩 유지");

            previousCamera = Camera.main;
            if (previousCamera != null) previousCamera.tag = "Untagged";
            Camera viewCamera = Track(new GameObject("PortalVerificationViewCamera")).AddComponent<Camera>();
            viewCamera.tag = "MainCamera";
            viewCamera.transform.position = Origin + Vector3.back * 10f;
            viewCamera.orthographic = true;
            viewCamera.orthographicSize = 5f;
            viewCamera.aspect = 16f / 9f;
            viewCamera.cullingMask = 0;

            GameObject actor = Track(new GameObject("PortalVerificationPlayer"));
            actor.transform.position = Origin;
            Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            BoxCollider2D collider = actor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.6f, 0.8f);
            collider.offset = new Vector2(0.15f, -0.2f);
            Physics2D.SyncTransforms();
            Require(PortalGatePlacement.TryFindDestination(actor.transform, Vector2.right, data, viewCamera, out Vector3 destination) &&
                Mathf.Abs(destination.x - Origin.x - (5f * viewCamera.aspect - 0.45f - data.screenEdgePadding)) < 0.002f,
                "고정 거리 대신 오른쪽 카메라 경계 안쪽에 점유 공간을 포함해 배치");
            Require(PortalGatePlacement.TryFindDestination(actor.transform, Vector2.left, data, viewCamera, out destination) &&
                Mathf.Abs(destination.x - Origin.x + (5f * viewCamera.aspect - 0.45f - data.screenEdgePadding)) < 0.002f,
                "왼쪽 카메라 경계 배치");
            Require(PortalGatePlacement.TryFindDestination(actor.transform, Vector2.up, data, viewCamera, out destination) &&
                Mathf.Abs(destination.y - Origin.y - (5f - 0.45f - data.screenEdgePadding)) < 0.002f,
                "위쪽 카메라 경계 배치");
            Require(PortalGatePlacement.TryFindDestination(actor.transform, Vector2.down, data, viewCamera, out destination) &&
                Mathf.Abs(destination.y - Origin.y + (5f - 0.6f - data.screenEdgePadding)) < 0.002f,
                "아래쪽 경계는 플레이어 충돌체의 비대칭 오프셋까지 포함");
            Require(PortalGatePlacement.TryFindDestination(actor.transform, Vector2.one, data, viewCamera, out destination) &&
                Mathf.Abs(destination.y - Origin.y - (5f - 0.45f - data.screenEdgePadding)) < 0.002f &&
                Mathf.Abs(destination.x - Origin.x - (destination.y - Origin.y)) < 0.002f,
                "대각선 조준은 먼저 만나는 화면 경계에서 정지");
            viewCamera.transform.position += new Vector3(2f, 1f);
            Require(PortalGatePlacement.TryFindDestination(actor.transform, Vector2.right, data, viewCamera, out destination) &&
                Mathf.Abs(destination.x - Origin.x - (2f + 5f * viewCamera.aspect - 0.45f - data.screenEdgePadding)) < 0.002f,
                "플레이어가 화면 중앙에서 벗어나도 현재 카메라 위치로 배치");
            viewCamera.transform.position = Origin + Vector3.back * 10f;
            viewCamera.orthographicSize = 8f;
            viewCamera.aspect = 1f;
            Require(PortalGatePlacement.TryFindDestination(actor.transform, Vector2.right, data, viewCamera, out destination) &&
                Mathf.Abs(destination.x - Origin.x - (8f - 0.45f - data.screenEdgePadding)) < 0.002f,
                "줌과 화면 비율 변경을 시전 시점에 반영");
            Require(!PortalGatePlacement.TryFindDestination(actor.transform, Vector2.right, data, null, out _),
                "카메라가 없으면 고정 거리로 대체하지 않고 배치 취소");
            viewCamera.orthographicSize = 5f;
            viewCamera.aspect = 16f / 9f;

            GameObject wall = Wall(Origin + Vector3.right * 4f, new Vector2(0.2f, 8f));
            Require(PortalGatePlacement.TryFindDestination(actor.transform, Vector2.right, data, viewCamera, out destination) &&
                destination.x > Origin.x + 3f && destination.x < Origin.x + 3.5f &&
                PortalGatePlacement.IsClear(destination, PortalGatePlacement.GetFootprint(actor.transform, data), data),
                "얇은 맵 경계 벽 앞에서 거리 축소·포탈과 플레이어 점유 공간 보장");
            wall.SetActive(false);
            wall = Wall(Origin + Vector3.one * 3f, new Vector2(0.2f, 8f));
            Require(PortalGatePlacement.TryFindDestination(actor.transform, Vector2.one, data, viewCamera, out destination) &&
                destination.x < Origin.x + 2.5f && destination.y < Origin.y + 2.5f, "대각선 조준도 벽 너머 생성 방지");
            wall.SetActive(false);
            wall = Wall(Origin + Vector3.right * 0.9f, new Vector2(0.2f, 8f));
            Require(!PortalGatePlacement.TryFindDestination(actor.transform, Vector2.right, data, viewCamera, out _),
                "벽이 너무 가까우면 겹치는 포탈 생성 취소");
            wall.SetActive(false);

            GameObject tileRoot = Track(new GameObject("PortalVerificationWallTiles", typeof(Grid)));
            GameObject tileObject = Track(new GameObject("WallTilemap", typeof(Tilemap), typeof(TilemapCollider2D)));
            tileObject.transform.SetParent(tileRoot.transform);
            tileObject.transform.position = Origin;
            tileObject.layer = LayerMask.NameToLayer("Wall");
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.colliderType = Tile.ColliderType.Grid;
            Tilemap tilemap = tileObject.GetComponent<Tilemap>();
            for (int y = -5; y <= 5; y++) tilemap.SetTile(new Vector3Int(4, y, 0), tile);
            tileObject.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
            Require(PortalGatePlacement.TryFindDestination(actor.transform, Vector2.right, data, viewCamera, out destination) &&
                destination.x > Origin.x + 3f && destination.x < Origin.x + 3.6f, "실제 TilemapCollider2D 벽 앞 배치");
            tileObject.SetActive(false);
            Object.Destroy(tile);

            PortalGateSkill skill = NewSkill(actor.transform);
            // 플레이어 발사점이 멀리 있어도 현재 위치에서 포탈을 만듭니다.
            GameObject firePoint = Track(new GameObject("DistantFirePoint"));
            firePoint.transform.position = Origin + Vector3.up * 20f;
            skill.firePoint = firePoint.transform;
            Require(skill.TryUse(), "기존 SkillBase.TryUse 시전 경로");
            PortalGateEndpoint entrance = Endpoint(skill, "entrance");
            PortalGateEndpoint exit = Endpoint(skill, "exit");
            Vector3 target = exit.transform.position;
            Vector2 castDirection = (target - Origin).normalized;
            Require(PortalGatePlacement.TryGetCameraDistance(viewCamera, Origin, castDirection,
                    PortalGatePlacement.GetFootprint(actor.transform, data), data.screenEdgePadding, out float expectedDistance) &&
                entrance.transform.position == Origin && Mathf.Abs(Vector3.Distance(target, Origin) - expectedDistance) < 0.002f,
                "실제 시전도 플레이어 현재 위치에서 마우스 방향의 카메라 경계에 생성·발사점 위치 제외");
            Require(!skill.TryUse() && skill.GetRemainingCooldown() > 44f, "시전 즉시 긴 쿨타임 적용·재시전 차단");
            Sprite initialFrame = entrance.GetComponent<SpriteRenderer>().sprite;
            yield return 0.12f;
            Require(entrance.GetComponent<SpriteRenderer>().sprite != initialFrame, "16프레임 포탈 그림 재생");
            Capture(entrance, exit, "portal-pair.png");

            InteractionManager interaction = Track(new GameObject("PortalVerificationInteraction")).AddComponent<InteractionManager>();
            Set(interaction, "playerTransform", actor.transform);
            Set(interaction, "interactableLayer", (LayerMask)LayerMask.GetMask("Interactable"));
            actor.transform.position = Origin + Vector3.up * 0.9f;
            Physics2D.SyncTransforms();
            Interact(interaction);
            Require(Vector3.Distance(actor.transform.position, Origin + Vector3.up * 0.9f) < 0.001f && skill.RemainingTraversals == 6,
                "상호작용 탐색 범위 안이어도 포탈 위가 아니면 이동·횟수 차감 없음");
            actor.transform.position = Origin;
            body.position = Origin;
            body.velocity = Vector2.right * 3f;
            Physics2D.SyncTransforms();
            Interact(interaction);
            Require(Vector3.Distance(actor.transform.position, target) < 0.001f &&
                Vector2.Distance(body.position, target) < 0.001f && body.velocity == Vector2.zero && skill.RemainingTraversals == 5,
                "F키 실제 InteractionManager 처리 경로로 순간이동·물리 위치 동기화·성공 1회 차감");
            Interact(interaction);
            Require(skill.RemainingTraversals == 5 && actor.transform.position == target, "연속 입력에 즉시 왕복하지 않도록 공통 이동 간격 적용");
            yield return data.traversalDelay + 0.04f;
            Time.timeScale = 0f;
            Require(!skill.TryTraverse(exit) && skill.RemainingTraversals == 5, "일시정지 중 이동과 횟수 차감 차단");
            Time.timeScale = 1f;

            Vector3 midpoint = (Origin + target) * 0.5f;
            GameObject newWall = Wall(midpoint, Vector2.one);
            Require(!skill.TryTraverse(exit) && skill.RemainingTraversals == 5, "생성 후 두 포탈 사이에 새 벽이 생기면 이동 거부·횟수 보존");
            newWall.SetActive(false);
            newWall = Wall(Origin, Vector2.one);
            Require(!skill.TryTraverse(exit) && skill.RemainingTraversals == 5, "도착 지점이 막히면 벽 속 이동 거부·횟수 보존");
            newWall.SetActive(false);
            for (int i = 0; i < 5; i++)
            {
                Physics2D.SyncTransforms();
                Interact(interaction);
                Require(skill.RemainingTraversals == 4 - i, "왕복 성공 횟수 공유 차감 " + (i + 2) + "/6");
                yield return data.traversalDelay + 0.03f;
            }
            Require(!skill.HasActivePair && !entrance.gameObject.activeSelf && !exit.gameObject.activeSelf,
                "마지막 이용 후 두 포탈 동시 종료");

            data.coolDown = 0f;
            data.lifetime = 0.3f;
            PortalGateSkill expiring = NewSkill(actor.transform);
            Require(expiring.TryUse(), "지속시간 검증용 재시전");
            yield return 0.4f;
            Require(!expiring.HasActivePair && expiring.RemainingTraversals == 0, "지속시간 만료 시 두 포탈 종료");

            data.lifetime = 15f;
            PortalGateSkill blocked = NewSkill(actor.transform);
            GameObject blockedWall = Wall(actor.transform.position, Vector2.one * 2f);
            Require(!blocked.TryUse() && blocked.GetRemainingCooldown() == 0f && !blocked.HasActivePair,
                "잘못된 시작 위치의 실패한 시전은 포탈·쿨타임 미생성");
            blockedWall.SetActive(false);
            Require(blocked.TryUse(), "공간 확보 후 다시 시전 가능");
            entrance = Endpoint(blocked, "entrance");
            exit = Endpoint(blocked, "exit");
            yield return 0.03f;
            Require(blocked.TryUse() && Endpoint(blocked, "entrance") == entrance && Endpoint(blocked, "exit") == exit,
                "쿨타임 후 재시전 시 기존 한 쌍 재사용·중복 생성 없음");
            blocked.enabled = false;
            Require(!entrance.gameObject.activeSelf && !exit.gameObject.activeSelf, "스킬 해제·비활성 시 두 포탈 정리");
        }

        private static PortalGateSkill NewSkill(Transform caster)
        {
            PortalGateSkill skill = Track(Object.Instantiate(data.skillPrefab)).GetComponent<PortalGateSkill>();
            skill.skillData = data;
            skill.caster = caster;
            return skill;
        }

        private static GameObject Wall(Vector3 position, Vector2 size)
        {
            GameObject wall = Track(new GameObject("PortalVerificationWall"));
            wall.transform.position = position;
            wall.layer = LayerMask.NameToLayer("Wall");
            wall.AddComponent<BoxCollider2D>().size = size;
            Physics2D.SyncTransforms();
            return wall;
        }

        private static GameObject Track(GameObject obj) { objects.Add(obj); return obj; }
        private static PortalGateEndpoint Endpoint(PortalGateSkill skill, string field) =>
            (PortalGateEndpoint)typeof(PortalGateSkill).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skill);
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Interact(InteractionManager manager) => typeof(InteractionManager)
            .GetMethod("HandleInteraction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add("PASS " + message);
        }

        private static void Capture(PortalGateEndpoint a, PortalGateEndpoint b, string file)
        {
            GameObject cameraObject = Track(new GameObject("PortalVerificationCamera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.transform.position = (a.transform.position + b.transform.position) * 0.5f + Vector3.back * 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.065f);
            RenderTexture target = new RenderTexture(960, 540, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + "/" + file, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(image);
                cameraObject.SetActive(false);
            }
        }

        private static void Finish(Exception error)
        {
            routine = null;
            SessionState.SetBool(Pending, false);
            foreach (GameObject obj in objects)
                if (obj != null) { obj.SetActive(false); Object.Destroy(obj); }
            objects.Clear();
            if (previousCamera != null) previousCamera.tag = "MainCamera";
            previousCamera = null;
            if (data != null) Object.Destroy(data);
            if (error != null) checks.Add("FAIL " + error);
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output + "/verification.txt", checks);
            if (exitAfter) EditorApplication.isPlaying = false;
            if (SessionState.GetBool(Pending + ".Batch", false)) EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
