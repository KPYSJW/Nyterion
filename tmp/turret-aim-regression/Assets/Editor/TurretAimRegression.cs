using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static class TurretAimRegression
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly List<string> Results = new List<string>();
    private static NavMeshDataInstance navMesh;
    private static int failures;
    private static int oldFailures;

    public static void Run()
    {
        try
        {
            foreach (float depth in new[] { 0f, 0.25f, -0.5f })
            {
                BuildFloor(depth);
                var data = ScriptableObject.CreateInstance<TurretSkillData>();
                data.searchRadius = 1f;
                data.minimumDeploymentDistance = 0.6f;
                Vector3 player = new Vector3(2f, -2f, 0f);
                Require(NavMesh.SamplePosition(player, out NavMeshHit floorHit, 1f, NavMesh.AllAreas), "바닥 빌드 실패");
                Results.Add($"실제 NavMesh 깊이: {floorHit.position.z:F4}, 플레이어 깊이: {player.z:F4}");
                Vector3 oldLanding = Find(typeof(LegacyTurretSkill), player, player + Vector3.right * 4f, data);
                if (Vector2.Distance(player, oldLanding) < 0.01f) oldFailures++;
                Results.Add($"이전 코드 착지: {oldLanding}, 제자리={Vector2.Distance(player, oldLanding) < 0.01f}");
                foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down,
                    new Vector3(1f, 1f).normalized, new Vector3(-1f, 1f).normalized,
                    new Vector3(1f, -1f).normalized, new Vector3(-1f, -1f).normalized })
                {
                    foreach (float aimDistance in new[] { 0.2f, 0.8f, 4f })
                    {
                        Vector3 expected = player + direction * Mathf.Clamp(aimDistance, 0.6f, 1f);
                        Vector3 landing = Find(typeof(TurretSkill), player, player + direction * aimDistance, data);
                        Check($"조준 {direction}, 거리 {aimDistance}, 깊이 {depth}", () =>
                            Require(Vector3.Distance(landing, expected) < 0.011f, $"예상={expected}, 실제={landing}"));
                    }
                }
                Check($"실제 마우스 입력 → Activate → 비행 → 착지 (바닥 깊이 {depth})", () => VerifyFlight(data, player));
                Object.DestroyImmediate(data);
                navMesh.Remove();
            }
            Check("이전 코드의 제자리 점프 재현", () => Require(oldFailures > 0, "이전 코드 실패를 재현하지 못함"));
            var noFloorData = ScriptableObject.CreateInstance<TurretSkillData>();
            noFloorData.searchRadius = 1f;
            noFloorData.minimumDeploymentDistance = 0.6f;
            Check("NavMesh 없음: 설치 실패 처리", () => Require(Find(typeof(TurretSkill), Vector3.zero,
                Vector3.right * 3f, noFloorData) == Vector3.zero, "바닥 없는 지점에 설치됨"));
            Object.DestroyImmediate(noFloorData);
        }
        catch (Exception exception)
        {
            failures++;
            Results.Add("FAIL: " + exception);
        }
        finally
        {
            if (navMesh.valid) navMesh.Remove();
            Results.Add($"실패 {failures}개");
            string reportPath = Path.GetFullPath("../../output/turret-aim-check/native-regression.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllLines(reportPath, Results);
            Debug.Log("[TurretAimRegression] " + string.Join("\n", Results));
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
    }

    private static void BuildFloor(float depth)
    {
        var mesh = new Mesh();
        mesh.vertices = new[] { new Vector3(-10f, -10f, depth), new Vector3(-10f, 10f, depth),
            new Vector3(10f, 10f, depth), new Vector3(10f, -10f, depth) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Mesh, sourceObject = mesh, transform = Matrix4x4.identity, area = 0
        } };
        NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources,
            new Bounds(Vector3.zero, new Vector3(24f, 4f, 24f)), Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
        Require(data != null, "NavMeshData 생성 실패");
        navMesh = NavMesh.AddNavMeshData(data);
        Object.DestroyImmediate(mesh);
    }

    private static Vector3 Find(Type skillType, Vector3 player, Vector3 aim, TurretSkillData data)
    {
        return (Vector3)skillType.GetMethod("FindAimedLandingPosition", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { player, aim, data, NavMesh.AllAreas });
    }

    private static void VerifyFlight(TurretSkillData data, Vector3 position)
    {
        GameObject cameraObject = new GameObject("회귀 검사 카메라");
        GameObject player = new GameObject("회귀 검사 플레이어");
        GameObject prefab = new GameObject("회귀 검사 포탑 원본");
        GameObject skillObject = new GameObject("회귀 검사 스킬");
        GameObject clone = null;
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(position.x, position.y, -10f);
            player.transform.position = position;
            prefab.AddComponent<RootiTurretController>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(prefab.transform, false);
            typeof(RootiTurretController).GetField("visual", Private).SetValue(prefab.GetComponent<RootiTurretController>(), visual);
            data.turretPrefab = prefab;
            data.launchAroundCaster = true;
            Vector3 aim = position + new Vector3(3f, 2f, 0f);
            Vector3 screenPosition = camera.WorldToScreenPoint(aim);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPosition });
            InputSystem.Update();
            TurretSkill skill = skillObject.AddComponent<TurretSkill>();
            skill.skillData = data;
            skill.caster = player.transform;
            typeof(TurretSkill).GetMethod("Activate", Private).Invoke(skill, null);
            foreach (RootiTurretController turret in Object.FindObjectsOfType<RootiTurretController>())
                if (turret.gameObject != prefab) clone = turret.gameObject;
            Require(clone != null, "포탑 생성 실패");
            var controller = clone.GetComponent<RootiTurretController>();
            Vector3 destination = (Vector3)typeof(RootiTurretController).GetField("landingPosition", Private).GetValue(controller);
            Vector3 expected = position + (aim - position).normalized;
            Require(Vector3.Distance(expected, destination) < 0.011f, $"착지점 예상={expected}, 실제={destination}");
            float flightDuration = (float)typeof(RootiTurretController).GetField("flightDuration", Private).GetValue(controller);
            FieldInfo startTime = typeof(RootiTurretController).GetField("phaseStartTime", Private);
            MethodInfo update = typeof(RootiTurretController).GetMethod("Update", Private);
            startTime.SetValue(controller, Time.time - flightDuration * 0.5f);
            update.Invoke(controller, null);
            Require(Vector3.Distance(clone.transform.position, Vector3.Lerp(position, destination, 0.5f)) < 0.011f,
                "포탑 본체가 조준 방향으로 이동하지 않음");
            Require(clone.transform.Find("Visual").localPosition.y > 0.7f, "비행 높이 연출 누락");
            startTime.SetValue(controller, Time.time - flightDuration - 0.1f);
            update.Invoke(controller, null);
            Require(Vector3.Distance(clone.transform.position, destination) < 0.011f, "목표 위치에 착지하지 않음");
            Require(clone.transform.Find("Visual").localPosition == Vector3.zero, "착지 후 시각 위치 미복원");
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
            if (clone != null) Object.DestroyImmediate(clone);
            Object.DestroyImmediate(skillObject);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(cameraObject);
        }
    }

    private static void Check(string name, Action verify)
    {
        try { verify(); Results.Add("PASS: " + name); }
        catch (Exception exception) { failures++; Results.Add("FAIL: " + name + " — " + exception); }
    }

    private static void Require(bool passed, string message)
    {
        if (!passed) throw new Exception(message);
    }
}
