using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BossGameMasterShieldPattern : MonoBehaviour, IBossGameMasterPattern
    {
        [Header("Shield Counter / 보호막 반격")]
        [SerializeField, Min(0.1f)] private float shieldDuration = 6f;
        [SerializeField, Range(0f, 1f)] private float damageReduction = 0.5f;
        [SerializeField, Min(0.01f)] private float damagePerProjectile = 1f;
        [SerializeField, Range(0f, 180f)] private float spreadAngle = 60f;
        [SerializeField] private Vector2 centerOffset = new Vector2(0f, 1.8f);
        [SerializeField, Min(0.1f)] private float shieldScale = 2.5f;
        [Tooltip("이미지의 투명 여백을 제외한 원형 피격 반경 비율입니다.")]
        [SerializeField, Range(0.1f, 1f)] private float shieldHitRadiusRatio = 0.92f;
        [SerializeField, Min(1f)] private float shieldFramesPerSecond = 24f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 7f;
        [SerializeField, Min(0.1f)] private float projectileScale = 1.5f;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.12f;
        [SerializeField, Min(0f)] private float projectileDamage = 1f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 7f;
        [SerializeField, Min(1f)] private float projectileFramesPerSecond = 16f;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);
        [Header("Visuals / 이미지")]
        [SerializeField] private Sprite[] shieldFrames, projectileFrames;
        [SerializeField] private Material effectMaterial;
        private BossGameMasterEnemy owner;
        private EnemyAIController ai;
        private SpriteRenderer shield;
        private GameObject root;
        private CapsuleCollider2D hitBody, movementBlocker;
        private Vector2 savedBodySize, savedBodyOffset;
        private CapsuleDirection2D savedBodyDirection;
        private bool bodySwitched, savedBodyTrigger;
        private int projectileSortingOrder;
        private readonly List<BossGameMasterHomingProjectile> projectiles = new List<BossGameMasterHomingProjectile>();
        private float beganAt;
        private double damageRemainder, pendingShots;
        public BossGameMasterPatternType PatternType => BossGameMasterPatternType.ShieldCounter;
        public bool HasStarted => root != null;
        public bool IsShieldActive => root != null && isActiveAndEnabled && owner != null && owner.isActiveAndEnabled &&
            !owner.isDead && !owner.IsUpgrading && Time.time - beganAt < Mathf.Max(0.1f, shieldDuration);

        public bool CanStart()
        {
            if (owner == null) owner = GetComponent<BossGameMasterEnemy>();
            if (ai == null) ai = GetComponent<EnemyAIController>();
            if (!isActiveAndEnabled || owner == null || owner.isDead || owner.IsUpgrading || ai == null ||
                ai.CurrentTarget == null || effectMaterial == null || shieldFrames == null || shieldFrames.Length == 0 ||
                projectileFrames == null || projectileFrames.Length == 0) return false;
            foreach (Sprite frame in shieldFrames) if (frame == null) return false;
            foreach (Sprite frame in projectileFrames) if (frame == null) return false;
            return true;
        }

        public void Begin()
        {
            Clear();
            if (!CanStart()) return;
            beganAt = Time.time;
            root = new GameObject("GameMaster Shield Counter");
            GameObject bubble = new GameObject("Shield");
            bubble.transform.SetParent(root.transform, false);
            shield = bubble.AddComponent<SpriteRenderer>();
            shield.sharedMaterial = effectMaterial;
            shield.sprite = shieldFrames[0];
            SpriteRenderer body = GetComponent<SpriteRenderer>();
            shield.sortingLayerID = body != null ? body.sortingLayerID : 0;
            shield.sortingOrder = body != null ? body.sortingOrder - 1 : 19;
            projectileSortingOrder = body != null ? body.sortingOrder + 6 : 26;
            SwitchToShieldHitBody();
            UpdateShield();
        }

        private void SwitchToShieldHitBody()
        {
            hitBody = GetComponent<CapsuleCollider2D>();
            if (hitBody == null || !hitBody.enabled) return;
            savedBodySize = hitBody.size;
            savedBodyOffset = hitBody.offset;
            savedBodyDirection = hitBody.direction;
            savedBodyTrigger = hitBody.isTrigger;
            bodySwitched = true;
            // A separate physical body has no damageable ancestor; only the root shield receives attacks.
            GameObject blocker = new GameObject("GameMaster Movement Body");
            blocker.transform.SetParent(root.transform, false);
            blocker.layer = LayerMask.NameToLayer("PlayerOnlyObstacle");
            Rigidbody2D rigidbody = blocker.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
            rigidbody.gravityScale = 0f;
            movementBlocker = blocker.AddComponent<CapsuleCollider2D>();
            movementBlocker.size = savedBodySize;
            movementBlocker.offset = savedBodyOffset;
            movementBlocker.direction = savedBodyDirection;
            movementBlocker.isTrigger = savedBodyTrigger;
            movementBlocker.sharedMaterial = hitBody.sharedMaterial;
            hitBody.isTrigger = true;
        }

        private void UpdateHitBody()
        {
            if (!bodySwitched) return;
            if (!IsShieldActive) { RestoreHitBody(); return; }
            Vector3 scale = transform.lossyScale;
            float diameter = Mathf.Min(shield.sprite.bounds.size.x, shield.sprite.bounds.size.y) *
                Mathf.Max(0.1f, shieldScale) * Mathf.Clamp(shieldHitRadiusRatio, 0.1f, 1f);
            hitBody.size = new Vector2(diameter / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                diameter / Mathf.Max(0.001f, Mathf.Abs(scale.y)));
            hitBody.offset = centerOffset;
            if (movementBlocker != null)
            {
                movementBlocker.transform.SetPositionAndRotation(transform.position, transform.rotation);
                movementBlocker.transform.localScale = scale;
            }
        }

        private void RestoreHitBody()
        {
            if (!bodySwitched) return;
            if (hitBody != null)
            {
                hitBody.size = savedBodySize;
                hitBody.offset = savedBodyOffset;
                hitBody.direction = savedBodyDirection;
                hitBody.isTrigger = savedBodyTrigger;
            }
            if (movementBlocker != null)
            {
                movementBlocker.gameObject.SetActive(false);
                Destroy(movementBlocker.gameObject);
            }
            movementBlocker = null;
            bodySwitched = false;
        }

        // Called after critical/status multipliers, before the shared damage event and health subtraction.
        public float ReceiveDamage(float amount)
        {
            if (!IsShieldActive || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return amount;
            damageRemainder += amount;
            double threshold = Mathf.Max(0.01f, damagePerProjectile);
            double count = System.Math.Floor((damageRemainder + threshold * 0.000001) / threshold);
            if (count > 0)
            {
                pendingShots += count;
                damageRemainder = System.Math.Max(0d, damageRemainder - count * threshold);
            }
            return amount * (1f - Mathf.Clamp01(damageReduction));
        }

        private void UpdateShield()
        {
            if (shield == null) return;
            shield.enabled = IsShieldActive;
            shield.transform.position = transform.TransformPoint(centerOffset);
            shield.transform.localScale = Vector3.one * Mathf.Max(0.1f, shieldScale);
            shield.sprite = shieldFrames[Mathf.Max(0, Mathf.FloorToInt((Time.time - beganAt) * Mathf.Max(1f, shieldFramesPerSecond))) % shieldFrames.Length];
            UpdateHitBody();
        }

        private void FireCounter()
        {
            Vector3 origin = transform.TransformPoint(centerOffset);
            Vector2 aim = ai.CurrentTarget.position - origin;
            float angle = (Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + Random.Range(-spreadAngle * 0.5f, spreadAngle * 0.5f)) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject node = new GameObject("Shield Counter Projectile");
            node.layer = 2;
            node.transform.SetParent(root.transform, false);
            node.transform.position = origin;
            BossGameMasterHomingProjectile shot = node.AddComponent<BossGameMasterHomingProjectile>();
            shot.Initialize(owner, ai, projectileSpeed, 0f, 0f, projectileLifetime, projectileScale, hitRadius, projectileDamage,
                projectileFramesPerSecond, 0f, wallLayers, projectileFrames, effectMaterial, shield.sortingLayerID, projectileSortingOrder);
            shot.FireInDirection(direction);
            projectiles.Add(shot);
        }

        public bool Tick()
        {
            if (root == null || owner == null || !owner.isActiveAndEnabled || owner.isDead || owner.IsUpgrading) { Clear(); return true; }
            UpdateShield();
            // Spread only exceptionally large hit bursts across frames; never discard earned counter shots.
            if (ai != null && ai.CurrentTarget != null)
                for (int i = 0; i < 16 && pendingShots >= 1d; i++, pendingShots--) FireCounter();
            projectiles.RemoveAll(shot => shot == null || shot.IsFinished);
            if (IsShieldActive || pendingShots >= 1d || projectiles.Count > 0) return false;
            Clear();
            return true;
        }

        private void LateUpdate()
        {
            if (root != null && owner != null) UpdateShield();
        }

        private void FixedUpdate()
        {
            if (root != null && owner != null) UpdateShield();
        }

        public void Clear()
        {
            RestoreHitBody();
            foreach (BossGameMasterHomingProjectile shot in projectiles) if (shot != null) shot.Cancel();
            projectiles.Clear();
            if (root != null) { root.SetActive(false); Destroy(root); }
            root = null; shield = null;
            damageRemainder = pendingShots = 0d;
        }

        private void OnDisable() => Clear();
    }
}
