using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    public static class WeaponFireEffectVerification
    {
        public static void Run()
        {
            string folder = "Assets/Editor/FireEffectVerify_" + Guid.NewGuid().ToString("N");
            string report;
            try
            {
                AssetDatabase.CreateFolder("Assets/Editor", Path.GetFileName(folder));
                string imagePath = folder + "/TestSheet.png";
                File.Copy("Assets/Nytherion/Art/Combat/FrenzyMuzzleFlash.png", imagePath);
                AssetDatabase.ImportAsset(imagePath, ImportAssetOptions.ForceSynchronousImport);
                var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
                WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(64, 64), 32f);
                Sprite[] frames = WeaponFireEffectAssets.LoadFrames(sheet);
                Require(frames.Length == 6, "격자 슬라이스 6프레임");
                long[] ids = frames.Select(Id).ToArray();
                WeaponFireEffectAssets.SliceGrid(sheet, new Vector2Int(64, 64), 32f);
                frames = WeaponFireEffectAssets.LoadFrames(sheet);
                Require(ids.SequenceEqual(frames.Select(Id)), "재슬라이스 시 스프라이트 ID 보존");
                var weapon = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(weapon, folder + "/TestWeapon.asset");
                string prefabPath = folder + "/TestFlash.prefab";
                GameObject prefab = WeaponFireEffectAssets.CreateAndAssign(weapon, frames, 1, 3,
                    24f, 0.4f, new Vector3(0.1f, 0.05f), prefabPath, folder);
                Require(weapon.fireEffectPrefab == prefab, "무기 데이터 프리팹 자동 연결");
                var serialized = new SerializedObject(prefab.GetComponent<SpriteMuzzleFlashEffect>());
                var clip = (AnimationClip)serialized.FindProperty("animationClip").objectReferenceValue;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"));
                Require(keys.Select(key => key.value).SequenceEqual(frames.Skip(1).Take(3)), "선택한 프레임 범위 및 순서");
                Require(Mathf.Approximately(clip.length, 3f / 24f) &&
                    prefab.transform.localScale == Vector3.one * 0.4f &&
                    serialized.FindProperty("muzzleOffset").vector3Value == new Vector3(0.1f, 0.05f), "속도·크기·위치 설정");
                string guid = AssetDatabase.AssetPathToGUID(prefabPath);
                prefab = WeaponFireEffectAssets.CreateAndAssign(weapon, frames, 0, 1,
                    30f, 0.2f, Vector3.zero, prefabPath, folder);
                Require(AssetDatabase.AssetPathToGUID(prefabPath) == guid && weapon.fireEffectPrefab == prefab,
                    "수정 생성 시 프리팹 GUID와 무기 연결 유지");
                Require(Mathf.Approximately(prefab.GetComponent<SpriteMuzzleFlashEffect>().Duration, 2f / 30f),
                    "수정한 프레임 수와 속도 반영");
                report = "PASS 격자 슬라이스 및 ID 보존\nPASS 선택 범위·fps·크기·위치 반영\n" +
                    "PASS 무기 데이터 자동 연결 및 프리팹 GUID 유지\n";
            }
            catch (Exception error)
            {
                report = "FAIL " + error;
                Debug.LogException(error);
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            }
            File.WriteAllText(MuzzleFlashSetup.Output + "/authoring-verification.txt", report);
        }

        private static long Id(Sprite sprite)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id);
            return id;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
