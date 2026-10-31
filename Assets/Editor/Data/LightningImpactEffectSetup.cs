using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Data.ScriptableObjects;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class LightningImpactEffectSetup
    {
        private const string Output = "output/lightning-impact-status";
        private const string SheetPath = "Assets/Nytherion/Art/Combat/LightningImpactEffect.png";
        private const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/ShockPulseVFX.prefab";
        private const string DataPath = "Assets/Nytherion/Data/StatusEffectDatabase.asset";

        static LightningImpactEffectSetup() => EditorApplication.update += ProcessRequest;

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

        [MenuItem("Tools/Nytherion/상태이상/감전 이펙트 연결")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            StatusEffectDatabase data = AssetDatabase.LoadAssetAtPath<StatusEffectDatabase>(DataPath);
            if (sheet == null || sheet.width != 600 || sheet.height != 100 || data == null)
                throw new InvalidOperationException("600×100 감전 시트와 상태이상 데이터베이스를 확인해 주세요.");
            SliceVisibleFrames();
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath));
            GameObject previous = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (previous != null && previous.GetComponent<ShockPulseVFX>() == null)
                throw new InvalidOperationException("같은 경로에 다른 용도의 프리팹이 있습니다.");
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = previous != null ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("ShockPulseVFX");
                if (previous == null) SceneManager.MoveGameObjectToScene(root, preview);
                ShockPulseVFX effect = root.GetComponent<ShockPulseVFX>() ?? root.AddComponent<ShockPulseVFX>();
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                renderer.enabled = false;
                SerializedObject serialized = new SerializedObject(effect);
                SerializedProperty array = serialized.FindProperty("frames");
                array.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                effect.enabled = false;
                data.shockPulsePrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<ShockPulseVFX>();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            finally
            {
                if (previous != null && root != null) PrefabUtility.UnloadPrefabContents(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            Verify();
            BurnTickEffectSetup.Verify();
            Debug.Log("[LightningImpactEffectSetup] 감전 프레임·프리팹 연결과 크기별 배치 검증을 완료했습니다.", data);
        }

        private static void SliceVisibleFrames()
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.SaveAndReimport();
            try
            {
                Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
                Color32[] pixels = sheet.GetPixels32();
                int minX = 100, minY = 100, maxX = -1, maxY = -1;
                for (int y = 0; y < 100; y++)
                    for (int x = 0; x < 600; x++)
                    {
                        if (pixels[y * 600 + x].a == 0) continue;
                        minX = Mathf.Min(minX, x % 100); maxX = Mathf.Max(maxX, x % 100);
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
                if (maxX < minX) throw new InvalidOperationException("감전 시트에 표시할 픽셀이 없습니다.");
                minX = Mathf.Max(0, minX - 2); minY = Mathf.Max(0, minY - 2);
                maxX = Mathf.Min(99, maxX + 2); maxY = Mathf.Min(99, maxY + 2);
                var factory = new SpriteDataProviderFactories();
                factory.Init();
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                SpriteRect[] previous = provider.GetSpriteRects();
                SpriteRect[] rects = new SpriteRect[6];
                for (int i = 0; i < 6; i++)
                {
                    string name = "LightningImpactEffect_" + i;
                    SpriteRect existing = previous.FirstOrDefault(rect => rect.name == name);
                    // 모든 프레임에 같은 영역·피벗을 사용해 애니메이션 위치가 흔들리지 않게 합니다.
                    rects[i] = new SpriteRect { name = name,
                        rect = new Rect(i * 100 + minX, minY, maxX - minX + 1, maxY - minY + 1),
                        alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f),
                        spriteID = existing != null ? existing.spriteID : GUID.Generate() };
                }
                provider.SetSpriteRects(rects);
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                    rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
                provider.Apply();
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
            finally
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        public static void Verify()
        {
            StatusEffectDatabase data = AssetDatabase.LoadAssetAtPath<StatusEffectDatabase>(DataPath);
            Sprite[] frames = WeaponFireEffectAssets.LoadFrames(AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath));
            if (frames.Length != 6 || frames.Any(frame => frame.rect.size != frames[0].rect.size) ||
                data.shockPulsePrefab == null || data.shockPulsePrefab.GetComponent<Collider2D>() != null)
                throw new InvalidOperationException("감전 프레임·프리팹 참조 검증 실패");
            Scene preview = EditorSceneManager.NewPreviewScene();
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(9142);
                GameObject target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TrainingDummySetup.PrefabPath));
                SceneManager.MoveGameObjectToScene(target, preview);
                target.transform.position = new Vector3(2000f, 2000f, 0f);
                SpriteRenderer body = target.GetComponent<SpriteRenderer>();
                Collider2D collider = target.GetComponent<Collider2D>();
                ShockPulseVFX effect = Object.Instantiate(data.shockPulsePrefab, target.transform);
                SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo elapsed = typeof(ShockPulseVFX).GetField("elapsed", flags);
                MethodInfo update = typeof(ShockPulseVFX).GetMethod("LateUpdate", flags);
                StatusEffectManager status = target.GetComponent<StatusEffectManager>();
                status.Construct(data);
                var enemy = target.GetComponent<Nytherion.GamePlay.Characters.Enemy.EnemyBase>();
                typeof(StatusEffectManager).GetField("owner", flags).SetValue(status, enemy);
                LightningEffect lightning = new LightningEffect(5f);
                lightning.Initialize(enemy, status, 5f);
                lightning.OnApply();
                for (int i = 0; i < 5; i++) lightning.OnUpdate(1f);
                if (typeof(StatusEffectManager).GetField("shockPulseVFX", flags).GetValue(status) != null)
                    throw new InvalidOperationException("감전 부여 또는 시간 경과만으로 이펙트가 재생됩니다.");
                // 몸체만 직접 재생하는 검사 외에 실제 타격 진입 경로도 검증합니다.
                typeof(Nytherion.GamePlay.Characters.Enemy.EnemyBase).GetField("statusEffectManager", flags).SetValue(enemy, status);
                typeof(Nytherion.GamePlay.Characters.Enemy.EnemyBase).GetField("hitFlashDuration", flags).SetValue(enemy, 0f);
                typeof(Nytherion.GamePlay.Characters.Enemy.EnemyBase).GetField("hitFlashProperties", flags).SetValue(enemy, new MaterialPropertyBlock());
                enemy.TakeDamage(10f);
                FieldInfo hitVFX = typeof(StatusEffectManager).GetField("shockPulseVFX", flags);
                if (hitVFX.GetValue(status) != null) throw new InvalidOperationException("일반 공격에 감전 이펙트가 생성됩니다.");
                enemy.TakeDamage(10f, false, new[] { Nytherion.Core.Enums.EquipmentTrait.Lightning });
                if (hitVFX.GetValue(status) != null)
                    throw new InvalidOperationException("감전되지 않은 대상에 감전 추가 피해 이펙트가 생성됩니다.");
                status.ApplyEffect(new LightningEffect(5f));
                enemy.TakeDamage(0f);
                ShockPulseVFX attackEffect = hitVFX.GetValue(status) as ShockPulseVFX;
                if (attackEffect == null || !attackEffect.GetComponent<SpriteRenderer>().enabled)
                    throw new InvalidOperationException("감전 대상의 재피격에 감전 이펙트가 없습니다.");
                attackEffect.Stop();
                float previousRotation = float.NaN;
                foreach (float size in new[] { 0.5f, 1f, 2f, 4f, -4f })
                {
                    target.transform.localScale = new Vector3(size, Mathf.Abs(size), 1f);
                    Physics2D.SyncTransforms();
                    effect.Play(body);
                    Vector2 initialOffset = (Vector2)(renderer.bounds.center - collider.bounds.center);
                    float initialRotation = effect.transform.eulerAngles.z;
                    float maximumOffset = new SerializedObject(data.shockPulsePrefab).FindProperty("centerOffsetRatio").floatValue;
                    if (initialOffset.magnitude <= 0f || initialOffset.magnitude > Mathf.Min(collider.bounds.size.x, collider.bounds.size.y) * maximumOffset + 0.001f ||
                        (!float.IsNaN(previousRotation) && Mathf.Abs(Mathf.DeltaAngle(previousRotation, initialRotation)) < 0.001f))
                        throw new InvalidOperationException("감전 위치·각도의 무작위 선택 검증 실패");
                    previousRotation = initialRotation;
                    for (int frame = 0; frame < 6; frame++)
                    {
                        elapsed.SetValue(effect, (frame + 0.01f) / 18f);
                        effect.Play(body);
                        update.Invoke(effect, null);
                        Bounds visible = collider.bounds;
                        if (!renderer.enabled || renderer.sprite != frames[frame] ||
                            Vector2.Distance((Vector2)(renderer.bounds.center - visible.center), initialOffset) > 0.001f ||
                            Mathf.Abs(Mathf.DeltaAngle(effect.transform.eulerAngles.z, initialRotation)) > 0.001f ||
                            Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y) < 0.5f - 0.001f ||
                            Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y) > 1.4f + 0.001f ||
                            renderer.sortingOrder != Mathf.Max(WeaponVFXHelper.HitEffectSortingOrder + 1, body.sortingOrder + 2))
                            throw new InvalidOperationException($"허수아비 {size}배 감전 프레임 {frame}의 위치·회전·크기·정렬 검증 실패");
                    }
                    elapsed.SetValue(effect, 1f);
                    update.Invoke(effect, null);
                    if (renderer.enabled || effect.enabled) throw new InvalidOperationException("재생 완료 후 감전이 남습니다.");
                    effect.Play(body);
                    target.SetActive(false);
                    // 편집 모드의 프리뷰 씬에서는 런타임 비활성화 콜백을 직접 호출합니다.
                    typeof(ShockPulseVFX).GetMethod("OnDisable", flags).Invoke(effect, null);
                    if (renderer.enabled) throw new InvalidOperationException("대상 비활성화 후 감전이 남습니다.");
                    target.SetActive(true);
                }
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                EditorSceneManager.ClosePreviewScene(preview);
            }
            File.WriteAllText(Output + "/verification.txt",
                $"PASS 감전 6프레임, 공통 슬라이스 {frames[0].rect.width}×{frames[0].rect.height}, 18fps, 약 0.33초\n" +
                "PASS 0.5/1/2/4배·좌우 반전 대상의 무작위 위치·각도, 최소·최대 표시 크기·정렬\n" +
                "PASS 재생 중 위치·각도 유지, 재생마다 새로운 각도 선택\n" +
                "PASS 감전 부여·시간 경과만으로 이펙트를 생성하지 않음\n" +
                "PASS 감전이 없는 대상에는 미재생, 피해량 0을 포함한 감전 대상의 직접 타격에 재생\n" +
                "PASS 빠른 재호출 시 프레임 유지, 재생 완료·비활성화 콜백의 정지, 재사용\n" +
                "PASS 데이터베이스 프리팹 참조, 추가 충돌체 없음\n");
        }
    }
}
