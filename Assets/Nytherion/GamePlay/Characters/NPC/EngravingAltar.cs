using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using VContainer;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class EngravingAltar : MonoBehaviour, IInteractable
    {
        public InteractableType Type => InteractableType.EngravingAltar;
        public bool IsInteractable { get; set; } = true;

        private EventManager eventManager;

        [Inject]
        public void Construct(EventManager eventManager)
        {
            this.eventManager = eventManager;
        }

        public void Interact()
        {
            Debug.Log($"[EngravingAltar] Interact() 호출됨 - IsInteractable: {IsInteractable}");
            if (!IsInteractable) return;

            // 각 상호작용 객체는 자신의 타입에 맞는 이벤트를 직접 발생시킵니다.
            // InteractionManager는 이 메서드를 호출하는 역할만 담당합니다.
            eventManager?.TriggerInteractionEvent(Type);
        }
    }
}