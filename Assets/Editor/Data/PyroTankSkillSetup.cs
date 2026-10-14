using System;
using System.Collections.Generic;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>원본 시트에서 애니메이션을 만들고 폭열 전차의 프리팹과 획득 경로를 연결합니다.</summary>
    [InitializeOnLoad]
    public static class PyroTankSkillSetup
    {
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Skill/PyroTank_Skill.asset";
        public const string TankPrefabPath = "Assets/Prefabs/Gameplay/Skills/PyroTank.prefab";
        public const string SkillPrefabPath = "Assets/Prefabs/Gameplay/Skills/PyroTankSkill.prefab";
        public const string RelicPath = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/Relic_PyroTank.asset";
        public const string ArtRoot = "Assets/Nytherion/Art/Skills/PyroTank";
        public const string AnimationRoot = ArtRoot + "/Animations";

        static PyroTankSkillSetup()
        {
            EditorApplication.delayCall += SetupIfMissing;
        }

        private static void SetupIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += SetupIfMissing;
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<PyroTankSkillData>(DataPath) == null &&
                AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Sprites/PyroTank_Idle.png") != null)
                CreateAssets();
        }

        [MenuItem("Tools/Nytherion/Pyro Tank/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureFolder(AnimationRoot);
            EnsureFolder("Assets/Prefabs/Gameplay/Skills");
            Sprite[] idle = ConfigureSheet("PyroTank_Idle", 32, new Vector2(0.5f, 0.14f));
            Sprite[] walk = ConfigureSheet("PyroTank_Walk", 32, new Vector2(0.5f, 0.14f));
            Sprite[] explosion = ConfigureSheet("PyroTankExplosionEffect", 64, new Vector2(0.5f, 0.07f));
            AnimationClip idleClip = CreateClip("PyroTank_Idle", idle, 8f, true);
            AnimationClip walkClip = CreateClip("PyroTank_Walk", walk, 12f, true);
            AnimationClip explosionClip = CreateClip("PyroTank_Explosion", explosion, 12f, false);
            RuntimeAnimatorController tankController = CreateTankAnimator(idleClip, walkClip);
            RuntimeAnimatorController explosionController = CreateExplosionAnimator(explosionClip);

            PyroTankSkillData data = AssetDatabase.LoadAssetAtPath<PyroTankSkillData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<PyroTankSkillData>();
                data.skillID = "skill_pyro_tank";
                data.skillType = SkillType.PyroTank;
                data.skillName = "폭열 전차";
                data.description = "플레이어 위치에 폭열 전차를 소환합니다. 주변에서 가장 가까운 적에게 돌진하고, 적이나 벽에 닿거나 수명이 다하면 폭발하여 범위 피해를 줍니다. 표적이 사라지면 다시 탐색하며 적이 없으면 정지합니다.";
                data.skillLevel = 1;
                data.coolDown = 8f;
                data.damage = 25f;
                data.range = 8f;
                data.enemyLayers = LayerMask.GetMask("Enemy");
                data.obstacleLayers = LayerMask.GetMask("Wall", "Obstacle");
                AssetDatabase.CreateAsset(data, DataPath);
            }
            // 재실행 시 Inspector에서 조정한 밸런스와 기존 GUID를 보존합니다.
            data.icon = idle[0];
            data.explosionAnimation = explosionClip;
            GameObject tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TankPrefabPath);
            if (tankPrefab == null) tankPrefab = CreateTankPrefab(idle[0], explosion[0], tankController, explosionController);
            UpdateTankCollider();
            data.tankPrefab = tankPrefab.GetComponent<PyroTankController>();
            GameObject skillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPrefabPath);
            if (skillPrefab == null) skillPrefab = CreateSkillPrefab(data);
            data.skillPrefab = skillPrefab;
            Save(data);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Sprites/PyroTank_Jump.png") != null)
                UpdateJumpAssets();

            SkillDatabaseSO database = Require<SkillDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Skill/SkillDatabaseSO.asset");
            if (database.allSkills == null) database.allSkills = new List<SkillData>();
            if (!database.allSkills.Contains(data)) { database.allSkills.Add(data); Save(database); }
            AddToPool(Require<GachaPoolSO>("Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Skill/SkillGachaPool.asset"), data, 1);

            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(RelicPath);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = "Relic of Pyro Tank";
                relic.koreanName = "폭열 전차 각인";
                relic.description_KR = "[폭열 전차] 스킬을 얻습니다.\n[폭열 전차] : " + data.description;
                relic.description_EN = "Grants Pyro Tank. A tank charges at the nearest enemy and explodes on contact or when its lifetime expires.";
                relic.Image = idle[0];
                relic.rarity = Rarity.Common;
                relic.effectModules = new List<RelicEffectModule>
                {
                    new RelicEffectModule { effects = new List<RelicEffectBase> { new GrantSkillEffect { skillData = data } } }
                };
                AssetDatabase.CreateAsset(relic, RelicPath);
            }
            RelicDatabaseSO relicDatabase = Require<RelicDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset");
            if (relicDatabase.allRelics == null) relicDatabase.allRelics = new List<RelicData>();
            if (!relicDatabase.allRelics.Contains(relic)) { relicDatabase.allRelics.Add(relic); Save(relicDatabase); }
            AddToPool(Require<GachaPoolSO>("Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Relic/Common_Relic.asset"), relic, 100);
            Debug.Log("[PyroTankSkillSetup] 폭열 전차 스킬, Idle·Walk·폭발 애니메이션 및 획득 경로 연결 완료.", data);
        }

        private static Sprite[] ConfigureSheet(string name, int frameSize, Vector2 pivot)
        {
            string path = ArtRoot + "/Sprites/" + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new MissingReferenceException("스프라이트 원본이 없습니다: " + path);
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
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            Texture2D texture = Require<Texture2D>(path);
            if (texture.height != frameSize || texture.width % frameSize != 0)
                throw new InvalidOperationException("스프라이트는 정사각형 프레임을 가로 한 줄로 배치해 주세요: " + path);
            SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            List<SpriteNameFileIdPair> previous = names.GetNameFileIdPairs().ToList();
            SpriteRect[] previousRects = provider.GetSpriteRects();
            SpriteRect[] rects = Enumerable.Range(0, texture.width / frameSize).Select(index =>
            {
                string frameName = name + "_" + index.ToString("00");
                SpriteNameFileIdPair pair = previous.FirstOrDefault(item => item.name == frameName);
                SpriteRect old = previousRects.FirstOrDefault(item => item.name == frameName);
                return new SpriteRect
                {
                    name = frameName, rect = new Rect(index * frameSize, 0, frameSize, frameSize),
                    alignment = old != null ? old.alignment : SpriteAlignment.Custom,
                    pivot = old != null ? old.pivot : pivot,
                    spriteID = pair != null ? pair.GetFileGUID() : GUID.Generate()
                };
            }).ToArray();
            provider.SetSpriteRects(rects);
            names.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.rect.x).ToArray();
        }

        [MenuItem("Tools/Nytherion/Pyro Tank/Update Jump Animation")]
        public static void UpdateJumpAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureFolder(AnimationRoot);
            Sprite[] frames = ConfigureSheet("PyroTank_Jump", 32, new Vector2(0.5f, 0.14f));
            AnimationClip clip = CreateJumpClip(frames);
            string controllerPath = AnimationRoot + "/PyroTankJump.controller";
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller == null)
            {
                AnimatorController created = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                AnimatorStateMachine machine = created.layers[0].stateMachine;
                AnimatorState state = machine.AddState("Jump");
                state.motion = clip;
                machine.defaultState = state;
                Save(created);
                controller = created;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(TankPrefabPath);
            try
            {
                Transform visual = root.transform.Find("JumpVisual");
                if (visual == null)
                {
                    GameObject child = new GameObject("JumpVisual");
                    child.transform.SetParent(root.transform, false);
                    visual = child.transform;
                    child.AddComponent<SpriteRenderer>();
                    child.AddComponent<Animator>();
                }
                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                SpriteRenderer tankRenderer = root.GetComponent<SpriteRenderer>();
                renderer.sharedMaterial = tankRenderer.sharedMaterial;
                renderer.sortingLayerID = tankRenderer.sortingLayerID;
                renderer.sortingOrder = tankRenderer.sortingOrder;
                Animator animator = visual.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                visual.localPosition = Vector3.zero;
                visual.gameObject.SetActive(false);
                SerializedObject serialized = new SerializedObject(root.GetComponent<PyroTankController>());
                serialized.FindProperty("jumpVisual").objectReferenceValue = visual;
                serialized.FindProperty("jumpRenderer").objectReferenceValue = renderer;
                serialized.FindProperty("jumpAnimator").objectReferenceValue = animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, TankPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            PyroTankSkillData data = Require<PyroTankSkillData>(DataPath);
            if (data.jumpAnimation == null) data.jumpAnimation = clip;
            Save(data);
            Debug.Log("[PyroTankSkillSetup] 점프 등장 애니메이션과 프리팹 참조 연결 완료.", data);
        }

        private static AnimationClip CreateJumpClip(Sprite[] frames)
        {
            if (frames.Length != 4) throw new InvalidOperationException("점프 시트에는 상승·최고점·하강·착지 4프레임이 필요합니다.");
            string path = AnimationRoot + "/PyroTank_Jump.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip { name = "PyroTank_Jump" };
            clip.frameRate = 10f;
            // 네 프레임의 길이를 같게 하고 착지 프레임이 중복돼 길어지는 것을 막습니다.
            ObjectReferenceKeyframe[] keys = frames.Select((frame, index) =>
                new ObjectReferenceKeyframe { time = index / clip.frameRate, value = frame }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip,
                new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f;
            settings.stopTime = frames.Length / clip.frameRate;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (isNew) AssetDatabase.CreateAsset(clip, path);
            else Save(clip);
            return clip;
        }

        private static AnimationClip CreateClip(string name, Sprite[] frames, float fps, bool loop)
        {
            string path = AnimationRoot + "/" + name + ".anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) return existing;
            AnimationClip clip = new AnimationClip { name = name, frameRate = fps };
            // 마지막 프레임의 재생 시간까지 포함해 루프와 폭발 종료 길이를 정확히 맞춥니다.
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[Mathf.Min(i, frames.Length - 1)] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static RuntimeAnimatorController CreateTankAnimator(AnimationClip idle, AnimationClip walk)
        {
            string path = AnimationRoot + "/PyroTank.controller";
            RuntimeAnimatorController existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            if (existing != null) return existing;
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = machine.AddState("Idle");
            idleState.motion = idle;
            AnimatorState walkState = machine.AddState("Walk");
            walkState.motion = walk;
            machine.defaultState = idleState;
            Transition(idleState, walkState, AnimatorConditionMode.If);
            Transition(walkState, idleState, AnimatorConditionMode.IfNot);
            Save(controller);
            return controller;
        }

        private static void Transition(AnimatorState from, AnimatorState to, AnimatorConditionMode condition)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.AddCondition(condition, 0f, "IsMoving");
        }

        private static RuntimeAnimatorController CreateExplosionAnimator(AnimationClip clip)
        {
            string path = AnimationRoot + "/PyroTankExplosion.controller";
            RuntimeAnimatorController existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
            if (existing != null) return existing;
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.AddState("Explosion");
            state.motion = clip;
            machine.defaultState = state;
            Save(controller);
            return controller;
        }

        private static GameObject CreateTankPrefab(Sprite idle, Sprite explosion,
            RuntimeAnimatorController tankController, RuntimeAnimatorController explosionController)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("PyroTank");
                SceneManager.MoveGameObjectToScene(root, preview);
                root.transform.localScale = new Vector3(2f, 2f, 1f);
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = idle;
                renderer.sortingOrder = 10;
                Animator animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = tankController;
                Rigidbody2D body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.offset = Vector2.zero;
                collider.radius = 0.22f;
                GameObject visual = new GameObject("ExplosionVisual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                SpriteRenderer explosionRenderer = visual.AddComponent<SpriteRenderer>();
                explosionRenderer.sprite = explosion;
                explosionRenderer.sortingOrder = 11;
                Animator explosionAnimator = visual.AddComponent<Animator>();
                explosionAnimator.runtimeAnimatorController = explosionController;
                visual.SetActive(false);
                PyroTankController tank = root.AddComponent<PyroTankController>();
                SerializedObject serialized = new SerializedObject(tank);
                serialized.FindProperty("tankRenderer").objectReferenceValue = renderer;
                serialized.FindProperty("tankAnimator").objectReferenceValue = animator;
                serialized.FindProperty("explosionVisual").objectReferenceValue = visual;
                serialized.FindProperty("explosionAnimator").objectReferenceValue = explosionAnimator;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, TankPrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static GameObject CreateSkillPrefab(PyroTankSkillData data)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = new GameObject("PyroTankSkill");
                SceneManager.MoveGameObjectToScene(root, preview);
                root.AddComponent<PyroTankSkill>().skillData = data;
                return PrefabUtility.SaveAsPrefabAsset(root, SkillPrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [MenuItem("Tools/Nytherion/Pyro Tank/Align Tank Collider")]
        public static void UpdateTankCollider()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject prefab = Require<GameObject>(TankPrefabPath);
            if (prefab.GetComponent<CircleCollider2D>().offset == Vector2.zero) return;
            GameObject root = PrefabUtility.LoadPrefabContents(TankPrefabPath);
            try
            {
                root.GetComponent<CircleCollider2D>().offset = Vector2.zero;
                PrefabUtility.SaveAsPrefabAsset(root, TankPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static T Require<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new MissingReferenceException("필수 에셋이 없습니다: " + path);
            return asset;
        }

        private static void AddToPool(GachaPoolSO pool, ScriptableObject item, int weight)
        {
            if (pool.items == null) pool.items = new List<GachaItemRate>();
            if (pool.items.Any(entry => entry.item == item)) return;
            pool.items.Add(new GachaItemRate { item = item, weight = weight });
            Save(pool);
        }

        private static void Save(Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
