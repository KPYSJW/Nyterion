using System.Collections.Generic;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Dungeon;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BossWitchOrbitController : MonoBehaviour
    {
        [Header("Orbit / Refill")]
        [SerializeField, Min(1)] private int orbCount = 5;
        [SerializeField, Min(0.1f)] private float orbitRadius = 1.2f;
        [SerializeField] private float orbitSpeed = 90f;
        [SerializeField] private Vector2 orbitOffset = Vector2.zero;
        [SerializeField, Min(0.01f)] private float orbScale = 0.45f;
        [SerializeField, Min(1f)] private float orbitFramesPerSecond = 14f;
        [SerializeField] private Sprite[] orbitSprites;
        [Header("Extra Shots Per Attack")]
        [SerializeField, Min(1)] private int minimumShots = 1;
        [SerializeField, Min(1)] private int maximumShots = 2;
        [SerializeField] private BossWitchProjectile projectilePrefab;
        [SerializeField, Min(0f)] private float projectileDamageMultiplier = 1f;
        [Header("Wall To Wall ThunderStrike")]
        [SerializeField] private BossWitchThunderStrike strikePrefab;
        [SerializeField, Min(0.1f)] private float strikeSpacing = 1.3f;
        [SerializeField, Min(0.01f)] private float propagationInterval = 0.07f;
        [SerializeField, Min(0.05f)] private float strikeWarning = 0.05f;
        [SerializeField, Min(0.1f)] private float strikeRadius = 1.2f;
        [SerializeField, Min(0f)] private float strikeDamageMultiplier = 1f;
        [SerializeField] private DungeonManager dungeonManager;

        private readonly BossWitchOrbInventory inventory = new BossWitchOrbInventory();
        private readonly Dictionary<int, SpriteRenderer> visuals = new Dictionary<int, SpriteRenderer>();
        private readonly List<BossWitchProjectile> projectiles = new List<BossWitchProjectile>();
        private readonly List<BossWitchThunderStrike> strikes = new List<BossWitchThunderStrike>();
        private readonly List<int> removedSlots = new List<int>();
        private EnemyBase enemyBody;
        private StatusEffectManager statusEffects;
        public int RemainingOrbs => inventory.Count;

        private void CacheComponents()
        {
            if (enemyBody == null) enemyBody = GetComponent<EnemyBase>();
            if (statusEffects == null) statusEffects = GetComponent<StatusEffectManager>();
            if (dungeonManager == null) dungeonManager = FindObjectOfType<DungeonManager>();
        }

        public bool CanRecharge()
        {
            CacheComponents();
            return isActiveAndEnabled && enemyBody != null && !enemyBody.isDead &&
                projectilePrefab != null && strikePrefab != null && dungeonManager != null && OwnerRoom() != null;
        }

        private RoomFirstDungeonGenerator.Room OwnerRoom() => enemyBody.homeRoom ??
            dungeonManager.FindRoomAt(Vector2Int.FloorToInt(transform.position));

        public void Recharge()
        {
            CacheComponents();
            inventory.Refill(orbCount);
            removedSlots.Clear();
            foreach (int slot in visuals.Keys)
                if (slot >= inventory.Capacity) removedSlots.Add(slot);
            foreach (int slot in removedSlots) RemoveVisual(slot);
            foreach (int slot in inventory.Slots)
            {
                if (visuals.ContainsKey(slot)) continue;
                GameObject orb = new GameObject("ThunderTwins Orbit " + (slot + 1));
                orb.transform.SetParent(transform, false);
                orb.transform.localScale = Vector3.one * orbScale;
                SpriteRenderer renderer = orb.AddComponent<SpriteRenderer>();
                SpriteRenderer source = projectilePrefab.GetComponent<SpriteRenderer>();
                renderer.sharedMaterial = source.sharedMaterial;
                SpriteRenderer body = enemyBody.GetComponent<SpriteRenderer>();
                renderer.sortingLayerID = body != null ? body.sortingLayerID : 0;
                renderer.sortingOrder = body != null ? body.sortingOrder + 2 : 2;
                visuals.Add(slot, renderer);
            }
            UpdateOrbit();
        }

        private void Update()
        {
            if (enemyBody == null || enemyBody.isDead || !enemyBody.isActiveAndEnabled)
            {
                if (inventory.Count > 0 || projectiles.Count > 0 || strikes.Count > 0) ClearAll();
                return;
            }
            UpdateOrbit();
            projectiles.RemoveAll(projectile => projectile == null);
            strikes.RemoveAll(strike => strike == null);
        }

        private void UpdateOrbit()
        {
            if (inventory.Capacity == 0) return;
            int frame = orbitSprites != null && orbitSprites.Length > 0 ?
                Mathf.FloorToInt(Time.time * orbitFramesPerSecond) % orbitSprites.Length : -1;
            foreach (KeyValuePair<int, SpriteRenderer> entry in visuals)
            {
                if (entry.Value == null) continue;
                float angle = (Time.time * orbitSpeed + entry.Key * 360f / inventory.Capacity) * Mathf.Deg2Rad;
                entry.Value.transform.position = transform.position + (Vector3)orbitOffset +
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * orbitRadius;
                // Face along the orbit tangent so the tail trails the direction of travel.
                entry.Value.transform.rotation = Quaternion.Euler(0f, 0f,
                    angle * Mathf.Rad2Deg + (orbitSpeed < 0f ? -90f : 90f));
                entry.Value.sprite = frame >= 0 ? orbitSprites[frame] : projectilePrefab.PreviewSprite;
            }
        }

        public void FireForAttack(EnemyAIController enemy)
        {
            CacheComponents();
            if (!isActiveAndEnabled || inventory.Count == 0 || enemy.CurrentTarget == null || !CanRecharge()) return;
            UpdateOrbit();
            int low = Mathf.Max(1, Mathf.Min(minimumShots, maximumShots));
            int high = Mathf.Max(low, Mathf.Max(minimumShots, maximumShots));
            int shots = Mathf.Min(inventory.Count, Random.Range(low, high + 1));
            RoomFirstDungeonGenerator.Room room = OwnerRoom();
            float damage = Damage(projectileDamageMultiplier);
            for (int i = 0; i < shots; i++)
            {
                int slot = inventory.ConsumeAt(Random.Range(0, inventory.Count));
                if (!visuals.TryGetValue(slot, out SpriteRenderer orb) || orb == null) continue;
                Vector3 origin = orb.transform.position;
                Vector2 direction = enemy.CurrentTarget.position - origin;
                if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
                BossWitchProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
                projectile.Initialize(enemyBody, direction.normalized, damage,
                    (point, travelDirection) => CreateStrikeLine(point, travelDirection, room));
                projectiles.Add(projectile);
                RemoveVisual(slot);
            }
        }

        private float Damage(float multiplier)
        {
            float value = (enemyBody.enemyData != null ? enemyBody.enemyData.damageAmount : 2f) * multiplier;
            if (statusEffects != null) value *= statusEffects.GetOutgoingDamageMultiplier();
            return Mathf.Max(0f, value);
        }

        private float RoomLimit(Vector2 point, RoomFirstDungeonGenerator.Room room)
        {
            BoundsInt bounds = room.Bounds;
            GameObject prefab = dungeonManager.CurrentDungeonData != null ? dungeonManager.CurrentDungeonData.bossRoomPrefab : null;
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
                limit = Mathf.Max(limit, Vector2.Distance(point, corner));
            return limit + 1f;
        }

        private bool Inside(Vector2 point, RoomFirstDungeonGenerator.Room room) =>
            dungeonManager.FindRoomAt(Vector2Int.FloorToInt(point)) == room;

        private bool TryAnchor(Vector2 impact, Vector2 direction, RoomFirstDungeonGenerator.Room room, float limit, out Vector2 anchor)
        {
            // A wall impact can lie just outside the floor. Search only along the original shot's line.
            for (float distance = 0f; distance <= limit; distance += 0.1f)
            {
                anchor = impact - direction * distance;
                if (Inside(anchor, room)) return true;
                anchor = impact + direction * distance;
                if (Inside(anchor, room)) return true;
            }
            anchor = impact;
            return false;
        }

        private Vector2 TraceEnd(Vector2 anchor, Vector2 direction, RoomFirstDungeonGenerator.Room room, float limit)
        {
            Vector2 end = anchor;
            for (float distance = 0.1f; distance <= limit; distance += 0.1f)
            {
                Vector2 point = anchor + direction * distance;
                if (!Inside(point, room)) break;
                end = point;
            }
            return end;
        }

        private void CreateStrikeLine(Vector3 impact, Vector2 direction, RoomFirstDungeonGenerator.Room room)
        {
            if (!isActiveAndEnabled || enemyBody == null || enemyBody.isDead || room == null || dungeonManager == null) return;
            direction.Normalize();
            float limit = RoomLimit(impact, room);
            if (!TryAnchor(impact, direction, room, limit, out Vector2 anchor)) return;
            Vector2 start = TraceEnd(anchor, -direction, room, limit);
            Vector2 end = TraceEnd(anchor, direction, room, limit);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / Mathf.Max(0.1f, strikeSpacing)));
            float damage = Damage(strikeDamageMultiplier);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(start, end, i / (float)steps);
                BossWitchThunderStrike strike = Instantiate(strikePrefab,
                    new Vector3(point.x, point.y, transform.position.z), Quaternion.identity);
                strike.Initialize(enemyBody, i * propagationInterval, strikeWarning, strikeRadius, damage);
                strikes.Add(strike);
            }
        }

        private void RemoveVisual(int slot)
        {
            if (!visuals.TryGetValue(slot, out SpriteRenderer renderer)) return;
            if (renderer != null)
            {
                renderer.gameObject.SetActive(false);
                Destroy(renderer.gameObject);
            }
            visuals.Remove(slot);
        }

        public void ClearAll()
        {
            foreach (SpriteRenderer renderer in visuals.Values)
                if (renderer != null) { renderer.gameObject.SetActive(false); Destroy(renderer.gameObject); }
            visuals.Clear();
            inventory.Clear();
            foreach (BossWitchProjectile projectile in projectiles)
                if (projectile != null) { projectile.gameObject.SetActive(false); Destroy(projectile.gameObject); }
            projectiles.Clear();
            foreach (BossWitchThunderStrike strike in strikes)
                if (strike != null) strike.Cancel();
            strikes.Clear();
        }

        private void OnDisable() => ClearAll();
    }
}
