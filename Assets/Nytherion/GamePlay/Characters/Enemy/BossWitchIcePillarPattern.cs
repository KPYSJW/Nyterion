using System.Collections.Generic;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Dungeon;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAIController), typeof(BossWitchMovementBehavior))]
    public sealed class BossWitchIcePillarPattern : MonoBehaviour, IBossWitchPattern
    {
        [SerializeField] private BossWitchIcePillarHazard pillarPrefab;
        [Header("Ground Propagation")]
        [SerializeField, Min(1)] private int branchCount = 3;
        [SerializeField, Range(0f, 160f)] private float fanAngle = 60f;
        [SerializeField, Min(0.1f)] private float startDistance = 1.8f;
        [SerializeField, Min(0.1f)] private float pillarSpacing = 1.8f;
        [SerializeField, Min(0.01f)] private float propagationInterval = 0.12f;
        [SerializeField, Min(0.05f)] private float warningDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float standingDuration = 3f;
        [SerializeField, Min(0.1f)] private float hitRadius = 0.85f;
        [SerializeField, Min(0.1f)] private float blockingRadius = 0.65f;
        [SerializeField, Min(0.1f)] private float activationRange = 10f;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private bool reverseCastingFacing;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);
        [SerializeField] private DungeonManager dungeonManager;

        private readonly List<Vector2> planned = new List<Vector2>();
        private readonly List<BossWitchIcePillarHazard> pillars = new List<BossWitchIcePillarHazard>();
        private readonly Collider2D[] walls = new Collider2D[64];
        private readonly RaycastHit2D[] pathHits = new RaycastHit2D[64];
        private EnemyBase enemyBody;
        private BossWitchMovementBehavior movement;
        private StatusEffectManager statusEffects;
        private bool casting;
        private static readonly int CastState = Animator.StringToHash("Base Layer.IcePillarCast");
        public BossWitchPatternType PatternType => BossWitchPatternType.IcePillar;

        private void CacheComponents()
        {
            if (enemyBody == null) enemyBody = GetComponent<EnemyBase>();
            if (movement == null) movement = GetComponent<BossWitchMovementBehavior>();
            if (statusEffects == null) statusEffects = GetComponent<StatusEffectManager>();
            if (dungeonManager == null) dungeonManager = FindObjectOfType<DungeonManager>();
        }

        private RoomFirstDungeonGenerator.Room OwnerRoom() =>
            enemyBody.homeRoom ?? dungeonManager.FindRoomAt(Vector2Int.FloorToInt(transform.position));

        public bool CanStart(EnemyAIController enemy)
        {
            CacheComponents();
            return isActiveAndEnabled && !casting && pillarPrefab != null && dungeonManager != null &&
                enemyBody != null && !enemyBody.isDead && OwnerRoom() != null && enemy.CurrentTarget != null &&
                enemy.animator != null && enemy.animator.isActiveAndEnabled && enemy.animator.layerCount > 0 &&
                enemy.animator.HasState(0, CastState) &&
                Vector2.Distance(transform.position, enemy.CurrentTarget.position) <= activationRange;
        }

        public void Begin(EnemyAIController enemy)
        {
            CacheComponents();
            pillars.RemoveAll(pillar => pillar == null);
            casting = true;
            movement.SetCastingFacingReversed(reverseCastingFacing);
            Vector2 forward = enemy.CurrentTarget.position - transform.position;
            if (forward.sqrMagnitude < 0.0001f) forward = movement.FacingRight ? Vector2.right : Vector2.left;
            forward.Normalize();
            FaceTarget(enemy);
            enemy.PlayAnimation("IcePillarCast");
            RoomFirstDungeonGenerator.Room room = OwnerRoom();
            float damage = (enemy.enemyData != null ? enemy.enemyData.damageAmount : 2f) * damageMultiplier;
            if (statusEffects != null) damage *= statusEffects.GetOutgoingDamageMultiplier();
            SpawnPillars(enemyBody, transform.position, forward, room, dungeonManager, damage, pillars);
        }

        internal bool HasEchoEffect => pillarPrefab != null;

        internal void SpawnPillars(EnemyBase source, Vector3 origin, Vector2 forward, RoomFirstDungeonGenerator.Room room,
            DungeonManager dungeon, float damage, List<BossWitchIcePillarHazard> destination, int sortingOrderOffset = 1)
        {
            planned.Clear();
            float maxDistance = RoomTravelLimit(origin, room, dungeon);
            float centerAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            int count = Mathf.Max(1, branchCount);
            for (int i = 0; i < count; i++)
            {
                float offset = count == 1 ? 0f : -fanAngle * 0.5f + fanAngle * i / (count - 1);
                float angle = (centerAngle + offset) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                TraceBranch(source, origin, direction, maxDistance, room, dungeon, damage, destination, sortingOrderOffset);
            }
            if (planned.Count == 0)
                Debug.LogWarning("[WitchBoss] 얼음 전파 경로가 모두 벽 또는 방 경계에 막혀 있습니다.", this);
        }

        private void TraceBranch(EnemyBase source, Vector3 origin, Vector2 direction, float maxDistance,
            RoomFirstDungeonGenerator.Room room, DungeonManager dungeon, float damage,
            List<BossWitchIcePillarHazard> destination, int sortingOrderOffset)
        {
            Vector2 previous = origin;
            float spacing = Mathf.Max(0.1f, pillarSpacing);
            int step = 0;
            // The room's actual bounds provide a finite travel limit, not a fixed pillar count.
            for (float distance = Mathf.Max(0.1f, startDistance); distance <= maxDistance; distance += spacing, step++)
            {
                Vector2 position = (Vector2)origin + direction * distance;
                if (!PathClear(previous, position, room, dungeon, step == 0) || !ValidPosition(position, room, dungeon)) break;
                previous = position;
                if (AlreadyPlanned(position)) continue;
                planned.Add(position);
                BossWitchIcePillarHazard pillar = Instantiate(pillarPrefab,
                    new Vector3(position.x, position.y, origin.z), Quaternion.identity);
                pillar.Initialize(source, warningDuration, standingDuration, hitRadius, blockingRadius,
                    damage, step * Mathf.Max(0.01f, propagationInterval), sortingOrderOffset);
                destination.Add(pillar);
            }
        }

        private float RoomTravelLimit(Vector2 origin, RoomFirstDungeonGenerator.Room room, DungeonManager dungeon)
        {
            BoundsInt bounds = room.Bounds;
            GameObject prefab = dungeon.CurrentDungeonData != null ? dungeon.CurrentDungeonData.bossRoomPrefab : null;
            Tilemap floor = prefab != null && room.type == RoomFirstDungeonGenerator.RoomType.Boss ?
                prefab.GetComponentInChildren<Tilemap>() : null;
            if (floor != null)
            {
                bounds = floor.cellBounds;
                bounds.position += Vector3Int.RoundToInt(room.center);
            }
            float limit = 0f;
            foreach (Vector2 corner in new[] { new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMin, bounds.yMax),
                new Vector2(bounds.xMax, bounds.yMin), new Vector2(bounds.xMax, bounds.yMax) })
                limit = Mathf.Max(limit, Vector2.Distance(origin, corner));
            return limit + Mathf.Max(0.1f, pillarSpacing);
        }

        private bool AlreadyPlanned(Vector2 position)
        {
            float minimum = Mathf.Max(0.1f, Mathf.Min(pillarSpacing * 0.35f, blockingRadius));
            foreach (Vector2 other in planned)
                if (Vector2.Distance(position, other) < minimum) return true;
            return false;
        }

        private bool FloorClear(Vector2 position, RoomFirstDungeonGenerator.Room room, DungeonManager dungeon, float radius)
        {
            if (dungeon.FindRoomAt(Vector2Int.FloorToInt(position)) != room) return false;
            for (int x = Mathf.FloorToInt(position.x - radius); x <= Mathf.FloorToInt(position.x + radius); x++)
                for (int y = Mathf.FloorToInt(position.y - radius); y <= Mathf.FloorToInt(position.y + radius); y++)
                {
                    Vector2 nearest = new Vector2(Mathf.Clamp(position.x, x, x + 1), Mathf.Clamp(position.y, y, y + 1));
                    if ((nearest - position).sqrMagnitude < radius * radius &&
                        dungeon.FindRoomAt(new Vector2Int(x, y)) != room) return false;
                }
            return true;
        }

        private bool IsWall(Collider2D collider) => collider != null &&
            (collider.CompareTag(Tags.Wall) || (wallLayers.value & (1 << collider.gameObject.layer)) != 0);

        private bool PathClear(Vector2 from, Vector2 to, RoomFirstDungeonGenerator.Room room, DungeonManager dungeon, bool firstSegment)
        {
            float distance = Vector2.Distance(from, to);
            int samples = Mathf.Max(1, Mathf.CeilToInt(distance / 0.25f));
            float radius = firstSegment ? 0f : Mathf.Max(hitRadius, blockingRadius) + 0.1f;
            for (int i = 1; i <= samples; i++)
                if (!FloorClear(Vector2.Lerp(from, to, i / (float)samples), room, dungeon, radius)) return false;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = true;
            Vector2 direction = (to - from).normalized;
            int count = firstSegment ? Physics2D.Raycast(from, direction, filter, pathHits, distance) :
                Physics2D.CircleCast(from, radius, direction, filter, pathHits, distance);
            for (int i = 0; i < count; i++)
                if (IsWall(pathHits[i].collider)) return false;
            return true;
        }

        private bool ValidPosition(Vector2 position, RoomFirstDungeonGenerator.Room room, DungeonManager dungeon)
        {
            float radius = Mathf.Max(hitRadius, blockingRadius) + 0.1f;
            if (!FloorClear(position, room, dungeon, radius)) return false;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = true;
            int count = Physics2D.OverlapCircle(position, radius, filter, walls);
            for (int i = 0; i < count; i++)
                if (IsWall(walls[i])) return false;
            return true;
        }

        public bool UpdatePattern(EnemyAIController enemy)
        {
            if (!casting) return true;
            if (enemy.animator == null || !enemy.animator.isActiveAndEnabled || enemy.animator.layerCount == 0) return false;
            AnimatorStateInfo state = enemy.animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == CastState && state.normalizedTime >= 1f && !enemy.animator.IsInTransition(0);
        }

        private void FaceTarget(EnemyAIController enemy)
        {
            if (enemy.CurrentTarget == null) return;
            float horizontal = enemy.CurrentTarget.position.x - transform.position.x;
            if (Mathf.Abs(horizontal) <= 0.01f) return;
            Vector2 facing = horizontal > 0f ? Vector2.right : Vector2.left;
            enemy.SetForcedFacingDirection(facing);
            movement.UpdateFacing(facing);
        }

        public void End(EnemyAIController enemy, bool completed)
        {
            if (!completed) ClearPillars();
            casting = false;
            movement.SetCastingFacingReversed(false);
            enemy.ClearForcedFacingDirection();
        }

        private void ClearPillars()
        {
            foreach (BossWitchIcePillarHazard pillar in pillars)
                if (pillar != null) pillar.Cancel();
            pillars.Clear();
        }

        public void ResetForReuse()
        {
            CacheComponents();
            ClearPillars();
            planned.Clear();
            casting = false;
        }

        private void OnDisable()
        {
            ClearPillars();
            casting = false;
        }
    }
}
