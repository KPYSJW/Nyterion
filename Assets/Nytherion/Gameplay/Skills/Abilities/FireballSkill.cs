using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nytherion.GamePlay.Skills
{
    public class FireballSkill : SkillBase
    {
        private readonly List<FireballProjectile> projectiles = new List<FireballProjectile>();

        public override bool CanUse() => base.CanUse() && isActiveAndEnabled &&
            skillData is FireballSkillData data && data.projectilePrefab != null && data.HasValidFrames;

        protected override void Activate()
        {
            FireballSkillData data = (FireballSkillData)skillData;
            Vector3 origin = firePoint != null ? firePoint.position : (caster != null ? caster.position : transform.position);
            Vector2 direction = firePoint != null ? (Vector2)firePoint.right : Vector2.right;
            Camera camera = Camera.main;
            if (camera != null && Mouse.current != null)
            {
                Vector2 screen = Mouse.current.position.ReadValue();
                Vector3 mouse = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(camera.transform.position.z - origin.z)));
                direction = (Vector2)(mouse - origin);
            }
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            direction.Normalize();
            var player = SkillOwner;
            int level = player != null ? player.GetSkillLevel(data) : Mathf.Max(1, data.skillLevel);
            PlayerSkillManager skills = caster != null ? caster.GetComponentInParent<PlayerSkillManager>() :
                GetComponentInParent<PlayerSkillManager>();
            int extra = player != null && player.currentPlayerData != null
                ? Mathf.Max(0, Mathf.FloorToInt(player.currentPlayerData.extraProjectiles)) : 0;
            int count = 1 + extra;
            float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float damage = ApplySkillDamage(data.GetDamage(level));
            float size = EffectSizeMultiplier;
            bool homing = skills != null && skills.DebugFireballHoming;
            float spreadAngle = Mathf.Clamp(data.projectileSpreadAngle, 0f, 180f);
            for (int i = 0; i < count; i++)
            {
                FireballProjectile projectile = projectiles.Find(item => item != null && !item.gameObject.activeSelf);
                if (projectile == null)
                {
                    projectile = Instantiate(data.projectilePrefab);
                    projectiles.Add(projectile);
                }
                float offset = count == 1 ? 0f : Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, i / (float)(count - 1));
                float radians = (centerAngle + offset) * Mathf.Deg2Rad;
                Vector2 launchDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                projectile.Begin(data, origin, launchDirection, damage, size, homing,
                    skills != null ? skills.DebugFireballHomingTurnSpeed : 180f,
                    skills != null ? skills.DebugFireballHomingStraightTime : 0f);
            }
        }

        private void OnDisable()
        {
            foreach (FireballProjectile projectile in projectiles)
                if (projectile != null) projectile.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (FireballProjectile projectile in projectiles)
                if (projectile != null) Destroy(projectile.gameObject);
            projectiles.Clear();
        }
    }
}
