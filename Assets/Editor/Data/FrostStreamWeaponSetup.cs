using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class FrostStreamWeaponSetup
    {
        private const string SheetPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/Weapons/FrostStream/FrostStream.png";
        private const string StartupSheetPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/Weapons/FrostStream/FrostStreamStart.png";
        private const string HitSheetPath = "Assets/Nytherion/Art/Combat/VFX/Sprites/Weapons/FrostStream/FrostStreamHit.png";
        private const string HitPrefabPath = "Assets/Prefabs/Gameplay/Combat/VFX/FrostStreamHitEffect.prefab";
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/FrostStream.asset";
        public const string PrefabPath = "Assets/Prefabs/Gameplay/Combat/Weapons/Generated/FrostStream.prefab";
        private const string DatabasePath = "Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset";
        private const string Output = "output/frost-stream";

        static FrostStreamWeaponSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/setup.request")) return;
            string request = File.ReadAllText(Output + "/setup.request").Trim();
            bool updateMuzzle = request == "muzzle-offset";
            File.Delete(Output + "/setup.request");
            try
            {
                if (request == "combat-tuning")
                {
                    ApplyCombatTuning();
                    return;
                }
                if (updateMuzzle)
                {
                    WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataPath);
                    if (data == null) throw new InvalidOperationException("냉기 방사 무기 데이터 누락");
                    data.firePointOffset = new Vector3(0.3f, -0.25f, 0f);
                    EditorUtility.SetDirty(data);
                }
                Apply();
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Frost Stream/냉기 방사 무기 생성")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            WeaponData reference = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/Frenzy.asset");
            ItemDatabaseSO database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DatabasePath);
            if (reference == null || database == null || LayerMask.NameToLayer("Enemy") < 0)
                throw new InvalidOperationException("Frenzy 데이터, 아이템 데이터베이스 또는 Enemy 레이어 누락");
            string[] databaseLines = File.ReadAllLines(DatabasePath);
            Sprite[] frames = ImportFrames(SheetPath, "FrostStream", 4, 2);
            Sprite[] startupFrames = ImportFrames(StartupSheetPath, "FrostStreamStart", 5, 1);
            Sprite frostParticleSource = AssetDatabase.LoadAllAssetsAtPath("Assets/Nytherion/Art/Combat/VFX/Sprites/StatusEffects/ChillStatusSheet.png")
                .OfType<Sprite>().FirstOrDefault(sprite => sprite.name == "ChillStatusSheet_00");
            if (frostParticleSource == null) throw new InvalidOperationException("ChillStatusSheet 첫 프레임 누락");
            FrostStreamEmissionFrame[] emissionFrames = BakeEmissionPoints(startupFrames.Concat(frames).ToArray());
            Sprite[] hitFrames = ImportFrames(HitSheetPath, "FrostStreamHit", 5, 1, 32, 32, true);
            GameObject hitEffect = CreateHitEffect(hitFrames);

            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataPath);
            if (data == null)
            {
                data = UnityEngine.Object.Instantiate(reference);
                data.name = "FrostStream";
                SerializedObject serialized = new SerializedObject(data);
                serialized.FindProperty("uniqueID").stringValue = Guid.NewGuid().ToString();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                data.damage = 2f;
                data.cooldown = 0.4f;
                data.range = 4f;
                data.firePointOffset = new Vector3(0.3f, -0.25f, 0f);
                data.traits = new List<EquipmentTrait> { EquipmentTrait.Ice };
                data.statModifiers = new List<Nytherion.Core.Data.StatModifier>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.itemName_KR = "냉기 방사기";
            data.itemName_EN = "Frost Stream";
            data.description_KR = "전방으로 냉기를 방사해 범위 안의 적에게 지속적으로 피해를 줍니다.";
            data.description_EN = "Sprays frost forward, repeatedly damaging enemies within the stream.";
            data.weaponEffectPrefab = null;
            data.fireEffectPrefab = null;
            data.chargedFireEffectPrefab = null;
            data.chargeEffectPrefab = null;
            data.projectilePrefab = null;
            data.chargedProjectilePrefab = null;
            data.hitEffectPrefab = hitEffect;
            data.animatorController = null;
            data.fireSound = null;
            data.isArchivable = false;

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            Scene preview = exists ? default : EditorSceneManager.NewPreviewScene();
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("FrostStream");
            GameObject prefab;
            try
            {
                if (!exists) SceneManager.MoveGameObjectToScene(root, preview);
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = data.weaponSprite;
                renderer.sharedMaterial = reference.weaponPrefab.GetComponent<SpriteRenderer>().sharedMaterial;
                FrostStreamWeapon weapon = root.GetComponent<FrostStreamWeapon>();
                if (weapon == null) weapon = root.AddComponent<FrostStreamWeapon>();
                weapon.weaponData = data;
                Transform firePoint = root.transform.Find("FirePoint");
                if (firePoint == null)
                {
                    firePoint = new GameObject("FirePoint").transform;
                    firePoint.SetParent(root.transform, false);
                }
                firePoint.localPosition = data.firePointOffset;
                weapon.firePoint = firePoint;
                SerializedObject serialized = new SerializedObject(weapon);
                serialized.FindProperty("frostParticleSource").objectReferenceValue = frostParticleSource;
                SerializedProperty emissionArray = serialized.FindProperty("particleEmissionFrames");
                emissionArray.arraySize = emissionFrames.Length;
                for (int i = 0; i < emissionFrames.Length; i++)
                {
                    SerializedProperty entry = emissionArray.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("sprite").objectReferenceValue = emissionFrames[i].sprite;
                    SerializedProperty points = entry.FindPropertyRelative("points");
                    points.arraySize = emissionFrames[i].points.Length;
                    for (int j = 0; j < points.arraySize; j++)
                        points.GetArrayElementAtIndex(j).vector2Value = emissionFrames[i].points[j];
                }
                SerializedProperty array = serialized.FindProperty("frames");
                array.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                SerializedProperty startupArray = serialized.FindProperty("startupFrames");
                startupArray.arraySize = startupFrames.Length;
                for (int i = 0; i < startupFrames.Length; i++) startupArray.GetArrayElementAtIndex(i).objectReferenceValue = startupFrames[i];
                serialized.FindProperty("enemyLayers").intValue = LayerMask.GetMask("Enemy");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("무기 프리팹 저장 실패");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(preview);
            }
            data.weaponPrefab = prefab.GetComponent<FrostStreamWeapon>();
            EditorUtility.SetDirty(data);
            if (!database.allItems.Contains(data))
            {
                database.allItems.Add(data);
                EditorUtility.SetDirty(database);
            }
            AssetDatabase.SaveAssets();
            // 기존 Missing 참조가 이번 데이터베이스 저장으로 지워지지 않도록 보존합니다.
            string[] saved = File.ReadAllLines(DatabasePath);
            bool restored = false;
            for (int i = 0; i < Mathf.Min(databaseLines.Length, saved.Length); i++)
            {
                if (!databaseLines[i].StartsWith("  - {fileID: 11400000, guid:") || saved[i] != "  - {fileID: 0}") continue;
                saved[i] = databaseLines[i];
                restored = true;
            }
            if (restored) File.WriteAllLines(DatabasePath, saved);
            VerifyInertia(frames, startupFrames);
            VerifyResidue(frostParticleSource, emissionFrames[0]);
            File.WriteAllText(Output + "/setup.txt", "PASS 시작 5프레임 → 반복 8프레임 / 왼쪽 총구 / Enemy 윤곽 판정\n" +
                $"damage={data.damage}, interval={data.cooldown}, range={data.range}, fps=16\n" +
                string.Join("\n", startupFrames.Concat(frames).Select(frame => frame.name + ": shapes=" + frame.GetPhysicsShapeCount())));
            Debug.Log("[FrostStreamWeaponSetup] 냉기 방사기 생성 및 참조 검증 완료", data);
        }

        private static void VerifyInertia(Sprite[] frames, Sprite[] startupFrames)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = new GameObject("냉기 관성 계산 검증");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                // 실제 런타임처럼 기존 SpriteRenderer가 있는 상태에서 메시를 생성합니다.
                root.AddComponent<SpriteRenderer>();
                PolygonCollider2D collider = root.AddComponent<PolygonCollider2D>();
                collider.enabled = false;
                FrostStreamDeformation bend = root.AddComponent<FrostStreamDeformation>();
                if (!bend.SetVisible(true) || root.GetComponent<MeshFilter>() != null ||
                    root.GetComponentInChildren<MeshRenderer>() == null)
                    throw new InvalidOperationException("스프라이트와 곡선 메시의 분리 생성 실패");
                bend.ResetDirection(Vector2.right);
                bend.Draw(frames[0], Vector2.right, 4f, 0.2f, 65f, 1f / 60f, collider, 0, 0);
                Mesh mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                Vector3[] vertices = mesh.vertices;
                Vector3 tip = (vertices[vertices.Length - 1] + vertices[vertices.Length - 2]) * 0.5f;
                if (Vector2.Distance(tip, Vector2.right * 4f) > 0.001f)
                    throw new InvalidOperationException("초기 직선 길이 불일치");
                Vector2[] previousPhysicsPath = collider.GetPath(0);
                bend.Draw(frames[0], Vector2.up, 4f, 0.2f, 65f, 1f / 60f, collider, 0, 0, 1f, false);
                if (!previousPhysicsPath.SequenceEqual(collider.GetPath(0)))
                    throw new InvalidOperationException("피해 틱 사이에 물리 윤곽이 갱신됨");
                vertices = mesh.vertices;
                Vector3 origin = (vertices[0] + vertices[1]) * 0.5f;
                tip = (vertices[vertices.Length - 1] + vertices[vertices.Length - 2]) * 0.5f;
                if (origin.sqrMagnitude > 0.000001f || tip.x <= 0.1f || tip.y <= 0.1f)
                    throw new InvalidOperationException("총구 고정 또는 끝부분 지연 불일치");
                for (int i = 0; i < 240; i++)
                    bend.Draw(frames[0], Vector2.up, 4f, 0.2f, 65f, 1f / 60f, collider, 0, 0);
                vertices = mesh.vertices;
                tip = (vertices[vertices.Length - 1] + vertices[vertices.Length - 2]) * 0.5f;
                if (Vector2.Distance(tip, Vector2.up * 4f) > 0.01f)
                    throw new InvalidOperationException("조준 정지 후 직선 복귀 실패");
                if (previousPhysicsPath.SequenceEqual(collider.GetPath(0)))
                    throw new InvalidOperationException("피해 틱에 물리 윤곽 갱신 누락");
                foreach (Sprite frame in startupFrames.Concat(frames))
                {
                    bend.Draw(frame, Vector2.up, 4f, 0.2f, 65f, 1f / 60f, collider, 0, 0);
                    if (collider.pathCount != frame.GetPhysicsShapeCount())
                        throw new InvalidOperationException("프레임 전환 시 피해 윤곽 누락: " + frame.name);
                    for (int path = 0; path < collider.pathCount; path++)
                    {
                        foreach (Vector2 point in collider.GetPath(path))
                            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y))
                                throw new InvalidOperationException("피해 윤곽에 유효하지 않은 좌표: " + frame.name);
                    }
                }
                // 좌우 조준 시 무기와 같은 폭 방향 반전을 적용해 이미지 높이를 유지합니다.
                Sprite sample = frames[0];
                Vector2 samplePoint = new Vector2(sample.bounds.center.x, sample.bounds.max.y * 0.5f);
                bend.ResetDirection(Vector2.right);
                bend.Draw(sample, Vector2.right, 4f, 0.2f, 65f, 1f / 60f, collider, 0, 0, -1f);
                Vector2 rightPoint = bend.TransformSpritePoint(sample, samplePoint, 4f);
                bend.ResetDirection(Vector2.left);
                bend.Draw(sample, Vector2.left, 4f, 0.2f, 65f, 1f / 60f, collider, 0, 0, 1f);
                Vector2 leftPoint = bend.TransformSpritePoint(sample, samplePoint, 4f);
                if (Mathf.Abs(rightPoint.y - leftPoint.y) > 0.001f || Mathf.Abs(rightPoint.x + leftPoint.x) > 0.001f)
                    throw new InvalidOperationException("좌우 조준 시 방사 이미지 높이 대칭 불일치");
                File.WriteAllText(Output + "/inertia-verification.txt",
                    "PASS 메시와 스프라이트 공존 / 총구 고정 / 관성 및 직선 복귀 / 13프레임 윤곽 / 좌우 대칭 / 피해 틱에만 물리 윤곽 갱신\n");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static FrostStreamEmissionFrame[] BakeEmissionPoints(Sprite[] frames)
        {
            var result = new List<FrostStreamEmissionFrame>();
            foreach (Sprite frame in frames)
            {
                Texture2D readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    // 원본 PNG만 읽어 위치를 굽고, 런타임 텍스처의 Read/Write 설정은 유지합니다.
                    if (!readable.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(frame.texture))))
                        throw new InvalidOperationException("방사 원본 픽셀 읽기 실패: " + frame.name);
                    Color32[] pixels = readable.GetPixels32();
                    Rect rect = frame.rect;
                    var candidates = new List<Vector2>();
                    for (int y = 1; y < (int)rect.height - 1; y++)
                    {
                        for (int x = 1; x < (int)rect.width - 1; x++)
                        {
                            int index = ((int)rect.y + y) * readable.width + (int)rect.x + x;
                            // 가장자리와 투명 구멍을 피하도록 주변 3×3 픽셀도 불투명한 지점을 고릅니다.
                            bool opaque = true;
                            for (int dy = -1; dy <= 1 && opaque; dy++)
                                for (int dx = -1; dx <= 1; dx++)
                                    if (pixels[index + dy * readable.width + dx].a < 200) { opaque = false; break; }
                            if (opaque) candidates.Add((new Vector2(x + 0.5f, y + 0.5f) - frame.pivot) / frame.pixelsPerUnit);
                        }
                    }
                    if (candidates.Count == 0) throw new InvalidOperationException("입자를 생성할 불투명 영역 없음: " + frame.name);
                    int count = Mathf.Min(128, candidates.Count);
                    Vector2[] points = new Vector2[count];
                    for (int i = 0; i < count; i++) points[i] = candidates[(int)((i + 0.5f) * candidates.Count / count)];
                    result.Add(new FrostStreamEmissionFrame { sprite = frame, points = points });
                }
                finally { UnityEngine.Object.DestroyImmediate(readable); }
            }
            return result.ToArray();
        }

        private static void VerifyResidue(Sprite sourceSprite, FrostStreamEmissionFrame emissionFrame)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = new GameObject("서리 입자 수명 검증");
            SceneManager.MoveGameObjectToScene(root, preview);
            GameObject source = new GameObject("검증용 방사 윤곽");
            SceneManager.MoveGameObjectToScene(source, preview);
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            try
            {
                PolygonCollider2D collider = source.AddComponent<PolygonCollider2D>();
                collider.enabled = false;
                collider.SetPath(0, new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                FrostStreamResidue residue = root.AddComponent<FrostStreamResidue>();
                residue.Initialize(sourceSprite);
                source.transform.position = new Vector3(3f, 2f, 0f);
                residue.EmitFrom(source.transform, emissionFrame.sprite, emissionFrame.points, null,
                    4f, Vector2.right, 35f, 32f, 0.75f, 1f, 0.1f, 0, 0);
                ParticleSystem particles = root.GetComponent<ParticleSystem>();
                ParticleSystem.TextureSheetAnimationModule animation = particles.textureSheetAnimation;
                if (animation.spriteCount != 3 || animation.GetSprite(0).texture != sourceSprite.texture)
                    throw new InvalidOperationException("ChillStatusSheet 눈 결정 3종 연결 실패");
                int count = particles.particleCount;
                if (count == 0 || particles.main.simulationSpace != ParticleSystemSimulationSpace.World ||
                    root.GetComponents<Collider2D>().Length != 0)
                    throw new InvalidOperationException("월드 공간 서리 입자 생성 또는 비전투 효과 설정 실패");
                ParticleSystem.Particle[] emitted = new ParticleSystem.Particle[count];
                particles.GetParticles(emitted);
                foreach (ParticleSystem.Particle particle in emitted)
                {
                    Vector2 local = source.transform.InverseTransformPoint(particle.position);
                    if (!emissionFrame.points.Any(point => Vector2.Distance(point, local) < 0.0001f) ||
                        Vector2.Angle(Vector2.right, particle.velocity) > 35.001f)
                        throw new InvalidOperationException("이미지 내부 생성 또는 전방 부채꼴 방향 검증 실패");
                }
                if (root.GetComponent<ParticleSystemRenderer>().sortingOrder != 0)
                    throw new InvalidOperationException("방사 이미지 아래 입자 정렬 실패");
                residue.Release();
                if (particles.particleCount != count)
                    throw new InvalidOperationException("발사 종료 시 남은 서리 입자가 즉시 지워짐");
                particles.Simulate(1.5f, true, false, false);
                if (particles.particleCount != 0)
                    throw new InvalidOperationException("서리 입자 수명 종료 후 소멸 실패");
                File.WriteAllText(Output + "/residue-verification.txt",
                    "PASS ChillStatusSheet 눈 결정 3종 / 불투명 픽셀 내부 생성 / 전방 ±35도 부채꼴 / 방사 이미지 아래 정렬 / 월드 공간 유지 / 방사 종료 후 잔존 및 소멸\n");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void ApplyCombatTuning()
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataPath);
            if (data == null) throw new InvalidOperationException("냉기 방사 무기 데이터 누락");
            GameObject root = PrefabUtility.LoadPrefabContents(HitPrefabPath);
            try
            {
                root.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                if (PrefabUtility.SaveAsPrefabAsset(root, HitPrefabPath) == null)
                    throw new InvalidOperationException("타격 이펙트 크기 저장 실패");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            data.cooldown = 0.4f;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Vector3 scale = AssetDatabase.LoadAssetAtPath<GameObject>(HitPrefabPath).transform.localScale;
            if (Mathf.Abs(scale.x - 0.5f) > 0.0001f || Mathf.Abs(scale.y - 0.5f) > 0.0001f ||
                Mathf.Abs(data.cooldown - 0.4f) > 0.0001f)
                throw new InvalidOperationException("타격 크기 또는 공격 간격 저장값 불일치");
            File.WriteAllText(Output + "/combat-tuning.txt", "PASS 타격 이펙트 크기 0.5배 / 피해 간격 0.4초\n");
        }

        private static GameObject CreateHitEffect(Sprite[] frames)
        {
            const string folder = "Assets/Nytherion/Art/Combat/VFX/Animations/FrostStreamHit";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Nytherion/Art/Combat/VFX/Animations", "FrostStreamHit");
            string clipPath = folder + "/FrostStreamHit.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "FrostStreamHit", frameRate = 20f };
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i] };
            float duration = frames.Length / clip.frameRate;
            keys[frames.Length] = new ObjectReferenceKeyframe { time = duration, value = null };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent
            {
                time = duration, functionName = "AnimationFinished"
            } });
            EditorUtility.SetDirty(clip);
            string controllerPath = folder + "/FrostStreamHit.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.FirstOrDefault(child => child.state.name == "FrostStreamHit").state;
            if (state == null) state = machine.AddState("FrostStreamHit");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(HitPrefabPath) != null;
            Scene preview = exists ? default : EditorSceneManager.NewPreviewScene();
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(HitPrefabPath) : new GameObject("FrostStreamHitEffect");
            try
            {
                if (!exists)
                {
                    SceneManager.MoveGameObjectToScene(root, preview);
                    root.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                }
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sortingOrder = WeaponVFXHelper.HitEffectSortingOrder;
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                string materialPath = folder + "/FrostStreamHit.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                renderer.sharedMaterial = material;
                Animator animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                PooledVFXAnimationEvent lifetime = root.GetComponent<PooledVFXAnimationEvent>();
                if (lifetime == null) lifetime = root.AddComponent<PooledVFXAnimationEvent>();
                SerializedObject serialized = new SerializedObject(lifetime);
                serialized.FindProperty("poolTag").stringValue = "FrostStreamHitEffect";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HitPrefabPath);
                if (prefab == null || AnimationUtility.GetAnimationEvents(clip).Length != 1 ||
                    AnimationUtility.GetAnimationClipSettings(clip).loopTime)
                    throw new InvalidOperationException("냉기 타격 프리팹 또는 종료 이벤트 연결 실패");
                File.WriteAllText(Output + "/hit-verification.txt",
                    $"PASS 타격 시트 {frames.Length}프레임 / {clip.frameRate}fps / {duration}초 단발 / 종료 시 풀 반환 이벤트\n");
                return prefab;
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static Sprite[] ImportFrames(string path, string prefix, int columns, int rows,
            int width = 96, int height = 40, bool centered = false)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.width != columns * width || texture.height != rows * height)
                throw new InvalidOperationException("냉기 방사 시트 크기 확인 필요: " + path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = true;
            importer.SetTextureSettings(settings);
            SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] previous = provider.GetSpriteRects();
            SpriteRect[] rects = new SpriteRect[columns * rows];
            for (int i = 0; i < rects.Length; i++)
            {
                string name = prefix + "_" + i.ToString("D2");
                SpriteRect old = previous.FirstOrDefault(rect => rect.name == name);
                rects[i] = new SpriteRect
                {
                    name = name, rect = new Rect(i % columns * width, rows * height - (i / columns + 1) * height, width, height),
                    alignment = SpriteAlignment.Custom, pivot = new Vector2(centered ? 0.5f : 0f, 0.5f),
                    spriteID = old != null ? old.spriteID : GUID.Generate()
                };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            if (frames.Length != columns * rows || frames.Any(frame => frame.GetPhysicsShapeCount() == 0))
                throw new InvalidOperationException("프레임 또는 이미지 윤곽 판정 생성 실패: " + path);

            return frames;
        }
    }
}
