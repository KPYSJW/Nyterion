using System;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Skills;
using Nytherion.UI.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI.Skill
{
    /// <summary>실제 스킬의 쿨타임과 포탑 충전 상태를 읽어 전투 HUD에 표시한다.</summary>
    public sealed class SkillCooldownHUD : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            public RectTransform root;
            public Image cooldownOverlay;
            public TMP_Text cooldownText;
            public Image readyFlash;
            public TMP_Text chargeText;
            public GameObject rechargeTrack;
            public Image rechargeFill;

            [NonSerialized] public SkillBase boundSkill;
            [NonSerialized] public bool wasCoolingDown;
            [NonSerialized] public int previousCharges;
            [NonSerialized] public float flashRemaining;
        }

        [SerializeField] private Slot[] slots;
        [SerializeField] private float flashDuration = 0.2f;
        private PlayerSkillManager playerSkills;

        public void Bind(PlayerSkillManager manager, QuickSlotUI[] hudSlots)
        {
            playerSkills = manager;
            for (int i = 0; i < slots.Length; i++)
            {
                bool available = hudSlots != null && i < hudSlots.Length && hudSlots[i] != null;
                slots[i].root.gameObject.SetActive(available);
                if (!available) continue;
                // 아이콘 위에 그리되 덮개 크기는 슬롯 배경 전체에 맞춘다.
                Transform target = hudSlots[i].IconImage != null
                    ? hudSlots[i].IconImage.transform : hudSlots[i].transform;
                slots[i].root.SetParent(target, false);
                slots[i].root.anchorMin = slots[i].root.anchorMax = Vector2.one * 0.5f;
                slots[i].root.pivot = Vector2.one * 0.5f;
                slots[i].root.sizeDelta = hudSlots[i].SkillBackgroundSize;
                slots[i].root.anchoredPosition = Vector2.zero;
                slots[i].readyFlash.sprite = slots[i].cooldownOverlay.sprite;
                slots[i].readyFlash.rectTransform.anchorMin = Vector2.zero;
                slots[i].readyFlash.rectTransform.anchorMax = Vector2.one;
                slots[i].readyFlash.rectTransform.sizeDelta = Vector2.zero;
                slots[i].readyFlash.rectTransform.anchoredPosition = Vector2.zero;
            }
            Refresh();
        }

        private void Update() => Refresh();

        private void OnDestroy()
        {
            if (slots == null) return;
            foreach (Slot slot in slots)
                if (slot.root != null && !slot.root.IsChildOf(transform)) Destroy(slot.root.gameObject);
        }

        public void Refresh()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                SkillBase skill = playerSkills != null && i < playerSkills.equippedSkills.Length
                    ? playerSkills.equippedSkills[i] : null;
                bool occupied = skill != null && skill.skillData != null;
                bool changed = !ReferenceEquals(slot.boundSkill, skill);
                slot.boundSkill = skill;
                float remaining = occupied ? skill.GetRemainingCooldown() : 0f;
                float duration = occupied ? Mathf.Max(0.01f, skill.GetCooldownTime()) : 1f;
                bool coolingDown = remaining > 0f;
                TurretSkill turret = skill as TurretSkill;
                int charges = turret != null ? turret.CurrentCharges : 0;

                if (changed)
                    slot.flashRemaining = 0f;
                else if (occupied && (turret != null ? charges > slot.previousCharges : slot.wasCoolingDown && !coolingDown))
                    slot.flashRemaining = flashDuration;

                slot.cooldownOverlay.enabled = coolingDown;
                slot.cooldownOverlay.fillAmount = Mathf.Clamp01(remaining / duration);
                slot.cooldownText.enabled = coolingDown;
                if (coolingDown) slot.cooldownText.SetText("{0}", Mathf.CeilToInt(remaining));

                slot.chargeText.enabled = turret != null;
                if (turret != null) slot.chargeText.SetText("{0}", charges);
                bool recharging = turret != null && charges < turret.MaxCharges;
                slot.rechargeTrack.SetActive(recharging);
                slot.rechargeFill.fillAmount = recharging
                    ? 1f - Mathf.Clamp01(turret.RemainingRechargeTime / duration) : 0f;

                slot.readyFlash.enabled = slot.flashRemaining > 0f;
                Color flashColor = slot.readyFlash.color;
                flashColor.a = Mathf.Clamp01(slot.flashRemaining / Mathf.Max(0.01f, flashDuration));
                slot.readyFlash.color = flashColor;
                slot.flashRemaining = Mathf.Max(0f, slot.flashRemaining - Time.deltaTime);
                slot.wasCoolingDown = coolingDown;
                slot.previousCharges = charges;
            }
        }
    }
}
