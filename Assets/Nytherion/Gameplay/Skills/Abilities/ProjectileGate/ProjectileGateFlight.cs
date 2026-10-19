using System.Collections.Generic;
using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>분열탄의 이동 모듈을 잠시 정지하고 반환 시 기존 상태를 복구합니다.</summary>
    public sealed class ProjectileGateFlight : MonoBehaviour
    {
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private CollisionObject collision;
        private float expiresAt;
        private bool flying;

        public void Begin(float lifetime, Quaternion rotation)
        {
            collision = GetComponent<CollisionObject>();
            Suspend(GetComponent<HomingProj>());
            Suspend(GetComponent<SineWaveProj>());
            Suspend(GetComponent<BoomerangMovement>());
            // 기존 거리 제한은 출구 위치를 기준으로 새로 시작하지 않도록 비활성화합니다.
            Suspend(GetComponent<ProjDistanceLimit>());
            Suspend(GetComponent<ProjCameraBoundsLimit>());
            Suspend(GetComponent<AutoReturnToPool>());
            transform.rotation = rotation;
            expiresAt = Time.time + Mathf.Max(0.1f, lifetime);
            flying = true;
        }

        private void Suspend(Behaviour component)
        {
            if (component == null || !component.enabled) return;
            suspended.Add(component);
            component.enabled = false;
        }

        private void Update()
        {
            if (flying && Time.time >= expiresAt) collision.ReturnToPool();
        }

        private void OnDisable()
        {
            flying = false;
            foreach (Behaviour component in suspended)
                if (component != null) component.enabled = true;
            suspended.Clear();
        }
    }
}
