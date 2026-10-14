using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    public static class WeaponFireEffectAssets
    {
        public static Sprite[] LoadFrames(Texture2D sheet)
        {
            if (sheet == null) return Array.Empty<Sprite>();
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(sheet)).OfType<Sprite>()
                .OrderByDescending(sprite => sprite.rect.y).ThenBy(sprite => sprite.rect.x).ToArray();
        }

        public static void SliceGrid(Texture2D sheet, Vector2Int cell, float pixelsPerUnit)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 슬라이스해 주세요.");
            if (sheet == null || cell.x <= 0 || cell.y <= 0 || pixelsPerUnit <= 0f ||
                sheet.width % cell.x != 0 || sheet.height % cell.y != 0)
                throw new InvalidOperationException("이미지 크기가 프레임 가로·세로 크기로 정확히 나누어져야 합니다.");
            string path = AssetDatabase.GetAssetPath(sheet);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("프로젝트의 이미지 파일을 선택해 주세요.");
            int columns = sheet.width / cell.x, rows = sheet.height / cell.y;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] previous = provider.GetSpriteRects();
            var rects = new SpriteRect[columns * rows];
            string prefix = Path.GetFileNameWithoutExtension(path);
            for (int i = 0; i < rects.Length; i++)
            {
                var rect = new Rect(i % columns * cell.x, (rows - 1 - i / columns) * cell.y, cell.x, cell.y);
                SpriteRect existing = previous.FirstOrDefault(value => value.rect == rect);
                rects[i] = new SpriteRect
                {
                    name = existing != null ? existing.name : prefix + "_" + i,
                    rect = rect,
                    alignment = existing != null ? existing.alignment : SpriteAlignment.Center,
                    pivot = existing != null ? existing.pivot : new Vector2(0.5f, 0.5f),
                    spriteID = existing != null ? existing.spriteID : GUID.Generate()
                };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        public static GameObject CreateAndAssign(WeaponData weapon, Sprite[] frames, int first, int last,
            float fps, float scale, Vector3 offset, string prefabPath = null, string animationFolder = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 생성해 주세요.");
            if (weapon == null || frames == null || first < 0 || last < first || last >= frames.Length ||
                fps <= 0f || scale <= 0f || frames.Skip(first).Take(last - first + 1).Any(sprite => sprite == null))
                throw new InvalidOperationException("무기·프레임 범위·재생 속도·크기를 확인해 주세요.");

            GameObject previous = weapon.fireEffectPrefab;
            bool reusable = previous != null && previous.GetComponent<SpriteMuzzleFlashEffect>() != null;
            if (prefabPath == null)
            {
                string weaponPath = AssetDatabase.GetAssetPath(weapon);
                if (string.IsNullOrEmpty(weaponPath)) throw new InvalidOperationException("저장된 무기 데이터를 선택해 주세요.");
                string name = Path.GetFileNameWithoutExtension(weaponPath) + "MuzzleFlash";
                prefabPath = reusable ? AssetDatabase.GetAssetPath(previous)
                    : "Assets/Prefabs/Gameplay/Combat/VFX/" + name + ".prefab";
                animationFolder = "Assets/Nytherion/Art/Combat/VFX/Animations/Weapons/" + name;
                if (reusable)
                {
                    var existing = new SerializedObject(previous.GetComponent<SpriteMuzzleFlashEffect>());
                    var currentClip = existing.FindProperty("animationClip").objectReferenceValue;
                    if (currentClip != null)
                        animationFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(currentClip)).Replace('\\', '/');
                }
            }
            if (!prefabPath.StartsWith("Assets/") || string.IsNullOrEmpty(animationFolder) ||
                !animationFolder.StartsWith("Assets/"))
                throw new InvalidOperationException("저장 경로는 Assets 내부여야 합니다.");
            GameObject atPath = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (atPath != null && atPath.GetComponent<SpriteMuzzleFlashEffect>() == null)
                throw new InvalidOperationException("같은 경로에 다른 용도의 프리팹이 있습니다: " + prefabPath);
            EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
            EnsureFolder(animationFolder);
            string assetName = Path.GetFileNameWithoutExtension(prefabPath);
            string clipPath = animationFolder + "/" + assetName + ".anim";
            string controllerPath = animationFolder + "/" + assetName + ".controller";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            int count = last - first + 1;
            clip.frameRate = fps;
            clip.wrapMode = WrapMode.Once;
            var keys = new ObjectReferenceKeyframe[count];
            for (int i = 0; i < count; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[first + i] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            clipSettings.startTime = 0f;
            clipSettings.stopTime = count / fps;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent
                { time = count / fps, functionName = nameof(SpriteMuzzleFlashEffect.AnimationFinished) } });
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.Select(entry => entry.state).FirstOrDefault(entry => entry.name == "Fire");
            if (state == null) state = machine.AddState("Fire");
            state.motion = clip;
            state.speed = 1f;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject root = atPath != null ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject(assetName);
            GameObject saved;
            try
            {
                root.transform.localScale = Vector3.one * scale;
                var effect = root.GetComponent<SpriteMuzzleFlashEffect>() ?? root.AddComponent<SpriteMuzzleFlashEffect>();
                var serialized = new SerializedObject(effect);
                serialized.FindProperty("animationClip").objectReferenceValue = clip;
                serialized.FindProperty("muzzleOffset").vector3Value = offset;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = frames[first];
                if (renderer.sharedMaterial == null)
                    renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                renderer.enabled = true;
                var animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = false;
                saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                if (atPath != null) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
            Undo.RecordObject(weapon, "무기 발사 이미지 연결");
            weapon.fireEffectPrefab = saved;
            EditorUtility.SetDirty(weapon);
            AssetDatabase.SaveAssetIfDirty(weapon);
            return saved;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

