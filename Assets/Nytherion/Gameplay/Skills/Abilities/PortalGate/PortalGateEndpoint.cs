using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Skill;
using TMPro;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class PortalGateEndpoint : MonoBehaviour, IConditionalInteractable
    {
        private PortalGateSkill owner;
        private PortalGateSkillData data;
        private SpriteRenderer visual;
        private TMP_Text prompt;
        private float animationStarted;
        public InteractableType Type => InteractableType.None;
        public string InteractionPrompt => owner != null
            ? $"이동 · {owner.RemainingTraversals}회 · {Mathf.CeilToInt(owner.RemainingLifetime)}초" : "이동";

        public void Begin(PortalGateSkill skill, PortalGateSkillData settings, Vector3 position)
        {
            owner = skill;
            data = settings;
            transform.position = position;
            visual = GetComponent<SpriteRenderer>();
            prompt = GetComponentInChildren<TMP_Text>(true);
            CircleCollider2D trigger = GetComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = data.interactionRadius;
            animationStarted = Time.time;
            gameObject.SetActive(true);
            RefreshVisual();
        }

        public bool CanInteract(Transform interactor) => owner != null && owner.CanTraverse(this, interactor);

        public void Interact()
        {
            if (owner != null) owner.TryTraverse(this);
        }

        private void Update() => RefreshVisual();

        private void RefreshVisual()
        {
            if (data == null || owner == null) return;
            if (data.animationFrames != null && data.animationFrames.Length > 0)
            {
                int index = Mathf.FloorToInt((Time.time - animationStarted) * Mathf.Max(1f, data.framesPerSecond));
                visual.sprite = data.animationFrames[index % data.animationFrames.Length];
            }
            if (prompt != null)
            {
                prompt.gameObject.SetActive(false);
            }
        }

        public void Close()
        {
            owner = null;
            data = null;
            gameObject.SetActive(false);
        }
    }
}
