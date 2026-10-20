using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 프리팹의 풀 재사용과 광선 초기화 후 정렬을 플레이 모드에서 확인합니다.</summary>
    [InitializeOnLoad]
    public static class WeaponPresentationVerification
    {
        private const string Key = "Nytherion.WeaponPresentationVerification";
        private const string Output = WeaponPresentationSetup.Output;
        private static double readyAt;

        static WeaponPresentationVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                    readyAt = EditorApplication.timeSinceStartup + 5d;
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    PlayerSettings.productName = SessionState.GetString(Key + ".Product", PlayerSettings.productName);
                    SessionState.SetBool(Key, false);
                    File.AppendAllText(Output + "/verification.txt", "PASS 원래 제품명 및 세이브 경로 복원\n");
                }
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(Output + "/verify.request") && !SessionState.GetBool(Key, false))
            {
                File.Delete(Output + "/verify.request");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    File.WriteAllText(Output + "/verification.txt", "NOT_RUN 편집 모드에서 실행해 주세요.");
                    return;
                }
                SessionState.SetString(Key + ".Product", PlayerSettings.productName);
                SessionState.SetBool(Key, true);
                PlayerSettings.productName += "_WeaponPresentationVerification_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                File.WriteAllText(Output + "/verification.txt", "RUNNING 격리 세이브 플레이 모드\n");
                EditorApplication.isPlaying = true;
                return;
            }
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || readyAt == 0d ||
                EditorApplication.timeSinceStartup < readyAt) return;
            readyAt = 0d;
            try { Verify(); }
            catch (Exception error) { File.AppendAllText(Output + "/verification.txt", "FAIL " + error + "\n"); }
            finally { EditorApplication.isPlaying = false; }
        }

        private static void Verify()
        {
            File.AppendAllText(Output + "/verification.txt", "씬: " + SceneManager.GetActiveScene().name + "\n");
            var temporary = new List<Object>();
            FieldInfo instance = typeof(ObjectPoolManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            ObjectPoolManager previousPool = ObjectPoolManager.Instance;
            instance.SetValue(null, null);
            try
            {
                GameObject poolRoot = new GameObject("[WeaponPresentationVerification] Pool");
                temporary.Add(poolRoot);
                ObjectPoolManager pool = poolRoot.AddComponent<ObjectPoolManager>();
                pool.Initialize();
                int enemyMin = int.MaxValue, enemyMax = int.MinValue;
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Characters/Enemies" }))
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    foreach (SpriteRenderer renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        Require(renderer.sortingLayerID == 0, "적 정렬 레이어 변경 감지: " + prefab.name);
                        enemyMin = Mathf.Min(enemyMin, renderer.sortingOrder);
                        enemyMax = Mathf.Max(enemyMax, renderer.sortingOrder);
                    }
                }
                Require(enemyMin > WeaponVFXHelper.BackgroundBeamSortingOrder && enemyMax < WeaponVFXHelper.HitEffectSortingOrder,
                    $"적 정렬 범위와 효과가 겹칩니다: {enemyMin}..{enemyMax}");
                Pass($"실제 적 프리팹 정렬 범위 {enemyMin}..{enemyMax}, 소멸 광선 {WeaponVFXHelper.BackgroundBeamSortingOrder}, 타격 100");
                foreach (var tilemap in Object.FindObjectsOfType<UnityEngine.Tilemaps.TilemapRenderer>())
                    if (tilemap.gameObject.layer == LayerMask.NameToLayer("Floor"))
                        Require(tilemap.sortingOrder < WeaponVFXHelper.BackgroundBeamSortingOrder, "소멸 광선이 바닥 뒤에 있습니다.");
                Pass("소멸 광선은 실제 씬의 바닥 타일보다 앞");

                var effects = AssetDatabase.FindAssets("t:WeaponData")
                    .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(data => data != null && data.hitEffectPrefab != null)
                    .Select(data => data.hitEffectPrefab)
                    .Concat(new[] { AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Combat/VFX/IcicleHitVFX.prefab") }).Distinct();
                foreach (GameObject prefab in effects)
                {
                    GameObject effect = pool.SpawnFromPool(prefab, Vector3.one * 20000f, Quaternion.identity, 1);
                    pool.ReturnToPool(prefab.name, effect);
                    WeaponVFXHelper.PlayHitEffect(prefab, Vector3.one * 20000f);
                    Require(effect.activeSelf, "풀의 동일 타격 객체가 재사용되지 않았습니다: " + prefab.name);
                    foreach (Renderer renderer in effect.GetComponentsInChildren<Renderer>(true))
                        Require(renderer.sortingOrder > enemyMax, "타격 효과가 적에게 가립니다: " + prefab.name);
                    pool.ReturnToPool(prefab.name, effect);
                    Pass(prefab.name + " 풀 재사용 후 타격 정렬");
                }

                WeaponData icicleData = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/Icicle.asset");
                GameObject icicle = Object.Instantiate(icicleData.projectilePrefab);
                temporary.Add(icicle);
                var icicleRenderer = icicle.GetComponent<SpriteRenderer>();
                int flightOrder = icicleRenderer.sortingOrder;
                GameObject target = new GameObject("[WeaponPresentationVerification] Enemy");
                temporary.Add(target);
                target.tag = "Enemy";
                target.transform.position = icicle.transform.position;
                icicle.GetComponent<IcicleProj>().OnHit(target.AddComponent<BoxCollider2D>());
                Require(icicleRenderer.sortingOrder > enemyMax, "고드름 충돌 애니메이션이 적 뒤에 있습니다.");
                icicle.SetActive(false);
                icicle.SetActive(true);
                Require(icicleRenderer.sortingOrder == flightOrder, "고드름 재사용 시 발사 정렬이 복원되지 않았습니다.");
                Pass("고드름: IcicleHitVFX 생성, 충돌 애니메이션 전경 표시 및 재사용 시 발사 정렬 복원");

                CrimsonLotusWeaponData laserData = AssetDatabase.LoadAssetAtPath<CrimsonLotusWeaponData>("Assets/Nytherion/Data/ScriptableObjects/Weapons/CrimsonLotus.asset");
                CrimsonLotusWeapon laser = Object.Instantiate(laserData.weaponPrefab) as CrimsonLotusWeapon;
                temporary.Add(laser.gameObject);
                laser.Initialize(laserData);
                laser.transform.position = new Vector3(20000f, 20000f, 0f);
                laser.GetComponentInChildren<SpriteRenderer>().sortingOrder = 500;
                CrimsonLotusBeam beam = Object.Instantiate(laserData.projectilePrefab).GetComponent<CrimsonLotusBeam>();
                temporary.Add(beam.gameObject);
                beam.Initialize(laser, laser.firePoint, Vector2.right, laserData, 0f, pool);
                Require(laserData.itemName_KR == "홍련" && laserData.itemName_EN == "CrimsonLotus", "홍련 이름 교환 누락");
                Require(beam.GetComponentInChildren<LineRenderer>().sortingOrder < enemyMin, "홍련 광선이 적보다 앞에 있습니다.");
                Require(beam.transform.Find("EndEffect").GetComponent<SpriteRenderer>().sortingOrder > enemyMax, "홍련 타격 효과가 적 뒤에 있습니다.");
                Pass("홍련: 연속 광선 이름 및 타격 효과 전경 정렬");
                BeamRenderingVerification.Run(laser, beam);

                ObliterationWeaponData lotusData = AssetDatabase.LoadAssetAtPath<ObliterationWeaponData>(ObliterationWeaponSetup.DataPath);
                ObliterationWeapon lotus = Object.Instantiate(lotusData.weaponPrefab) as ObliterationWeapon;
                temporary.Add(lotus.gameObject);
                lotus.Initialize(lotusData);
                lotus.transform.position = new Vector3(20000f, 20000f, 0f);
                lotus.GetComponent<SpriteRenderer>().sortingOrder = 500;
                ObliterationBeam lotusBeam = Object.Instantiate(lotusData.projectilePrefab).GetComponent<ObliterationBeam>();
                temporary.Add(lotusBeam.gameObject);
                lotusBeam.Initialize(lotus, lotus.firePoint, Vector2.right, lotusData, 4, 0f, pool);
                Require(lotusData.itemName_KR == "소멸" && lotusData.itemName_EN == "Obliteration", "소멸 이름 교환 누락");
                Require(lotusBeam.transform.Find("Visual").GetComponent<SpriteRenderer>().sortingOrder < enemyMin, "소멸 공격이 적보다 앞에 있습니다.");
                Require(lotusBeam.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder < enemyMin, "소멸 공격 그룹이 적보다 앞에 있습니다.");
                Require(lotusBeam.transform.Find("EndEffect").GetComponent<SpriteRenderer>().sortingOrder > enemyMax, "소멸 타격 효과가 적 뒤에 있습니다.");
                Pass("소멸: 충전 광선 이름, 공격 배경 및 타격 효과 전경 정렬");
                BeamRenderingVerification.Run(lotus, lotusBeam);
            }
            finally
            {
                for (int i = temporary.Count - 1; i >= 0; i--) if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
                instance.SetValue(null, previousPool);
                Pass("검증 객체 정리 및 기존 오브젝트 풀 복원");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Pass(string message) => File.AppendAllText(Output + "/verification.txt", "PASS " + message + "\n");
    }
}
