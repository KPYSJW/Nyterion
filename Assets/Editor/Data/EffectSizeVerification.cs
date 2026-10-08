using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>원본 에셋을 수정하지 않고 크기 변경의 실제 판정과 풀 재사용을 검증합니다.</summary>
    [InitializeOnLoad]
    public static class EffectSizeVerification
    {
        private const string Output = "output/effect-size";
        private const string Pending = "Nytherion.EffectSize.Verify";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double readyAt;

        static EffectSizeVerification() => EditorApplication.update += Update;

        [MenuItem("Tools/Nytherion/Effect Size/Verify In Play Mode %#&e")]
        public static void Start()
        {
            SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
            SessionState.SetBool(Pending, true);
            readyAt = EditorApplication.timeSinceStartup + 5d;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists("effect-size.verify.request"))
            {
                File.Delete("effect-size.verify.request");
                Start();
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            if (readyAt == 0d) readyAt = EditorApplication.timeSinceStartup + 5d;
            if (EditorApplication.timeSinceStartup < readyAt) return;
            SessionState.SetBool(Pending, false);
            Directory.CreateDirectory(Output);
            try { File.WriteAllText(Output + "/verification.txt", Verify()); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL: " + error);
                Debug.LogException(error);
            }
            finally
            {
                if (SessionState.GetBool(Pending + ".Exit", false)) EditorApplication.isPlaying = false;
                readyAt = 0d;
            }
        }

        private static string Verify()
        {
            var temporary = new List<Object>();
            var results = new List<string>();
            GameObject root = new GameObject("[검증] 효과 범위");
            temporary.Add(root);
            root.transform.position = new Vector3(10000f, 10000f);
            PlayerManager player = root.AddComponent<PlayerManager>();
            player.enabled = false;
            PlayerData stats = ScriptableObject.CreateInstance<PlayerData>();
            temporary.Add(stats);
            player.currentPlayerData = stats;
            try
            {
                foreach (string name in new[] { "Blazeshade", "GuardianStaff" })
                {
                    WeaponData source = Load<WeaponData>(name);
                    WeaponData data = Object.Instantiate(source);
                    temporary.Add(data);
                    data.hitEffectPrefab = null;
                    WeaponBase weapon = Object.Instantiate(source.weaponPrefab, root.transform);
                    typeof(WeaponBase).GetField("playerManager", Private).SetValue(weapon, player);
                    weapon.Initialize(data);
                    weapon.enabled = false;
                    var targetObject = new GameObject("[검증] 기본 범위 밖의 적");
                    temporary.Add(targetObject);
                    targetObject.layer = LayerMask.NameToLayer("Enemy");
                    targetObject.transform.position = root.transform.position + Vector3.right * source.range * 1.5f;
                    targetObject.AddComponent<CircleCollider2D>().radius = 0.02f;
                    var target = targetObject.AddComponent<EffectSizeDamageProbe>();
                    Physics2D.SyncTransforms();
                    stats.projectileSizeMultiplier = 1f;
                    HitArea(weapon, root.transform.position);
                    Equal(0f, target.Damage, name + " 기본 범위 밖 제외");
                    stats.projectileSizeMultiplier = 2f;
                    HitArea(weapon, root.transform.position);
                    Equal(data.damage, target.Damage, name + " 확대 범위 실제 피해");
                    stats.projectileSizeMultiplier = 1f;
                    HitArea(weapon, root.transform.position);
                    Equal(data.damage, target.Damage, name + " 배율 해제 후 판정 복원");
                    results.Add("PASS " + name + " 기본 범위 밖 제외 → 2배에서 타격 → 해제 후 제외");
                    Object.DestroyImmediate(weapon.gameObject);
                    Object.DestroyImmediate(targetObject);
                }

                foreach (string name in new[] { "LaserEmitter", "LayLaser" })
                {
                    WeaponData data = Load<WeaponData>(name);
                    var weapon = Object.Instantiate(data.weaponPrefab, root.transform);
                    typeof(WeaponBase).GetField("playerManager", Private).SetValue(weapon, player);
                    weapon.Initialize(data);
                    var beam = Object.Instantiate(data.projectilePrefab);
                    temporary.Add(beam);
                    stats.projectileSizeMultiplier = 2f;
                    if (weapon is LaserWeapon laser)
                    {
                        var effect = beam.GetComponent<WeaponLaserBeam>();
                        effect.Initialize(laser, laser.firePoint, Vector2.right, (LaserWeaponData)data, 0f, null);
                        Equal(((LaserWeaponData)data).beamWidth * 2f,
                            (float)typeof(WeaponLaserBeam).GetField("width", Private).GetValue(effect), name + " 판정 폭");
                        Equal(((LaserWeaponData)data).visualBeamWidth * 2f,
                            (float)typeof(WeaponLaserBeam).GetField("visualWidth", Private).GetValue(effect), name + " 시각 폭");
                    }
                    else if (weapon is LayLaserWeapon lay)
                    {
                        var effect = beam.GetComponent<LayLaserBeam>();
                        effect.Initialize(lay, lay.firePoint, Vector2.right, (LayLaserWeaponData)data, 0, 0f, null);
                        Equal(((LayLaserWeaponData)data).GetWidth(0) * 2f, effect.CurrentWidth, name + " 판정 폭");
                    }
                    else throw new InvalidOperationException(name + " 무기 타입");
                    results.Add("PASS " + name + " 광선 폭 2배");
                    Object.DestroyImmediate(weapon.gameObject);
                }

                WeaponData projectileData = Load<WeaponData>("ForestThorn");
                var ranged = (RangedWeapon)Object.Instantiate(projectileData.weaponPrefab, root.transform);
                typeof(WeaponBase).GetField("playerManager", Private).SetValue(ranged, player);
                ranged.Initialize(projectileData);
                foreach (float size in new[] { 2f, 2f, 1f })
                {
                    stats.projectileSizeMultiplier = size;
                    GameObject projectile = ranged.SpawnProj(Vector2.right);
                    if (projectile == null) throw new InvalidOperationException("투사체 풀 생성 실패");
                    Equal(projectileData.projectilePrefab.transform.localScale.x * size,
                        projectile.transform.localScale.x, "풀 재사용 배율");
                    Equal(size, projectile.GetComponent<CollisionObject>().effectSizeMultiplier, "분열/폭발 배율 전달");
                    ObjectPoolManager.Instance.ReturnToPool(projectileData.projectilePrefab.name, projectile);
                }
                results.Add("PASS 일반 투사체 2배 → 재사용 2배 → 해제 1배, 크기 누적 없음");

                var voidData = Load<VoidRayWeaponData>("VoidRay");
                var voidWeapon = (VoidRayWeapon)Object.Instantiate(voidData.weaponPrefab, root.transform);
                typeof(WeaponBase).GetField("playerManager", Private).SetValue(voidWeapon, player);
                voidWeapon.Initialize(voidData);
                var voidObject = Object.Instantiate(voidData.projectilePrefab);
                temporary.Add(voidObject);
                var voidBeam = voidObject.GetComponent<VoidRayBeam>();
                stats.projectileSizeMultiplier = 1f;
                voidBeam.Initialize(voidWeapon, voidWeapon.firePoint, voidData, null);
                LineRenderer core = voidObject.transform.Find("CoreLine").GetComponent<LineRenderer>();
                float baseWidth = core.widthMultiplier;
                stats.projectileSizeMultiplier = 2f;
                typeof(VoidRayBeam).GetMethod("UpdateGeometry", Private).Invoke(voidBeam, null);
                typeof(VoidRayBeam).GetMethod("UpdateVisual", Private).Invoke(voidBeam, null);
                Equal(baseWidth * 2f, core.widthMultiplier, "VoidRay 지속 광선 시각 폭");
                stats.projectileSizeMultiplier = 1f;
                typeof(VoidRayBeam).GetMethod("UpdateGeometry", Private).Invoke(voidBeam, null);
                typeof(VoidRayBeam).GetMethod("UpdateVisual", Private).Invoke(voidBeam, null);
                Equal(baseWidth, core.widthMultiplier, "VoidRay 해제 후 폭 복원");
                results.Add("PASS VoidRay 지속 광선 2배 및 해제 후 복원");

                var sideObject = new GameObject("[검증] 광선 중심선 밖의 적");
                temporary.Add(sideObject);
                sideObject.layer = LayerMask.NameToLayer("Enemy");
                sideObject.transform.position = voidWeapon.firePoint.position + Vector3.right * 1f +
                    Vector3.up * (0.01f + voidData.AirCoreWidth * 0.25f);
                sideObject.AddComponent<CircleCollider2D>().radius = 0.01f;
                sideObject.AddComponent<EffectSizeDamageProbe>();
                Physics2D.SyncTransforms();
                typeof(VoidRayBeam).GetMethod("UpdateGeometry", Private).Invoke(voidBeam, null);
                if (voidBeam.HasTargetHit) throw new InvalidOperationException("VoidRay 기본 중심선 밖 대상이 연결됨");
                stats.projectileSizeMultiplier = 2f;
                typeof(VoidRayBeam).GetMethod("UpdateGeometry", Private).Invoke(voidBeam, null);
                if (!voidBeam.HasTargetHit) throw new InvalidOperationException("VoidRay 확대 판정이 옆의 적을 놓침");
                results.Add("PASS VoidRay 중심선 밖의 적은 크기 2배에서만 연결");

                return string.Join("\n", results);
            }
            finally
            {
                foreach (Object item in temporary) if (item != null) Object.DestroyImmediate(item);
            }
        }

        private static T Load<T>(string name) where T : WeaponData =>
            AssetDatabase.LoadAssetAtPath<T>("Assets/Nytherion/Data/ScriptableObjects/Weapons/" + name + ".asset");

        private static void HitArea(WeaponBase weapon, Vector3 center)
        {
            if (weapon is BlazeshadeWeapon)
                typeof(BlazeshadeWeapon).GetMethod("DealAuraDamage", Private).Invoke(weapon, null);
            else
                typeof(GuardianStaffWeapon).GetMethod("DealDamage", Private).Invoke(weapon,
                    new object[] { (Vector2)center, weapon.weaponData.range * weapon.EffectSizeMultiplier });
        }

        private static void Equal(float expected, float actual, string label)
        {
            if (!Mathf.Approximately(expected, actual))
                throw new InvalidOperationException(label + ": 기대 " + expected + ", 실제 " + actual);
        }
    }

    public sealed class EffectSizeDamageProbe : MonoBehaviour, IDamageable
    {
        public float Damage { get; private set; }
        public void TakeDamage(float damageAmount, bool isChain = false) => Damage += damageAmount;
    }
}
