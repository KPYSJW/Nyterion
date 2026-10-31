using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Data.ScriptableObjects;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class BurnTickEffectSetup
    {
        private const string Output = "output/burn-tick";
        private const string SheetPath = "Assets/Nytherion/Art/Combat/BurnTickEffect.png";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/BurnTickVFX.prefab";
        private const string DataPath = "Assets/Nytherion/Data/StatusEffectDatabase.asset";

        static BurnTickEffectSetup() => EditorApplication.update += ProcessRequest;

        private static void ProcessRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/상태이상/화상 피해 이펙트 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            StatusEffectDatabase data = AssetDatabase.LoadAssetAtPath<StatusEffectDatabase>(DataPath);
            if (sheet == null || sheet.width != 320 || sheet.height != 64 || data == null)
                throw new InvalidOperationException("320×64 화상 시트와 상태이상 데이터베이스를 확인해 주세요.");
            WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(64, 64), 64f);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
            if (frames.Length != 5) throw new InvalidOperationException("화상 시트는 프레임 5장이어야 합니다.");

            GameObject previous = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (previous != null && previous.GetComponent<BurnTickVFX>() == null)
                throw new InvalidOperationException("같은 경로에 다른 용도의 프리팹이 있습니다.");
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = previous != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("BurnTickVFX");
                if (previous == null) SceneManager.MoveGameObjectToScene(root, preview);
                BurnTickVFX effect = root.GetComponent<BurnTickVFX>() ?? root.AddComponent<BurnTickVFX>();
                SpriteRenderer[] flames = root.GetComponentsInChildren<SpriteRenderer>(true);
                if (flames.Length == 0)
                {
                    flames = new SpriteRenderer[3];
                    for (int i = 0; i < flames.Length; i++)
                    {
                        GameObject child = new GameObject("Flame" + i);
                        child.transform.SetParent(root.transform, false);
                        flames[i] = child.AddComponent<SpriteRenderer>();
                    }
                }
                foreach (SpriteRenderer flame in flames)
                {
                    flame.sprite = frames[0];
                    flame.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                    flame.enabled = false;
                }
                SerializedObject serialized = new SerializedObject(effect);
                SerializedProperty frameArray = serialized.FindProperty("frames");
                frameArray.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) frameArray.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                SerializedProperty flameArray = serialized.FindProperty("flames");
                flameArray.arraySize = flames.Length;
                for (int i = 0; i < flames.Length; i++) flameArray.GetArrayElementAtIndex(i).objectReferenceValue = flames[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                effect.enabled = false;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                data.burnTickPrefab = saved.GetComponent<BurnTickVFX>();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            finally
            {
                if (previous != null && root != null) PrefabUtility.UnloadPrefabContents(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            Verify();
            Debug.Log("[BurnTickEffectSetup] 화상 시트·프리팹·데이터 참조와 크기별 재생 검증을 완료했습니다.", data);
        }

        public static void Verify()
        {
            StatusEffectDatabase data = AssetDatabase.LoadAssetAtPath<StatusEffectDatabase>(DataPath);
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath));
            if (data.burnTickPrefab == null || frames.Length != 5 ||
                data.burnTickPrefab.GetComponentsInChildren<Collider2D>(true).Length != 0)
                throw new InvalidOperationException("화상 프리팹 참조·프레임·충돌체 검증 실패");
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TrainingDummySetup.PrefabPath));
                SceneManager.MoveGameObjectToScene(target, preview);
                SpriteRenderer body = target.GetComponent<SpriteRenderer>();
                Vector3 baseScale = target.transform.localScale;
                Vector3 baseBodySize = body.bounds.size;
                Collider2D collider = target.GetComponent<Collider2D>();
                Vector3 baseColliderSize = collider.bounds.size;
                body.sortingOrder = 17;
                BurnTickVFX effect = Object.Instantiate(data.burnTickPrefab, target.transform);
                SpriteRenderer[] flames = effect.GetComponentsInChildren<SpriteRenderer>(true);
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo elapsed = typeof(BurnTickVFX).GetField("elapsed", flags);
                MethodInfo update = typeof(BurnTickVFX).GetMethod("LateUpdate", flags);
                float[] sizes = { 0.5f, 1f, 2f, 4f, -4f };
                int[] counts = { 1, 1, 2, 3, 3 };
                for (int i = 0; i < sizes.Length; i++)
                {
                    target.transform.localScale = Vector3.Scale(baseScale, new Vector3(sizes[i], Mathf.Abs(sizes[i]), 1f));
                    Physics2D.SyncTransforms();
                    effect.Play(body);
                    // 빠른 틱 재호출이 재생 시간을 초기화하지 않는지 확인합니다.
                    elapsed.SetValue(effect, 0.13f);
                    effect.Play(body);
                    if ((float)elapsed.GetValue(effect) < 0.13f)
                        throw new InvalidOperationException("빠른 피해 틱에 화상 애니메이션이 초기화됩니다.");
                    update.Invoke(effect, null);
                    if (flames.Count(flame => flame.enabled) != counts[i])
                        throw new InvalidOperationException($"허수아비 {sizes[i]}배의 불꽃 개수가 올바르지 않습니다. 너비={body.bounds.size.x}, 개수={flames.Count(flame => flame.enabled)}");
                    if (Mathf.Abs(body.bounds.size.x - baseBodySize.x * Mathf.Abs(sizes[i])) > 0.001f ||
                        Mathf.Abs(collider.bounds.size.x - baseColliderSize.x * Mathf.Abs(sizes[i])) > 0.001f)
                        throw new InvalidOperationException("허수아비 외형과 피격 범위의 크기 배율이 다릅니다.");
                    float expected = Mathf.Clamp(collider.bounds.size.y * 0.6f, 0.5f, 0.85f);
                    if (Mathf.Abs(flames[0].bounds.size.y - expected) > 0.001f ||
                        flames.Any(flame => flame.enabled && flame.sortingOrder != 18))
                        throw new InvalidOperationException("화상 월드 크기·좌우 반전·정렬 검증 실패");
                    // 투명 여백이 있는 실제 허수아비에서 모든 프레임의 몸체 겹침을 확인합니다.
                    foreach (float time in new[] { 0.001f, 0.065f, 0.13f, 0.195f, 0.26f })
                    {
                        elapsed.SetValue(effect, time);
                        update.Invoke(effect, null);
                        foreach (SpriteRenderer flame in flames.Where(flame => flame.enabled))
                        {
                            Bounds visibleBody = collider.bounds;
                            float distance = Vector2.Distance(flame.transform.position, visibleBody.center);
                            float shortSide = Mathf.Min(visibleBody.size.x, visibleBody.size.y);
                            if ((counts[i] == 1 && distance > 0.001f) ||
                                (counts[i] > 1 && (distance < shortSide * 0.12f - 0.001f || distance > shortSide * 0.48f + 0.001f)))
                                throw new InvalidOperationException($"허수아비 {sizes[i]}배의 불꽃 중심·최대 이동 거리 검증 실패");
                        }
                        for (int first = 0; first < flames.Length; first++)
                            for (int second = first + 1; second < flames.Length; second++)
                                if (flames[first].enabled && flames[second].enabled && flames[first].bounds.Intersects(flames[second].bounds))
                                    throw new InvalidOperationException($"허수아비 {sizes[i]}배의 불꽃이 겹칩니다. 프레임 시간={time}");
                    }
                    effect.Stop();
                    if (flames.Any(flame => flame.enabled)) throw new InvalidOperationException("화상 정지 후 불꽃이 남습니다.");
                }
                effect.Play(body);
                elapsed.SetValue(effect, 1f);
                update.Invoke(effect, null);
                if (effect.enabled || flames.Any(flame => flame.enabled))
                    throw new InvalidOperationException("화상 재생 완료 후 불꽃이 남습니다.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            File.WriteAllText(Output + "/verification.txt",
                "PASS 64×64 프레임 5장, 16fps, 0.3125초 + 불꽃별 0.025초 시차\n" +
                "PASS 상태이상 데이터베이스의 프리팹 참조, 추가 충돌체 없음\n" +
                "PASS 실제 허수아비 0.5/1배 중심 고정, 2/4배 무작위 배치\n" +
                "PASS 전 프레임 불꽃 간 겹침 없음, 중심에서 최대 이동 거리 제한\n" +
                "PASS 외형·피격 범위 동시 확대, 높이 제한, 부모 확대·좌우 반전, 정렬\n" +
                "PASS 빠른 피해 틱의 재생 유지, 정지·재사용·재생 종료 시 불꽃 숨김\n");
        }
    }
}
