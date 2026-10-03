using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class GuardianStaffWeaponSetup
    {
        public const string Output = "output/guardian-staff";
        public const string DataPath = "Assets/Nytherion/Data/ScriptableObjects/Weapons/GuardianStaff.asset";
        public const string WeaponPath = "Assets/Prefabs/Gameplay/Combat/Weapons/Generated/GuardianStaff.prefab";
        public const string EffectPrefix = "Assets/Prefabs/Gameplay/Combat/VFX/GuardianStaffAttackEffect";
        public const string HitEffectPath = "Assets/Prefabs/Gameplay/Combat/VFX/GuardianStaffHitEffect.prefab";
        private const string HitAnimationFolder = "Assets/Nytherion/Art/Combat/VFX/Animations/GuardianStaffHitEffect";
        private const string AttackAnimationFolder = "Assets/Nytherion/Art/Combat/VFX/Animations/GuardianStaffAttackEffect";
        private const string SpriteFolder = "Assets/Nytherion/Art/Combat/Weapons/Sprites/";
        private const string EffectFolder = "Assets/Nytherion/Art/Combat/VFX/Sprites/";
        private const string DatabasePath = "Assets/Nytherion/Data/ScriptableObjects/Items/ItemDatabaseSO.asset";

        static GuardianStaffWeaponSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/fourth-radius.request"))
            {
                File.Delete(Output + "/fourth-radius.request");
                try { IncreaseFourthAttackRadius(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/fourth-radius.txt", error.ToString());
                    Debug.LogException(error);
                }
                return;
            }
            if (File.Exists(Output + "/attack-setup.request"))
            {
                File.Delete(Output + "/attack-setup.request");
                try { UpdateAttackEffects(); }
                catch (Exception error)
                {
                    File.WriteAllText(Output + "/attack-setup.txt", error.ToString());
                    Debug.LogException(error);
                }
                return;
            }
            if (!File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { CreateAssets(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", error.ToString());
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Guardian Staff/Create Assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            WeaponData reference = AssetDatabase.LoadAssetAtPath<WeaponData>(BlazeshadeWeaponSetup.DataPath);
            GameObject referencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlazeshadeWeaponSetup.WeaponPath);
            string[] originalDatabaseLines = File.ReadAllLines(DatabasePath);
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(DatabasePath);
            var rarePool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>("Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Weapon/Rare_Weapon.asset");
            if (reference == null || referencePrefab == null || database == null || rarePool == null || LayerMask.NameToLayer("Enemy") < 0)
                throw new InvalidOperationException("Blazeshade, 아이템 데이터베이스, 뽑기 풀 또는 Enemy 레이어 누락");

            Sprite weaponSprite = SingleSprite(SpriteFolder + "Guardian'sStaff.png");
            Sprite icon = SingleSprite(SpriteFolder + "Guardian'sStaff_Icon.png");
            GuardianStaffAttackEffect[] effects = new GuardianStaffAttackEffect[4];
            for (int i = 0; i < effects.Length; i++) effects[i] = CreateEffect(i + 1);
            GameObject hitEffect = CreateHitEffect();

            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataPath);
            if (data == null)
            {
                data = UnityEngine.Object.Instantiate(reference);
                data.name = "GuardianStaff";
                var serialized = new SerializedObject(data);
                serialized.FindProperty("uniqueID").stringValue = Guid.NewGuid().ToString();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                data.damage = 10f;
                data.cooldown = 1f;
                data.range = effects.Max(effect => effect.NativeRadius) * 2f;
                data.traits = new List<EquipmentTrait>();
                data.statModifiers = new List<Nytherion.Core.Data.StatModifier>();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            data.itemName_KR = "수호자의 지팡이";
            data.itemName_EN = "Guardian's Staff";
            data.description_KR = "공격할 때마다 네 가지 수호의 원을 순서대로 펼쳐 플레이어 주변 원형 범위 안의 적을 한 번씩 타격합니다.";
            data.description_EN = "Cycles through four guardian circles, striking each enemy within the circle around the player once per attack.";
            data.weaponSprite = weaponSprite;
            data.icon = icon;
            data.weaponType = WeaponType.Ranged;
            data.spriteRotationOffset = reference.spriteRotationOffset;
            data.visualPositionOffset = reference.visualPositionOffset;
            data.visualScale = reference.visualScale;
            data.sortingOrderOffset = reference.sortingOrderOffset;
            data.firePointOffset = reference.firePointOffset;
            data.useStaffRecoil = reference.useStaffRecoil;
            data.weaponEffectPrefab = null;
            data.fireEffectPrefab = null;
            data.chargeEffectPrefab = null;
            data.animatorController = null;
            data.projectilePrefab = null;
            data.hitEffectPrefab = hitEffect;
            data.isArchivable = false;

            GameObject prefab = SaveObject(WeaponPath, "GuardianStaff", root =>
            {
                root.transform.localPosition = referencePrefab.transform.localPosition;
                root.transform.localRotation = referencePrefab.transform.localRotation;
                root.transform.localScale = referencePrefab.transform.localScale;
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                SpriteRenderer referenceRenderer = referencePrefab.GetComponent<SpriteRenderer>();
                renderer.sprite = weaponSprite;
                renderer.sharedMaterial = referenceRenderer.sharedMaterial;
                renderer.sortingLayerID = referenceRenderer.sortingLayerID;
                renderer.sortingOrder = referenceRenderer.sortingOrder;
                var weapon = root.GetComponent<GuardianStaffWeapon>();
                if (weapon == null) weapon = root.AddComponent<GuardianStaffWeapon>();
                weapon.weaponData = data;
                var serialized = new SerializedObject(weapon);
                SerializedProperty array = serialized.FindProperty("attackEffects");
                array.arraySize = effects.Length;
                for (int i = 0; i < effects.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
                serialized.FindProperty("enemyLayers").intValue = LayerMask.GetMask("Enemy");
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
            data.weaponPrefab = prefab.GetComponent<GuardianStaffWeapon>();
            EditorUtility.SetDirty(data);
            if (database.allItems == null) database.allItems = new List<ItemData>();
            if (!database.allItems.Contains(data)) { database.allItems.Add(data); EditorUtility.SetDirty(database); }
            if (rarePool.items == null) rarePool.items = new List<GachaItemRate>();
            if (!rarePool.items.Any(entry => entry.item == data))
            {
                rarePool.items.Add(new GachaItemRate { item = data, weight = 100 });
                EditorUtility.SetDirty(rarePool);
            }
            AssetDatabase.SaveAssets();
            PreserveUnresolvedReferences(DatabasePath, originalDatabaseLines);
            File.WriteAllText(Output + "/setup.txt", $"PASS Guardian Staff 에셋 및 데이터베이스/희귀 뽑기 연결\n" +
                $"위치={data.visualPositionOffset}, 각도={data.spriteRotationOffset}, 크기={data.visualScale}\n" +
                $"피해={data.damage}, 쿨다운={data.cooldown}, 이펙트/타격 반지름={data.range}\n");
            Debug.Log("[GuardianStaffWeaponSetup] 수호자의 지팡이 생성 완료", data);
        }

        private static Sprite SingleSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("무기 이미지 누락: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void PreserveUnresolvedReferences(string path, string[] originalLines)
        {
            // Unity 재직렬화가 요청과 무관한 기존 Missing 참조를 지우지 않도록 유지합니다.
            string[] savedLines = File.ReadAllLines(path);
            bool changed = false;
            for (int i = 0; i < Mathf.Min(originalLines.Length, savedLines.Length); i++)
            {
                if (originalLines[i].StartsWith("  - {fileID: 11400000, guid:") && savedLines[i] == "  - {fileID: 0}")
                {
                    savedLines[i] = originalLines[i];
                    changed = true;
                }
            }
            if (changed) File.WriteAllLines(path, savedLines);
        }

        [MenuItem("Tools/Nytherion/Guardian Staff/Update Attack Animators")]
        public static void UpdateAttackEffects()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(Output);
            GuardianStaffAttackEffect[] effects = Enumerable.Range(1, 4).Select(CreateEffect).ToArray();
            SaveObject(WeaponPath, "GuardianStaff", root =>
            {
                var serialized = new SerializedObject(root.GetComponent<GuardianStaffWeapon>());
                SerializedProperty array = serialized.FindProperty("attackEffects");
                array.arraySize = effects.Length;
                for (int i = 0; i < effects.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(DataPath);
            data.description_KR = "공격할 때마다 네 가지 수호의 원을 순서대로 펼쳐 플레이어 주변 원형 범위 안의 적을 한 번씩 타격합니다.";
            data.description_EN = "Cycles through four guardian circles, striking each enemy within the circle around the player once per attack.";
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Output + "/attack-setup.txt", "PASS 공격 이펙트 1~4 Animator 연결, 무기 순환 1→2→3→4→1");
        }

        [MenuItem("Tools/Nytherion/Guardian Staff/Increase Fourth Attack Radius")]
        public static void IncreaseFourthAttackRadius()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            SaveObject(EffectPrefix + "4.prefab", "GuardianStaffAttackEffect4", root =>
            {
                var serialized = new SerializedObject(root.GetComponent<GuardianStaffAttackEffect>());
                serialized.FindProperty("radiusMultiplier").floatValue = 1.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/fourth-radius.txt", "PASS 4번 공격만 표시 크기/판정 반지름 1.5배, 기존 Native Radius 유지");
        }

        private static GuardianStaffAttackEffect CreateEffect(int number)
        {
            string path = EffectFolder + $"Guardian'sStaffAttackEffect{number}.png";
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(sprite => int.Parse(sprite.name.Substring(sprite.name.LastIndexOf('_') + 1))).ToArray();
            int expected = number == 1 ? 4 : number == 4 ? 6 : 14;
            if (frames.Length != expected) throw new InvalidOperationException($"이펙트 {number} 프레임 누락: {frames.Length}/{expected}");
            float radius = frames.Max(frame => Mathf.Max(frame.bounds.size.x, frame.bounds.size.y) * 0.5f);
            EnsureFolder(AttackAnimationFolder);
            string name = "GuardianStaffAttackEffect" + number;
            string clipPath = AttackAnimationFolder + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                float frameRate = number == 1 ? 14f : 28f;
                // 기존 Inspector 속도는 Animator 클립의 Samples 값으로 옮깁니다.
                string prefabPath = EffectPrefix + number + ".prefab";
                if (File.Exists(prefabPath))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(prefabPath), @"framesPerSecond: ([\d.]+)");
                    if (match.Success) frameRate = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                }
                clip = new AnimationClip { name = name, frameRate = frameRate };
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i] };
            keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / clip.frameRate, value = null };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            string controllerPath = AttackAnimationFolder + "/" + name + ".controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.FirstOrDefault(child => child.state.name == name).state;
            if (state == null) state = machine.AddState(name);
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return SaveObject(EffectPrefix + number + ".prefab", "GuardianStaffAttackEffect" + number, root =>
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                renderer.sortingOrder = 100;
                var effect = root.GetComponent<GuardianStaffAttackEffect>();
                if (effect == null) effect = root.AddComponent<GuardianStaffAttackEffect>();
                Animator animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                var serialized = new SerializedObject(effect);
                serialized.FindProperty("nativeRadius").floatValue = radius;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }).GetComponent<GuardianStaffAttackEffect>();
        }

        private static GameObject CreateHitEffect()
        {
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(EffectFolder + "Guardian'sStaffHitEffect.png")
                .OfType<Sprite>().OrderBy(sprite => int.Parse(sprite.name.Substring(sprite.name.LastIndexOf('_') + 1))).ToArray();
            if (frames.Length != 4) throw new InvalidOperationException("수호자의 지팡이 타격 이펙트 4프레임을 확인해 주세요.");
            EnsureFolder(HitAnimationFolder);
            string clipPath = HitAnimationFolder + "/GuardianStaffHit.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "GuardianStaffHit", frameRate = 14f };
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i] };
            // 마지막 프레임도 한 프레임만 표시한 뒤 비웁니다. 풀 반환이 다음 프레임에 처리돼도 잔상이 남지 않습니다.
            keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / clip.frameRate, value = null };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);

            string controllerPath = HitAnimationFolder + "/GuardianStaffHit.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.FirstOrDefault(child => child.state.name == "GuardianStaffHit").state;
            if (state == null) state = machine.AddState("GuardianStaffHit");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return SaveObject(HitEffectPath, "GuardianStaffHitEffect", root =>
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                renderer.sortingOrder = 10;
                Animator animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                if (root.GetComponent<AutoReturnToPool>() == null) root.AddComponent<AutoReturnToPool>();
            });
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        private static GameObject SaveObject(string path, string name, Action<GameObject> configure)
        {
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            Scene preview = existing ? default : EditorSceneManager.NewPreviewScene();
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
            try
            {
                if (!existing) SceneManager.MoveGameObjectToScene(root, preview);
                configure(root);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || prefab == null) throw new InvalidOperationException("프리팹 저장 실패: " + path);
                return prefab;
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
