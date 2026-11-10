using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>메테오 스킬 전용 낙하·착탄 시트와 애니메이션을 연결합니다.</summary>
    [InitializeOnLoad]
    public static class MeteorStrikeFallSetup
    {
        private const string Sheet = "Assets/Nytherion/Art/Combat/VFX/Sprites/MeteorStrikeSheet.png";
        private const string ClipPath = "Assets/Nytherion/Art/Combat/VFX/Animations/MeteorStrikeFall.anim";
        private const string ExplosionSheet = Sheet;
        private const string ExplosionClipPath = "Assets/Nytherion/Art/Combat/VFX/Animations/MeteorStrikeExplosion.anim";
        private const string ControllerPath = "Assets/Nytherion/Art/Combat/VFX/Animations/MeteorStrikeSkill.overrideController";
        private const string Request = MeteorStrikeSkillSetup.Output + "/fall-setup.request";

        static MeteorStrikeFallSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            File.Delete(Request);
            Connect();
        }

        [MenuItem("Tools/Nytherion/Meteor Strike/Connect Fall And Explosion Sprite Sheets")]
        public static void Connect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(MeteorStrikeSkillSetup.Output);
            try
            {
                AssetDatabase.ImportAsset(Sheet, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(Sheet);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 48f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                // 바닥과 운석이 합쳐지는 6번째 프레임부터 착탄으로 재생합니다.
                // 폭발 바닥 중심은 셀 중앙보다 3픽셀 왼쪽에 있습니다.
                Slice(importer, "MeteorStrike", 25, new Vector2(45f / 96f, 6f / 96f));
                Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().OrderBy(frame => frame.name).ToArray();
                if (frames.Length != 25) throw new InvalidOperationException("통합 시트의 25프레임 분할에 실패했습니다.");
                Sprite[] fallFrames = frames.Take(5).ToArray();

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                if (clip == null)
                {
                    clip = new AnimationClip { name = "MeteorStrikeFall" };
                    AssetDatabase.CreateAsset(clip, ClipPath);
                }
                clip.frameRate = 12f;
                AnimationUtility.SetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
                    fallFrames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 12f, value = frame }).ToArray());
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0f;
                settings.stopTime = fallFrames.Length / clip.frameRate;
                // 긴 낙하에서는 마지막 프레임을 유지합니다.
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
                Save(clip);
                AnimationClip explosion = CreateExplosion();

                RuntimeAnimatorController original = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Nytherion/Art/Combat/VFX/Animations/Meteor.controller");
                AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerPath);
                if (controller == null)
                {
                    controller = new AnimatorOverrideController(original);
                    AssetDatabase.CreateAsset(controller, ControllerPath);
                }
                controller.runtimeAnimatorController = original;
                controller["Fall"] = clip;
                controller["Explode"] = explosion;
                Save(controller);

                var data = AssetDatabase.LoadAssetAtPath<Nytherion.Data.ScriptableObjects.Skill.MeteorStrikeSkillData>(
                    MeteorStrikeSkillSetup.DataPath);
                if (data == null) throw new MissingReferenceException("메테오 스트라이크 스킬 데이터가 없습니다.");
                // 폭발의 지면을 적 몸체 중심보다 아래에 맞춥니다.
                data.explosionYOffset = 0.5f;
                Save(data);

                GameObject root = PrefabUtility.LoadPrefabContents(MeteorStrikeSkillSetup.ImpactPath);
                try
                {
                    root.GetComponent<Animator>().runtimeAnimatorController = controller;
                    root.GetComponent<SpriteRenderer>().sprite = fallFrames[0];
                    PrefabUtility.SaveAsPrefabAsset(root, MeteorStrikeSkillSetup.ImpactPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }

                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                if (keys.Length != 5 || !keys.Select(key => key.value).SequenceEqual(fallFrames.Cast<UnityEngine.Object>()) ||
                    AnimationUtility.GetAnimationClipSettings(clip).loopTime ||
                    !Mathf.Approximately(clip.length, 5f / 12f))
                    throw new InvalidOperationException("낙하 애니메이션의 5프레임 단발 검증에 실패했습니다.");
                ObjectReferenceKeyframe[] explosionKeys = AnimationUtility.GetObjectReferenceCurve(explosion,
                    EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeteorStrikeSkillSetup.ImpactPath);
                if (!Mathf.Approximately(data.explosionYOffset, 0.5f) ||
                    frames.Skip(5).Any(frame => !Mathf.Approximately(frame.pivot.x, 45f) ||
                        !Mathf.Approximately(frame.pivot.y, 6f)) ||
                    explosionKeys.Length != 20 ||
                    !explosionKeys.Select(key => key.value).SequenceEqual(frames.Skip(5).Cast<UnityEngine.Object>()) ||
                    AnimationUtility.GetAnimationClipSettings(explosion).loopTime ||
                    !Mathf.Approximately(explosion.length, 20f / 12f) ||
                    prefab.GetComponent<Animator>().runtimeAnimatorController != controller ||
                    prefab.GetComponent<SpriteRenderer>().sprite != fallFrames[0] ||
                    controller["Fall"] != clip || controller["Explode"] != explosion ||
                    !AnimationUtility.GetAnimationEvents(explosion).Any(evt => evt.functionName == "ApplyDamage" && Mathf.Approximately(evt.time, 0.1f)) ||
                    !AnimationUtility.GetAnimationEvents(explosion).Any(evt => evt.functionName == "DisableProjectile" &&
                        Mathf.Approximately(evt.time, 20f / 12f - 0.001f)))
                    throw new InvalidOperationException("낙하·폭발 교체 또는 피해·반환 이벤트 검증에 실패했습니다.");
                File.WriteAllText(MeteorStrikeSkillSetup.Output + "/fall-setup.txt",
                    "PASS Unity 컴파일·통합 768x384 시트 25프레임·낙하 5프레임(머리 피벗/바닥 제외)·착탄/폭발 20프레임 단발·12fps·폭발 피벗 (45,6)·착탄 아래쪽 보정 0.5·스킬 전용 프리팹 연결·착탄 0.1초 피해·폭발 종료 후 반환\n");
            }
            catch (Exception error)
            {
                File.WriteAllText(MeteorStrikeSkillSetup.Output + "/fall-setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        private static AnimationClip CreateExplosion()
        {
            AssetDatabase.ImportAsset(ExplosionSheet, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(ExplosionSheet);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 48f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(ExplosionSheet).OfType<Sprite>().OrderBy(frame => frame.name).Skip(5).ToArray();
            if (frames.Length != 20) throw new InvalidOperationException("착탄·폭발 시트의 20프레임 분할에 실패했습니다.");
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ExplosionClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "MeteorStrikeExplosion" };
                AssetDatabase.CreateAsset(clip, ExplosionClipPath);
            }
            clip.frameRate = 12f;
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
                frames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 12f, value = frame }).ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f;
            settings.stopTime = frames.Length / clip.frameRate;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent { time = 0.1f, functionName = "ApplyDamage" },
                new AnimationEvent { time = frames.Length / 12f - 0.001f, functionName = "DisableProjectile" }
            });
            Save(clip);
            return clip;
        }

        private static void Slice(TextureImporter importer, string prefix, int count, Vector2 pivot)
        {
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            SpriteRect[] previous = provider.GetSpriteRects();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);
            if (texture.width != 768 || texture.height != 384)
                throw new InvalidOperationException("메테오 통합 시트는 768x384 크기여야 합니다.");
            // 위쪽 원점 기준 머리 중심과 바닥 표시 직전까지의 높이입니다.
            Vector2[] heads = { new Vector2(87, 7), new Vector2(81, 20), new Vector2(73, 34),
                new Vector2(66, 48), new Vector2(59, 61) };
            int[] heights = { 80, 80, 80, 80, 78 };
            SpriteRect[] rects = Enumerable.Range(0, count).Select(index =>
            {
                string name = prefix + "_" + index.ToString("D2");
                SpriteRect old = previous.FirstOrDefault(rect => rect.name == name);
                int height = index < 5 ? heights[index] : 96;
                return new SpriteRect
                {
                    name = name, rect = new Rect(index % 8 * 96, texture.height - (index / 8 * 96 + height), 96, height),
                    alignment = SpriteAlignment.Custom,
                    pivot = index < 5 ? new Vector2(heads[index].x / 96f, 1f - heads[index].y / height) : pivot,
                    spriteID = old != null ? old.spriteID : GUID.Generate()
                };
            }).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static void Save(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
