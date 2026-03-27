using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    public abstract class RangedWeapon : WeaponBase
    {
        [Header("Ranged Settings")]
        [Tooltip("발사체가 생성될 위치를 지정하는 Transform입니다." +
                 "캐릭터의 손이나 무기 끝부분에 위치시켜야 합니다.")]
        public Transform firePoint;
        
        private const float DefaultProjectileSpeed = 8f;

       public void Projectile(Vector2 direction)
        {
            GameObject projectile = Instantiate(weaponData.projectilePrefab, firePoint.position, Quaternion.identity);
            if (projectile.TryGetComponent<Rigidbody2D>(out var rb))
            {
                Vector2 normalizedDir = direction.normalized;
                rb.velocity = direction.normalized * DefaultProjectileSpeed;

                float angle = Mathf.Atan2(normalizedDir.y, normalizedDir.x) * Mathf.Rad2Deg;
                projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            }
        }
    }
}