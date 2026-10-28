using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nytherion.GamePlay.Skills
{
    public class ProjectileGateSkill : SkillBase
    {
        private readonly List<ProjectileGateController> gates = new List<ProjectileGateController>();

        public override bool CanUse() => base.CanUse() && skillData is ProjectileGateSkillData data &&
            data.gatePrefab != null && caster != null;

        public override float GetCooldownTime()
        {
            if (!(skillData is ProjectileGateSkillData data)) return base.GetCooldownTime();
            PlayerManager player = caster != null ? caster.GetComponentInParent<PlayerManager>() :
                GetComponentInParent<PlayerManager>();
            int level = player != null ? player.GetSkillLevel(data) : Mathf.Max(1, data.skillLevel);
            return ApplyDebugCooldownReduction(data.GetCooldown(level));
        }

        protected override void Activate()
        {
            var data = (ProjectileGateSkillData)skillData;
            Vector2 direction = firePoint != null ? (Vector2)firePoint.right : Vector2.right;
            Camera camera = Camera.main;
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
            ProjectileGateController gate = gates.Find(candidate => candidate != null && !candidate.gameObject.activeSelf);
            if (gate == null)
            {
                gate = Instantiate(data.gatePrefab);
                gates.Add(gate);
            }
            gate.Begin(data, caster, direction);
        }

        private void OnDisable()
        {
            foreach (ProjectileGateController gate in gates)
                if (gate != null) gate.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (ProjectileGateController gate in gates)
                if (gate != null) Destroy(gate.gameObject);
            gates.Clear();
        }
    }
}
