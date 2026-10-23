using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nytherion.GamePlay.Skills
{
    public class PortalGateSkill : SkillBase
    {
        private PortalGateEndpoint entrance;
        private PortalGateEndpoint exit;
        private float expiresAt;
        private float nextTraversalAt;
        private Vector3 preparedDestination;

        public int RemainingTraversals { get; private set; }
        public float RemainingLifetime => HasActivePair ? Mathf.Max(0f, expiresAt - Time.time) : 0f;
        public bool HasActivePair => entrance != null && exit != null &&
            entrance.gameObject.activeSelf && exit.gameObject.activeSelf;

        public override bool CanUse() => base.CanUse() && caster != null &&
            skillData is PortalGateSkillData data && data.portalPrefab != null;

        public override bool TryUse()
        {
            // 실패한 배치는 SkillBase의 쿨타임을 시작하지 않습니다.
            if (!CanUse()) return false;
            Camera camera = Camera.main;
            if (!PortalGatePlacement.TryFindDestination(caster, GetAimDirection(camera),
                (PortalGateSkillData)skillData, camera, out preparedDestination)) return false;
            return base.TryUse();
        }

        private Vector2 GetAimDirection(Camera camera)
        {
            Vector2 direction = firePoint != null ? (Vector2)firePoint.right : Vector2.right;
            if (camera != null && Mouse.current != null)
            {
                Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                Plane plane = new Plane(Vector3.forward, caster.position);
                if (plane.Raycast(ray, out float distance))
                {
                    Vector2 aim = ray.GetPoint(distance) - caster.position;
                    if (aim.sqrMagnitude > 0.0001f) direction = aim.normalized;
                }
            }
            return direction;
        }

        protected override void Activate()
        {
            PortalGateSkillData data = (PortalGateSkillData)skillData;
            ClosePair();
            if (entrance == null) entrance = Instantiate(data.portalPrefab);
            if (exit == null) exit = Instantiate(data.portalPrefab);
            RemainingTraversals = Mathf.Max(1, data.maxTraversals);
            expiresAt = Time.time + Mathf.Max(0.1f, data.lifetime);
            nextTraversalAt = Time.time;
            entrance.Begin(this, data, caster.position);
            exit.Begin(this, data, preparedDestination);
        }

        public bool CanTraverse(PortalGateEndpoint source, Transform interactor)
        {
            if (!isActiveAndEnabled || caster == null || !caster.gameObject.activeInHierarchy || interactor != caster ||
                !HasActivePair || (source != entrance && source != exit) || Time.timeScale <= 0f ||
                UIPanelBase.IsAnyPanelOpen || Time.time >= expiresAt || Time.time < nextTraversalAt ||
                RemainingTraversals <= 0) return false;
            PlayerController controller = caster.GetComponent<PlayerController>();
            if (controller != null && (controller.IsDashing || controller.IsKnockedBack)) return false;
            PlayerHealth health = caster.GetComponent<PlayerHealth>();
            if (health != null && health.CurrentHealth <= 0f) return false;
            PortalGateSkillData data = (PortalGateSkillData)skillData;
            return ((Vector2)(caster.position - source.transform.position)).sqrMagnitude <=
                data.interactionRadius * data.interactionRadius;
        }

        public bool TryTraverse(PortalGateEndpoint source)
        {
            if (!CanTraverse(source, caster)) return false;
            PortalGateSkillData data = (PortalGateSkillData)skillData;
            PortalGateEndpoint target = source == entrance ? exit : entrance;
            Physics2D.SyncTransforms();
            Bounds footprint = PortalGatePlacement.GetFootprint(caster, data);
            Vector2 route = target.transform.position - source.transform.position;
            float distance = route.magnitude;
            if (!PortalGatePlacement.IsClear(target.transform.position, footprint, data) ||
                PortalGatePlacement.ClearDistance(source.transform.position, route.normalized,
                    distance, footprint, data) < distance - 0.001f) return false;

            // MovePosition 대신 물리 위치를 즉시 바꾸고 보간의 이전 위치도 초기화합니다.
            Rigidbody2D body = caster.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                RigidbodyInterpolation2D interpolation = body.interpolation;
                body.interpolation = RigidbodyInterpolation2D.None;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.position = target.transform.position;
                caster.position = target.transform.position;
                body.interpolation = interpolation;
            }
            else caster.position = target.transform.position;
            Physics2D.SyncTransforms();
            nextTraversalAt = Time.time + Mathf.Max(0.1f, data.traversalDelay);
            RemainingTraversals--;
            if (RemainingTraversals <= 0) ClosePair();
            return true;
        }

        private void Update()
        {
            bool anyActive = (entrance != null && entrance.gameObject.activeSelf) ||
                (exit != null && exit.gameObject.activeSelf);
            if (anyActive && (!HasActivePair || caster == null || !caster.gameObject.activeInHierarchy || Time.time >= expiresAt))
                ClosePair();
        }

        private void ClosePair()
        {
            if (entrance != null) entrance.Close();
            if (exit != null) exit.Close();
            RemainingTraversals = 0;
        }

        private void OnDisable() => ClosePair();

        private void OnDestroy()
        {
            if (entrance != null) Destroy(entrance.gameObject);
            if (exit != null) Destroy(exit.gameObject);
        }
    }
}
