using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public class SplitModifier : MonoBehaviour, IProjModifier
    {
        public int splitCount = 3;
        public float splitAngle = 60f;
        public float splitSpeedMultiplier = 1f;
        public float splitDamageMultiplier = 0.5f;
        public string splitProjectileTag = "";

        private void Start() { }
        public bool OnHit(Collider2D target)
        {
            CollisionObject myCol = GetComponent<CollisionObject>();
            string targetPoolTag = string.IsNullOrEmpty(splitProjectileTag) && myCol != null ? myCol.poolTag : splitProjectileTag;

            Rigidbody2D myRb = GetComponent<Rigidbody2D>();
            Vector2 currentDir = transform.right;
            float currentSpeed = 8f;

            if (myRb != null && myRb.velocity.sqrMagnitude > 0.1f)
            {
                currentDir = myRb.velocity.normalized;
                currentSpeed = myRb.velocity.magnitude;
            }

            float baseAngle = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;
            float startAngle = baseAngle - (splitAngle / 2f);
            float angleStep = splitCount > 1 ? splitAngle / (splitCount - 1) : 0;

            for (int i = 0; i < splitCount; i++)
            {
                float currentA = startAngle + (angleStep * i);
                Vector2 spreadDirection = new Vector2(
                    Mathf.Cos(currentA * Mathf.Deg2Rad),
                    Mathf.Sin(currentA * Mathf.Deg2Rad)
                );

                GameObject fragment = ObjectPoolManager.Instance.SpawnFromPool(targetPoolTag, transform.position, Quaternion.identity);

                if (fragment != null)
                {
                    fragment.transform.rotation = Quaternion.AngleAxis(currentA, Vector3.forward);

                    if (fragment.TryGetComponent<Rigidbody2D>(out var fragRb))
                    {
                        fragRb.velocity = spreadDirection * (currentSpeed * splitSpeedMultiplier);
                    }

                    if (fragment.TryGetComponent<CollisionObject>(out var fragCol))
                    {
                        if (myCol != null) fragCol.damage = myCol.damage * splitDamageMultiplier;

                        fragCol.DisableAllProjModifiers();
                    }
                }
            }

            return false;
        }
    }
}
