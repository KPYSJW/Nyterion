using System.Collections.Generic;
using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    public interface IProjectileGateBeam
    {
        void StopImmediately();
    }

    /// <summary>레이저별 원본 프리팹을 재사용하며 한 입사 구간의 출구 위치와 각도를 유지합니다.</summary>
    public sealed class ProjectileGateBeamPath<T> where T : Component, IProjectileGateBeam
    {
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
        private readonly List<T> branches = new List<T>();
        private readonly List<Vector2> samples = new List<Vector2>();
        private ProjectileGateController gate;
        private Vector2 incomingDirection;

        public bool HasGate => gate != null && branches.Count > 0;
        public int Count => branches.Count;
        public float SizeMultiplier => gate != null ? gate.LaserSizeMultiplier : 1f;
        public T GetBranch(int index) => branches[index];
        public Vector3 GetStart(int index) => gate.GetLaserOutputPosition(samples[index].x);
        public Vector2 GetDirection(int index) => gate.GetLaserOutputDirection(incomingDirection, samples[index].y);

        public float Update(Transform caster, Vector3 start, Vector2 direction, float length,
            GameObject prefab, ObjectPoolManager pool)
        {
            ProjectileGateController nearest = null;
            float nearestDistance = length;
            if (prefab != null && prefab.GetComponent<T>() != null && caster != null)
            {
                hits.Clear();
                Physics2D.Raycast(start, direction, new ContactFilter2D { useTriggers = true }, hits, length);
                foreach (RaycastHit2D hit in hits)
                {
                    if (hit.collider != null && hit.collider.TryGetComponent(out ProjectileGateController candidate) &&
                        candidate.TryIntersectLaser(caster, start, direction, nearestDistance, out float distance))
                    {
                        nearest = candidate;
                        nearestDistance = distance;
                    }
                }
                hits.Clear();
            }
            if (nearest == null)
            {
                Clear();
                return length;
            }
            if (gate != nearest)
            {
                Clear();
                gate = nearest;
            }
            incomingDirection = direction;
            int count = gate.LaserOutputCount;
            while (branches.Count > count)
            {
                int last = branches.Count - 1;
                T branch = branches[last];
                branches.RemoveAt(last);
                samples.RemoveAt(last);
                if (branch != null) branch.StopImmediately();
            }
            while (branches.Count < count)
            {
                GameObject instance = pool != null
                    ? pool.SpawnFromPool(prefab, start, Quaternion.identity, 1)
                    : Object.Instantiate(prefab, start, Quaternion.identity);
                if (instance == null) break;
                branches.Add(instance.GetComponent<T>());
                samples.Add(new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)));
            }
            return HasGate ? nearestDistance : length;
        }

        public void Clear()
        {
            gate = null;
            foreach (T branch in branches)
                if (branch != null) branch.StopImmediately();
            branches.Clear();
            samples.Clear();
            hits.Clear();
        }
    }
}
