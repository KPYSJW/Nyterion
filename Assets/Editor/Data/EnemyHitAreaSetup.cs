using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Skills;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>런타임 비용 없이 적 프리팹의 몸체 크기를 기본 스프라이트 외형에 맞춥니다.</summary>
    [InitializeOnLoad]
    public static class EnemyHitAreaSetup
    {
        private const string Output = "output/enemy-hit-area";
        private static readonly string[] PrefabFolders =
        {
            "Assets/Prefabs/Characters/Enemies", "Assets/Resources/Sprites/Monster", "Assets/Prefabs/Debug"
        };

        static EnemyHitAreaSetup()
        {
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            RunRequest("diagnose", () => ProcessPrefabs(false));
            RunRequest("fit", () => ProcessPrefabs(true));
        }

        private static void RunRequest(string name, Action action)
        {
            string request = Output + "/" + name + ".request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { action(); }
            catch (Exception exception)
            {
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output + "/" + name + ".txt", "FAIL " + exception);
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Nytherion/Enemy/Fit Hit Areas To Sprites")]
        public static void FitPrefabs() => ProcessPrefabs(true);

        public static void DiagnosePrefabs() => ProcessPrefabs(false);

        public static bool FitToSprite(EnemyBase enemy)
        {
            SerializedObject serialized = new SerializedObject(enemy);
            SpriteRenderer renderer = serialized.FindProperty("spriteRenderer").objectReferenceValue as SpriteRenderer;
            Collider2D body = serialized.FindProperty("groundBodyCollider").objectReferenceValue as Collider2D;
            if (body == null) body = enemy.GetComponent<Collider2D>();
            if (renderer == null) renderer = enemy.GetComponentInChildren<SpriteRenderer>();
            if (body == null || renderer == null || renderer.sprite == null) return false;
            if (!TryGetLocalBounds(renderer, body.transform, out Bounds bounds)) return false;

            if (body is BoxCollider2D box) box.size = bounds.size;
            else if (body is CapsuleCollider2D capsule) capsule.size = bounds.size;
            else if (body is CircleCollider2D circle)
                circle.radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
            else return false;
            body.offset = bounds.center;
            serialized.FindProperty("groundBodyCollider").objectReferenceValue = body;
            serialized.FindProperty("groundHitRadiusRatio").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool TryGetLocalBounds(SpriteRenderer renderer, Transform target, out Bounds bounds)
        {
            Sprite sprite = renderer.sprite;
            var points = new List<Vector2>();
            Matrix4x4 matrix = target.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            bool hasPoint = false;
            bounds = default;
            // Unity가 생성한 외형 윤곽은 원본 이미지의 투명 여백을 포함하지 않습니다.
            for (int shape = 0; shape < sprite.GetPhysicsShapeCount(); shape++)
            {
                sprite.GetPhysicsShape(shape, points);
                foreach (Vector2 point in points)
                    Encapsulate(point, renderer, matrix, ref bounds, ref hasPoint);
            }
            if (!hasPoint)
            {
                foreach (Vector2 point in sprite.vertices)
                    Encapsulate(point, renderer, matrix, ref bounds, ref hasPoint);
            }
            return hasPoint && bounds.size.x > 0f && bounds.size.y > 0f;
        }

        private static void Encapsulate(Vector2 point, SpriteRenderer renderer, Matrix4x4 matrix,
            ref Bounds bounds, ref bool hasPoint)
        {
            if (renderer.flipX) point.x = -point.x;
            if (renderer.flipY) point.y = -point.y;
            Vector3 local = matrix.MultiplyPoint3x4(point);
            if (hasPoint) bounds.Encapsulate(local);
            else { bounds = new Bounds(local, Vector3.zero); hasPoint = true; }
        }

        private static void ProcessPrefabs(bool apply)
        {
            Directory.CreateDirectory(Output);
            var report = new List<string>();
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", PrefabFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                EnemyBase assetEnemy = asset.GetComponent<EnemyBase>();
                if (assetEnemy == null) continue;
                string original = apply ? File.ReadAllText(path) : null;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long rootId);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(assetEnemy, out string _, out long enemyId);
                Collider2D assetBody = new SerializedObject(assetEnemy).FindProperty("groundBodyCollider").objectReferenceValue as Collider2D;
                if (assetBody == null) assetBody = asset.GetComponent<Collider2D>();
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(assetBody, out string _, out long bodyId);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    EnemyBase enemy = root.GetComponent<EnemyBase>();
                    Collider2D body = enemy.GetComponent<Collider2D>();
                    string before = Describe(body);
                    if (!FitToSprite(enemy)) throw new InvalidOperationException("외형을 구할 수 없는 적: " + path);
                    // Enemy 레이어에서 누락되어 범위 공격이 검색하지 못하던 프리팹도 함께 바로잡습니다.
                    root.layer = LayerMask.NameToLayer("Enemy");
                    if (apply)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        string saved = File.ReadAllText(path);
                        // 기존 스크립트의 옛 필드까지 재직렬화하지 않고 이번에 설정한 필드만 반영합니다.
                        original = CopySerializedField(original, saved, rootId, "m_Layer");
                        original = CopySerializedField(original, saved, enemyId, "groundBodyCollider");
                        original = CopySerializedField(original, saved, enemyId, "groundHitRadiusRatio");
                        original = CopySerializedField(original, saved, bodyId, "m_Offset");
                        original = CopySerializedField(original, saved, bodyId, body is CircleCollider2D ? "m_Radius" : "m_Size");
                        File.WriteAllText(path, original, new UTF8Encoding(false));
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                    report.Add(path + " | " + before + " -> " + Describe(body) + " | layer=" + root.layer);
                    count++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            VerifyGeometry();
            report.Add("PASS 적 " + count + "종: 외형 크기 설정 및 원형/타원형 경계 확인");
            File.WriteAllLines(Output + "/" + (apply ? "fit" : "diagnose") + ".txt", report);
            Debug.Log("적 피격 범위 " + count + "종 " + (apply ? "저장" : "진단") + " 완료");
        }

        private static string Describe(Collider2D collider)
        {
            if (collider == null) return "없음";
            string size = collider is BoxCollider2D box ? box.size.ToString("F3") :
                collider is CapsuleCollider2D capsule ? capsule.size.ToString("F3") :
                collider is CircleCollider2D circle ? "radius=" + circle.radius.ToString("F3") : collider.GetType().Name;
            return collider.GetType().Name + " " + size + " offset=" + collider.offset.ToString("F3");
        }

        private static string CopySerializedField(string original, string saved, long fileId, string field)
        {
            string sectionPattern = @"(?ms)^--- !u!\d+ &" + fileId + @"\r?\n.*?(?=^--- !u!|\z)";
            string fieldPattern = @"(?m)^  " + Regex.Escape(field) + @":[^\r\n]*";
            Match savedSection = Regex.Match(saved, sectionPattern);
            Match savedField = Regex.Match(savedSection.Value, fieldPattern);
            if (!savedSection.Success || !savedField.Success || !Regex.IsMatch(original, sectionPattern))
                throw new InvalidOperationException("직렬화 필드 확인 실패: " + fileId + " / " + field);
            string newline = original.Contains("\r\n") ? "\r\n" : "\n";
            return Regex.Replace(original, sectionPattern, match =>
                Regex.IsMatch(match.Value, fieldPattern) ?
                    Regex.Replace(match.Value, fieldPattern, _ => savedField.Value) :
                    match.Value.TrimEnd('\r', '\n') + newline + savedField.Value + newline);
        }

        private static void VerifyGeometry()
        {
            Bounds tallBody = new Bounds(Vector3.zero, new Vector3(0.4f, 1.5f, 0f));
            Require(ChainIgnitionHitRange.OverlapsBounds(new Vector2(0f, 1.34f), new Vector2(0.6f, 0.6f), tallBody),
                "긴 적의 머리 쪽 몸체 접촉");
            Require(!ChainIgnitionHitRange.OverlapsBounds(new Vector2(0f, 1.37f), new Vector2(0.6f, 0.6f), tallBody),
                "긴 적의 몸체 바깥 제외");
            Require(ChainIgnitionHitRange.OverlapsBounds(new Vector2(0.79f, 0f), new Vector2(0.6f, 0.3f), tallBody),
                "타원의 가로 경계 접촉");
            Require(!ChainIgnitionHitRange.OverlapsBounds(new Vector2(0.83f, 0f), new Vector2(0.6f, 0.3f), tallBody),
                "타원의 가로 경계 바깥 제외");
            Require(!ChainIgnitionHitRange.OverlapsBounds(new Vector2(0.75f, 1.03f), new Vector2(0.6f, 0.3f), tallBody),
                "사각형 바깥 대각선에서 타원 경계 유지");
        }

        private static void Require(bool success, string label)
        {
            if (!success) throw new InvalidOperationException(label);
        }
    }
}
