using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Combat;
using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>플레이어를 따라다니며 벼락의 착탄 범위에 피해와 감전을 적용합니다.</summary>
    // 기존 프리팹의 스크립트 참조를 보존하며 소환물 전용으로 사용합니다.
    public sealed class DarkCloudSkill : MonoBehaviour
    {
        private readonly List<Collider2D> hits = new List<Collider2D>();
        private readonly HashSet<EnemyBase> uniqueEnemies = new HashSet<EnemyBase>();
        // 통합 시트 각 착탄 프레임의 보이는 바닥까지 남은 투명 픽셀입니다.
        private static readonly int[] ImpactBottomPadding = { 21, 18, 9, 11, 7, 6, 5, 4, 3 };
        private static readonly int[] BoltBottomPadding = { 6, 5, 5, 5 };
        private static readonly float[] BoltTopOffset = { 0f, -0.5f, -1.5f, 0f };
        private const int FirstBoltFrame = 4;
        private readonly SpriteBodyBounds bodyBounds = new SpriteBodyBounds();
        private SpriteRenderer cloud;
        private SpriteRenderer launchCloud;
        private Visual lightning;
        private Visual impact;
        private DarkCloudSkillData data;
        private PlayerManager owner;
        private Transform caster;
        private int relicLevel;
        private Vector3 groundPosition;
        private Vector3 strikePosition;
        private Vector3 strikeDamagePosition;
        private float nextAttackAt, idleTime, castDamage, castSize;
        private EnemyBase pendingTarget;
        private Sprite pendingSprite;
        private float pixelWaitStartedAt;
        private bool summoned;
        private bool impactApplied;

        private sealed class Visual
        {
            public SpriteRenderer renderer;
            public float elapsed;
            public int startFrame;
        }

        public void Initialize(PlayerManager player, DarkCloudSkillData summonData, int level)
        {
            owner = player;
            caster = player != null ? player.transform : null;
            data = summonData;
            relicLevel = Mathf.Max(1, level);
            if (caster != null && data != null && data.HasValidFrames) BeginSummon();
        }

        private void BeginSummon()
        {
            castDamage = data.GetDamage(relicLevel);
            castSize = owner.currentPlayerData != null
                ? Mathf.Max(0.01f, owner.currentPlayerData.projectileSizeMultiplier) : 1f;
            groundPosition = caster.position;
            nextAttackAt = Time.time;
            idleTime = 0f;
            summoned = true;
            pendingTarget = null;
            pendingSprite = null;
            if (cloud == null) cloud = CreateRenderer("DarkCloud");
            if (lightning != null) lightning.renderer.gameObject.SetActive(false);
            if (launchCloud != null) launchCloud.gameObject.SetActive(false);
            if (impact != null) impact.renderer.gameObject.SetActive(false);
            cloud.sprite = data.idleFrames[0];
            cloud.transform.localScale = Vector3.one * (Mathf.Max(0.01f, data.cloudVisualScale) * castSize);
            UpdateCloudPosition();
            cloud.gameObject.SetActive(true);
        }

        private SpriteRenderer CreateRenderer(string objectName)
        {
            GameObject instance = new GameObject(objectName);
            instance.transform.SetParent(transform, false);
            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 20;
            return renderer;
        }

        private void Update()
        {
            if (caster == null || owner == null || !owner.isActiveAndEnabled || data == null || !data.HasValidFrames)
            {
                StopSummon();
                return;
            }
            if (!summoned) BeginSummon();

            bool lightningActive = launchCloud != null && launchCloud.gameObject.activeSelf;
            bool impactActive = impact != null && impact.renderer.gameObject.activeSelf;
            idleTime += Time.deltaTime;
            cloud.sprite = data.idleFrames[Mathf.FloorToInt(idleTime * Mathf.Max(0.01f, data.idleFps)) % data.idleFrames.Length];
            FollowCaster();
            UpdateCloudPosition();
            if (impactActive) AdvanceImpact();
            if (lightningActive)
            {
                if (lightning.startFrame != Time.frameCount) lightning.elapsed += Time.deltaTime;
                int frame = Mathf.FloorToInt(lightning.elapsed * Mathf.Max(0.01f, data.launchFps));
                if (!impactApplied && frame >= FirstBoltFrame)
                {
                    impactApplied = true;
                    BeginImpact();
                }
                if (frame >= data.launchCloudFrames.Length)
                {
                    lightning.renderer.gameObject.SetActive(false);
                    launchCloud.gameObject.SetActive(false);
                }
                else
                {
                    launchCloud.sprite = data.launchCloudFrames[frame];
                    UpdateLaunchCloudPosition();
                    int boltFrame = frame - FirstBoltFrame;
                    bool showBolt = boltFrame >= 0 && boltFrame < data.boltFrames.Length;
                    if (showBolt)
                    {
                        lightning.renderer.sprite = data.boltFrames[boltFrame];
                        UpdateLightningPosition(boltFrame);
                    }
                    lightning.renderer.gameObject.SetActive(showBolt);
                }
                return;
            }
            if (impactActive) return;

            if (pendingTarget != null && (!pendingTarget.isActiveAndEnabled || pendingTarget.isDead))
            {
                pendingTarget = null;
                pendingSprite = null;
            }
            EnemyBase target = pendingTarget != null ? pendingTarget : FindTarget();
            if (target != null && Time.time >= nextAttackAt)
            {
                Vector3 destination = target.GetFootPosition();
                destination.z = caster.position.z;
                SpriteRenderer body = target.BodyRenderer;
                if (pendingTarget == null)
                {
                    pendingTarget = target;
                    pendingSprite = body != null ? body.sprite : null;
                    pixelWaitStartedAt = Time.time;
                }

                bool pixelsPending = false;
                if (body != null && body.sprite != null)
                {
                    Bounds visibleBounds = bodyBounds.GetBounds(body);
                    Sprite anchorSprite = pendingSprite != null ? pendingSprite : body.sprite;
                    destination.x = visibleBounds.center.x;
                    // 적 스프라이트의 투명 여백을 제외한 하단에 착탄합니다.
                    destination.y = CurseSpritePixels.GetWorldBottom(body, anchorSprite,
                        visibleBounds.min.y, out pixelsPending);
                }

                // 첫 픽셀 읽기가 끝난 뒤 위치를 고정하며, 읽기가 지연돼도 공격은 계속합니다.
                if (!pixelsPending || Time.time - pixelWaitStartedAt >= 0.5f)
                {
                    strikePosition = destination;
                    strikeDamagePosition = target.TryGetGroundHitBounds(out Bounds bounds)
                        ? bounds.center : target.transform.position;
                    pendingTarget = null;
                    pendingSprite = null;
                    nextAttackAt = Time.time + Mathf.Max(0.01f, data.attackInterval);
                    StartLightning();
                }
            }
        }

        private void FollowCaster()
        {
            Vector3 destination = caster.position;
            destination.z = groundPosition.z;
            float distance = Vector2.Distance(groundPosition, destination);
            float maximumSpeed = owner != null ? owner.BaseMoveSpeed : Mathf.Max(0f, data.moveSpeed);
            float speed = maximumSpeed * Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(distance / 5f));
            groundPosition = Vector3.MoveTowards(groundPosition, destination, speed * Time.deltaTime);
        }

        private EnemyBase FindTarget()
        {
            hits.Clear();
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(data.targetLayers);
            Physics2D.OverlapCircle(caster.position, Mathf.Max(0.01f, data.detectionRadius), filter, hits);
            EnemyBase nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Collider2D hit in hits)
            {
                EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.isDead) continue;
                Vector3 position = enemy.TryGetGroundHitBounds(out Bounds bounds) ? bounds.center : enemy.transform.position;
                float distance = ((Vector2)(position - groundPosition)).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = enemy;
                nearestDistance = distance;
            }
            return nearest;
        }

        private void UpdateCloudPosition()
        {
            // 구름 시트의 보이는 바닥을 기존 번개 시작 높이에 고정합니다.
            Bounds bounds = cloud.sprite.bounds;
            float cloudBottom = bounds.min.y + bounds.size.y * (36f / 96f);
            float scaleOffset = cloudBottom * (0.625f - Mathf.Max(0.01f, data.cloudVisualScale)) * castSize;
            cloud.transform.position = groundPosition + Vector3.up * (Mathf.Max(0.01f, data.cloudHeight) + scaleOffset);
        }

        private void StartLightning()
        {
            if (lightning == null) lightning = new Visual { renderer = CreateRenderer("DarkCloudLightning") };
            if (launchCloud == null) launchCloud = CreateRenderer("DarkCloudLaunchCloud");
            SpriteRenderer renderer = lightning.renderer;
            renderer.sprite = data.boltFrames[0];
            renderer.flipX = Random.value < 0.5f;
            renderer.sortingOrder = cloud.sortingOrder - 1;
            launchCloud.sprite = data.launchCloudFrames[0];
            launchCloud.sortingOrder = cloud.sortingOrder - 1;
            lightning.elapsed = 0f;
            lightning.startFrame = Time.frameCount;
            impactApplied = false;
            UpdateLaunchCloudPosition();
            renderer.gameObject.SetActive(false);
            launchCloud.gameObject.SetActive(true);
        }

        private Vector3 GetCloudBottom()
        {
            Bounds bounds = cloud.sprite.bounds;
            float bottom = bounds.min.y + bounds.size.y * (36f / 96f);
            return cloud.transform.position + Vector3.up * (bottom * cloud.transform.localScale.y);
        }

        private void UpdateLaunchCloudPosition()
        {
            float scale = Mathf.Max(0.01f, data.cloudVisualScale) * castSize * 0.5f;
            launchCloud.transform.localScale = new Vector3(scale, scale, 1f);
            launchCloud.transform.position = GetLaunchOrigin() - Vector3.up * (launchCloud.sprite.bounds.min.y * scale)
                + Vector3.right * (5f / 32f * scale);
        }

        private Vector3 GetLaunchOrigin()
        {
            // 전조 시트의 밝은 부분이 구름 아래로 살짝 보이도록 투명 여백을 보정합니다.
            float scale = Mathf.Max(0.01f, data.cloudVisualScale) * castSize * 0.5f;
            return GetCloudBottom() - Vector3.up * (11f / 32f * scale);
        }

        private void UpdateLightningPosition(int frame)
        {
            Vector3 start = GetLaunchOrigin();
            // 번개 시작점이 발사 전조와 살짝 겹치도록 합니다.
            start += Vector3.up * (0.05f * castSize);
            Vector3 direction = start - strikePosition;
            direction.z = 0f;
            float length = direction.magnitude;
            Bounds boltBounds = lightning.renderer.sprite.bounds;
            // 낙뢰 시트의 바닥 아래 투명 여백과 중심보다 왼쪽에 있는 시작점을 반영합니다.
            float localBottom = boltBounds.min.y + boltBounds.size.y * (BoltBottomPadding[frame] / 112f);
            float localHeight = Mathf.Max(0.001f, boltBounds.size.y * ((112f - BoltBottomPadding[frame]) / 112f));
            float localTopX = boltBounds.size.x * (BoltTopOffset[frame] / 96f);
            if (lightning.renderer.flipX) localTopX = -localTopX;
            float scaleX = Mathf.Min(castSize, length * 0.95f / Mathf.Max(0.001f, Mathf.Abs(localTopX)));
            float offsetX = localTopX * scaleX;
            float scaleY = Mathf.Sqrt(Mathf.Max(0f, length * length - offsetX * offsetX)) / localHeight;
            Vector3 localDirection = new Vector3(offsetX, localHeight * scaleY, 0f);
            Quaternion rotation = length > 0.001f
                ? Quaternion.FromToRotation(localDirection, direction) : Quaternion.identity;
            Transform bolt = lightning.renderer.transform;
            bolt.rotation = rotation;
            bolt.localScale = new Vector3(scaleX, scaleY, 1f);
            bolt.position = strikePosition - rotation * (Vector3.up * (localBottom * scaleY));
        }

        private void UpdateImpactPosition(int frame)
        {
            SpriteRenderer renderer = impact.renderer;
            Bounds bounds = renderer.sprite.bounds;
            float radius = Mathf.Max(0.01f, data.impactRadius) * castSize;
            float scale = radius * 2f / Mathf.Max(0.01f, bounds.size.x * (184f / 192f));
            int index = Mathf.Clamp(frame, 0, ImpactBottomPadding.Length - 1);
            float bottom = bounds.min.y + bounds.size.y * (ImpactBottomPadding[index] / 208f);
            renderer.sortingOrder = cloud.sortingOrder;
            renderer.transform.rotation = Quaternion.identity;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.transform.position = strikePosition - Vector3.up * (bottom * scale);
        }

        private void BeginImpact()
        {
            ApplyImpact(strikeDamagePosition);
            if (impact == null) impact = new Visual { renderer = CreateRenderer("DarkCloudImpact") };
            impact.renderer.sprite = data.impactFrames[0];
            impact.elapsed = 0f;
            impact.startFrame = Time.frameCount;
            UpdateImpactPosition(0);
            impact.renderer.gameObject.SetActive(true);
        }

        private void AdvanceImpact()
        {
            if (impact.startFrame != Time.frameCount) impact.elapsed += Time.deltaTime;
            int frame = Mathf.FloorToInt(impact.elapsed * Mathf.Max(0.01f, data.impactFps));
            if (frame >= data.impactFrames.Length)
            {
                impact.renderer.gameObject.SetActive(false);
                return;
            }
            impact.renderer.sprite = data.impactFrames[frame];
            UpdateImpactPosition(frame);
        }

        private void ApplyImpact(Vector3 damagePosition)
        {
            float radius = Mathf.Max(0.01f, data.impactRadius) * castSize;
            hits.Clear();
            uniqueEnemies.Clear();
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(data.targetLayers);
            Physics2D.OverlapCircle(damagePosition, radius, filter, hits);
            foreach (Collider2D hit in hits)
            {
                EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.isDead || !uniqueEnemies.Add(enemy)) continue;
                enemy.TakeDamage(castDamage);
                if (!enemy.isDead && enemy.isActiveAndEnabled && enemy.TryGetComponent(out StatusEffectManager status))
                    status.ApplyEffect(new LightningEffect(Mathf.Max(0.01f, data.shockDuration)));
            }
        }

        private void StopSummon()
        {
            summoned = false;
            pendingTarget = null;
            pendingSprite = null;
            if (cloud != null) cloud.gameObject.SetActive(false);
            if (lightning != null && lightning.renderer != null) lightning.renderer.gameObject.SetActive(false);
            if (launchCloud != null) launchCloud.gameObject.SetActive(false);
            if (impact != null && impact.renderer != null) impact.renderer.gameObject.SetActive(false);
        }

        private void OnDisable() => StopSummon();

        private void OnDestroy()
        {
            if (cloud != null) Destroy(cloud.gameObject);
            if (lightning != null && lightning.renderer != null) Destroy(lightning.renderer.gameObject);
            if (launchCloud != null) Destroy(launchCloud.gameObject);
            if (impact != null && impact.renderer != null) Destroy(impact.renderer.gameObject);
        }
    }
}
