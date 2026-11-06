using System.Collections.Generic;
using Nytherion.GamePlay.Dungeon;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BossGameMasterOrbLinkPattern : MonoBehaviour, IBossGameMasterPattern
    {
        [Header("Orb Links / 구체 연결 광선")]
        [SerializeField, Min(2)] private int orbCount = 5;
        [Tooltip("모든 구체를 연결하기 위해 실제 생성된 구체 수 - 1 이상의 연결을 보장합니다.")]
        [SerializeField, Min(1)] private int connectionCount = 4;
        [SerializeField, Min(0.1f)] private float minimumSpacing = 4.5f;
        [SerializeField, Min(0.1f)] private float orbSize = 1.5f;
        [SerializeField, Min(0f)] private float wallClearance = 0.75f;
        [SerializeField, Min(0.05f)] private float warningDuration = 0.6f;
        [FormerlySerializedAs("beamDuration")]
        [Tooltip("광선 발사 후 다음 연결 예고까지의 간격입니다. 발사된 광선은 패턴 종료까지 유지됩니다.")]
        [SerializeField, Min(0.05f)] private float connectionInterval = 1f;
        [Tooltip("마지막 광선의 연결 간격이 지난 뒤 차원문과 모든 빔을 추가로 유지하는 시간입니다. 0이면 추가 대기 없이 종료합니다.")]
        [SerializeField, Min(0f)] private float holdDuration = 0f;
        [SerializeField, Min(0.01f)] private float launchDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float beamWidth = 0.7f;
        [SerializeField, Min(0f)] private float damage = 1f;
        [SerializeField, Min(0.05f)] private float damageInterval = 0.5f;
        [Tooltip("레이저 원본 프레임 시간 기준 재생 배속입니다. 1이면 원본 속도입니다.")]
        [SerializeField, Min(0.1f)] private float beamAnimationSpeed = 1f;
        [Tooltip("구체 회전 애니메이션의 초당 프레임 수입니다.")]
        [SerializeField, Min(1f)] private float framesPerSecond = 24f;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);
        [Header("Visuals / 이미지")]
        [SerializeField] private Sprite[] orbFrames;
        [SerializeField] private Sprite[] startFrames;
        [SerializeField] private Sprite[] bodyFrames;
        [SerializeField] private Sprite[] endFrames;
        [SerializeField] private Material orbMaterial;
        [SerializeField] private Material beamMaterial;
        [SerializeField] private Material warningMaterial;
        private BossGameMasterEnemy owner;
        private DungeonManager dungeon;
        private GameObject root;
        private readonly List<Vector2> candidates = new List<Vector2>();
        private readonly List<Vector2> points = new List<Vector2>();
        private readonly List<Vector2Int> links = new List<Vector2Int>();
        private readonly List<SpriteRenderer> orbs = new List<SpriteRenderer>();
        private readonly List<BossGameMasterLinkBeam> beams = new List<BossGameMasterLinkBeam>();
        private BossGameMasterLinkBeam pendingBeam;
        private LineRenderer warning;
        private MaterialPropertyBlock orbProperties;
        private int linkIndex, activeConnectionCount;
        private bool firing, finishing;
        private float stageAt, beganAt;
        public BossGameMasterPatternType PatternType => BossGameMasterPatternType.OrbLinks;

        private void Cache()
        {
            if (owner == null) owner = GetComponent<BossGameMasterEnemy>();
            if (dungeon == null) dungeon = FindObjectOfType<DungeonManager>();
        }

        private RoomFirstDungeonGenerator.Room Room() => owner.homeRoom ??
            (dungeon != null ? dungeon.FindRoomAt(Vector2Int.FloorToInt(transform.position)) : null);

        public bool CanStart()
        {
            Cache();
            return isActiveAndEnabled && owner != null && !owner.isDead && !owner.IsUpgrading &&
                dungeon != null && Room() != null && orbFrames != null && orbFrames.Length > 0 &&
                startFrames != null && startFrames.Length >= 21 && bodyFrames != null && bodyFrames.Length >= 13 &&
                endFrames != null && endFrames.Length >= 13 && orbMaterial != null && beamMaterial != null && warningMaterial != null;
        }

        public void Begin()
        {
            Clear();
            Cache();
            if (!CanStart()) return;
            SelectPoints(Room());
            if (!BuildConnections())
            {
                Debug.LogWarning("[GameMaster] 구체를 연결할 안전한 공간이 없습니다. 구체 간격과 방 크기를 확인하세요.", this);
                return;
            }
            root = new GameObject("GameMaster Orb Links");
            orbProperties = new MaterialPropertyBlock();
            SpriteRenderer body = GetComponent<SpriteRenderer>();
            for (int i = 0; i < points.Count; i++)
            {
                GameObject node = new GameObject("Orb " + (i + 1));
                node.transform.SetParent(root.transform, false);
                node.transform.position = points[i];
                node.transform.localScale = Vector3.one * Mathf.Max(0.1f, orbSize);
                SpriteRenderer visual = node.AddComponent<SpriteRenderer>();
                visual.sharedMaterial = orbMaterial;
                visual.sprite = orbFrames[0];
                visual.sortingLayerID = body != null ? body.sortingLayerID : 0;
                visual.sortingOrder = body != null ? body.sortingOrder + 4 : 24;
                orbs.Add(visual);
            }
            GameObject line = new GameObject("Next Connection Warning");
            line.transform.SetParent(root.transform, false);
            warning = line.AddComponent<LineRenderer>();
            warning.sharedMaterial = warningMaterial;
            warning.useWorldSpace = true;
            warning.positionCount = 2;
            warning.widthMultiplier = Mathf.Max(0.05f, beamWidth);
            warning.startColor = warning.endColor = new Color(1f, 0.05f, 0.02f, 0.25f);
            warning.sortingLayerID = orbs[0].sortingLayerID;
            warning.sortingOrder = orbs[0].sortingOrder - 2;
            beganAt = stageAt = Time.time;
            linkIndex = 0;
            firing = false;
            ShowWarning();
        }

        private bool BuildConnections()
        {
            links.Clear();
            activeConnectionCount = 0;
            if (points.Count < 2) return false;
            bool[,] visible = new bool[points.Count, points.Count];
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 1; j < points.Count; j++)
                    visible[i, j] = visible[j, i] = !Physics2D.Linecast(points[i], points[j], wallLayers);

            bool[] connected = new bool[points.Count];
            connected[Random.Range(0, points.Count)] = true;
            List<Vector2Int> choices = new List<Vector2Int>();
            HashSet<int> used = new HashSet<int>();
            // Each of the first N - 1 beams attaches exactly one new orb to the existing network.
            for (int count = 1; count < points.Count; count++)
            {
                choices.Clear();
                for (int from = 0; from < points.Count; from++)
                    if (connected[from])
                        for (int to = 0; to < points.Count; to++)
                            if (!connected[to] && visible[from, to]) choices.Add(new Vector2Int(from, to));
                if (choices.Count == 0) { links.Clear(); return false; }
                Vector2Int edge = choices[Random.Range(0, choices.Count)];
                connected[edge.y] = true;
                links.Add(edge);
                used.Add(Mathf.Min(edge.x, edge.y) * points.Count + Mathf.Max(edge.x, edge.y));
            }

            // Extra requested beams use the remaining pairs without disturbing the coverage-first order.
            choices.Clear();
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 1; j < points.Count; j++)
                    if (visible[i, j] && !used.Contains(i * points.Count + j)) choices.Add(new Vector2Int(i, j));
            for (int i = 0; i < choices.Count; i++)
            {
                int chosen = Random.Range(i, choices.Count);
                Vector2Int edge = choices[chosen]; choices[chosen] = choices[i]; choices[i] = edge;
            }
            links.AddRange(choices);
            activeConnectionCount = Mathf.Max(connectionCount, points.Count - 1);
            return true;
        }

        private void SelectPoints(RoomFirstDungeonGenerator.Room room)
        {
            BoundsInt bounds = room.Bounds;
            GameObject prefab = dungeon.CurrentDungeonData != null ? dungeon.CurrentDungeonData.bossRoomPrefab : null;
            Tilemap floor = prefab != null && room.type == RoomFirstDungeonGenerator.RoomType.Boss ? prefab.GetComponentInChildren<Tilemap>() : null;
            if (floor != null) { bounds = floor.cellBounds; bounds.position += Vector3Int.RoundToInt(room.center); }
            for (int x = bounds.xMin; x < bounds.xMax; x++)
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                    if (dungeon.FindRoomAt(new Vector2Int(x, y)) == room) candidates.Add(new Vector2(x + 0.5f, y + 0.5f));
            float clearance = Mathf.Max(wallClearance, orbSize * 0.5f);
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                Vector2 point = candidates[i];
                bool valid = Physics2D.OverlapCircle(point, clearance, wallLayers) == null;
                for (int x = Mathf.FloorToInt(point.x - clearance); x <= Mathf.FloorToInt(point.x + clearance); x++)
                    for (int y = Mathf.FloorToInt(point.y - clearance); y <= Mathf.FloorToInt(point.y + clearance); y++)
                    {
                        Vector2 nearest = new Vector2(Mathf.Clamp(point.x, x, x + 1), Mathf.Clamp(point.y, y, y + 1));
                        if ((nearest - point).sqrMagnitude < clearance * clearance && dungeon.FindRoomAt(new Vector2Int(x, y)) != room) valid = false;
                    }
                if (!valid) candidates.RemoveAt(i);
            }
            // Randomize the first point, then maximize distance to the nearest selected orb.
            // This spreads the group across the room instead of merely avoiding close neighbors.
            while (points.Count < Mathf.Clamp(orbCount, 2, 64))
            {
                int chosen = FarthestCandidateIndex(Mathf.Max(minimumSpacing, orbSize));
                if (chosen < 0) break;
                points.Add(candidates[chosen]);
                candidates.RemoveAt(chosen);
            }
            if (points.Count < orbCount) Debug.LogWarning($"[GameMaster] 구체 {orbCount}개 중 {points.Count}개를 안전하게 배치했습니다.", this);
        }

        private int FarthestCandidateIndex(float minimumDistance)
        {
            if (candidates.Count == 0) return -1;
            if (points.Count == 0) return Random.Range(0, candidates.Count);
            float minimumSquared = minimumDistance * minimumDistance;
            float bestDistance = -1f;
            int bestIndex = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                float nearest = float.PositiveInfinity;
                foreach (Vector2 point in points)
                    nearest = Mathf.Min(nearest, (candidates[i] - point).sqrMagnitude);
                if (nearest < minimumSquared || nearest <= bestDistance) continue;
                bool connectable = false;
                foreach (Vector2 point in points)
                    if (!Physics2D.Linecast(point, candidates[i], wallLayers)) { connectable = true; break; }
                if (!connectable) continue;
                bestDistance = nearest;
                bestIndex = i;
            }
            return bestIndex;
        }

        private void ShowWarning()
        {
            Vector2Int link = links[linkIndex % links.Count];
            warning.SetPosition(0, points[link.x]);
            warning.SetPosition(1, points[link.y]);
            warning.enabled = true;
            GameObject effect = new GameObject("Connecting Beam");
            effect.transform.SetParent(root.transform, false);
            pendingBeam = effect.AddComponent<BossGameMasterLinkBeam>();
            beams.Add(pendingBeam);
            pendingBeam.Initialize(owner, points[link.x], points[link.y], beamWidth, launchDuration, warningDuration,
                damage, damageInterval, beamAnimationSpeed, startFrames, bodyFrames, endFrames, beamMaterial, orbMaterial,
                orbs[0].sortingLayerID, orbs[0].sortingOrder + 1);
        }

        public bool Tick()
        {
            if (root == null || owner == null || owner.isDead || owner.IsUpgrading) { Clear(); return true; }
            int frame = Mathf.FloorToInt((Time.time - beganAt) * framesPerSecond) % orbFrames.Length;
            for (int i = 0; i < orbs.Count; i++)
            {
                orbs[i].sprite = orbFrames[frame];
                orbs[i].color = Color.white;
                orbs[i].GetPropertyBlock(orbProperties);
                // Keep the sprite texture alongside the flash override; otherwise the shader can sample white.
                orbProperties.SetTexture("_MainTex", orbFrames[frame].texture);
                orbProperties.SetFloat("_FlashAmount", 0f);
                orbs[i].SetPropertyBlock(orbProperties);
            }
            if (finishing)
            {
                foreach (BossGameMasterLinkBeam beam in beams)
                    if (beam != null && !beam.IsFinished) return false;
                Clear();
                return true;
            }
            if (!firing)
            {
                if (pendingBeam == null) { Clear(); return true; }
                if (!pendingBeam.CanFire || !pendingBeam.Fire()) return false;
                warning.enabled = false;
                pendingBeam = null;
                stageAt = Time.time;
                firing = true;
            }
            float wait = Mathf.Max(0.05f, connectionInterval);
            if (linkIndex + 1 >= activeConnectionCount) wait += Mathf.Max(0f, holdDuration);
            if (Time.time - stageAt < wait) return false;
            linkIndex++;
            if (linkIndex >= activeConnectionCount)
            {
                finishing = true;
                warning.enabled = false;
                foreach (BossGameMasterLinkBeam beam in beams)
                    if (beam != null) beam.BeginEnd();
                return false;
            }
            firing = false;
            stageAt = Time.time;
            ShowWarning();
            return false;
        }

        public void Clear()
        {
            foreach (BossGameMasterLinkBeam beam in beams)
                if (beam != null) beam.Cancel();
            beams.Clear();
            pendingBeam = null;
            if (root != null) { root.SetActive(false); Destroy(root); }
            root = null;
            warning = null;
            orbs.Clear(); points.Clear(); links.Clear(); candidates.Clear();
            linkIndex = activeConnectionCount = 0;
            firing = finishing = false;
        }

        private void OnDisable() => Clear();
    }
}
