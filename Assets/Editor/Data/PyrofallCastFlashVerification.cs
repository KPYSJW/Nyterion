using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Editor
{
    public static class PyrofallCastFlashVerification
    {
        public static void Run()
        {
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(PyrofallWeaponSetup.DataPath);
            GameObject weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PyrofallWeaponSetup.WeaponPath);
            Require(data.fireEffectPrefab != null && weaponPrefab.transform.Find("Visual/CastFlash") == null,
                "발사 프리팹 연결 및 기존 자식 제거");
            SpriteMuzzleFlashEffect saved = data.fireEffectPrefab.GetComponent<SpriteMuzzleFlashEffect>();
            var serialized = new SerializedObject(saved);
            AnimationClip clip = (AnimationClip)serialized.FindProperty("animationClip").objectReferenceValue;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"));
            Require(keys.Length == 7 && Mathf.Approximately(clip.frameRate, 14f) &&
                Mathf.Approximately(clip.length, 0.5f) &&
                Mathf.Approximately(AnimationUtility.GetAnimationEvents(clip).Single().time, 0.5f),
                "7프레임·14 FPS·마지막 프레임 유지");

            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject poolRoot = new GameObject("발사 연출 검증 풀");
                SceneManager.MoveGameObjectToScene(poolRoot, scene);
                ObjectPoolManager pool = poolRoot.AddComponent<ObjectPoolManager>();
                GameObject weapon = UnityEngine.Object.Instantiate(weaponPrefab);
                SceneManager.MoveGameObjectToScene(weapon, scene);
                Transform visual = weapon.transform.Find("Visual");
                SpriteRenderer weaponRenderer = visual.GetComponent<SpriteRenderer>();
                weaponRenderer.sortingOrder = 17;
                var expected = new GameObject("기존 자식 위치").transform;
                expected.SetParent(visual, false);
                expected.localPosition = new Vector3(0f, 0.68f, 0f);
                expected.localScale = Vector3.one * 0.75f;
                GameObject effect = pool.SpawnFromPool(data.fireEffectPrefab, visual.position, visual.rotation, 1);
                SpriteMuzzleFlashEffect flash = effect.GetComponent<SpriteMuzzleFlashEffect>();
                MethodInfo follow = typeof(SpriteMuzzleFlashEffect).GetMethod("FollowMuzzle", BindingFlags.Instance | BindingFlags.NonPublic);
                SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
                foreach (float facing in new[] { 1f, -1f })
                {
                    weapon.transform.localScale = new Vector3(facing * 1.2f, 1.2f, 1.2f);
                    weapon.transform.localRotation = Quaternion.Euler(0f, 0f, facing * 55f);
                    if (!effect.activeSelf) effect = pool.SpawnFromPool(data.fireEffectPrefab, visual.position, visual.rotation, 1);
                    flash.Play(pool, visual);
                    follow.Invoke(flash, null);
                    Require(Vector3.Distance(effect.transform.position, expected.position) < 0.0001f &&
                        Vector3.Distance(effect.transform.lossyScale, expected.lossyScale) < 0.0001f &&
                        Quaternion.Angle(effect.transform.rotation, expected.rotation) < 0.001f &&
                        renderer.sortingOrder == 18, "좌우 반전·위치·크기·표시 순서");
                    Require(renderer.sprite == keys[0].value, "첫 프레임부터 재생");
                    flash.AnimationFinished();
                    Require(!effect.activeSelf && pool.poolDictionary[data.fireEffectPrefab.name].Count == 1,
                        "재생 완료 후 풀 반환 및 재사용");
                }
                effect = pool.SpawnFromPool(data.fireEffectPrefab, visual.position, visual.rotation, 1);
                flash.Play(pool, visual);
                weapon.SetActive(false);
                typeof(SpriteMuzzleFlashEffect).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flash, null);
                Require(!effect.activeSelf, "무기 비활성화 시 연출 반환");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Directory.CreateDirectory("output/pyrofall-cast-flash");
            File.WriteAllText("output/pyrofall-cast-flash/verification.txt",
                "PASS 프리팹 연결·기존 자식 제거\nPASS 7프레임·14 FPS·0.5초\nPASS 좌우 위치·크기·회전·표시 순서\nPASS 첫 프레임·풀 반환·재사용·무기 비활성화\n");
            Debug.Log("[PyrofallCastFlashVerification] PASS");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
