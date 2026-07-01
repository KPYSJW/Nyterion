using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapon
{
    /// <summary>
    /// Abaddon 슬래시 이펙트 프리팹 중 자식 객체에 콜라이더가 배치된 경우 사용합니다.
    /// 자식 객체의 물리 충돌(Triggger) 이벤트를 부모의 AbaddonCollision 컴포넌트로 중계합니다.
    /// </summary>
    public class AbaddonChildCollision : MonoBehaviour
    {
        private AbaddonCollision parentCollision;

        private void Awake()
        {
            parentCollision = GetComponentInParent<AbaddonCollision>();
            if (parentCollision == null)
            {
                Debug.LogWarning($"[AbaddonChildCollision] '{gameObject.name}'의 부모 객체에서 AbaddonCollision 컴포넌트를 찾을 수 없습니다.");
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (parentCollision != null)
            {
                parentCollision.ProcessTriggerEnter(collision);
            }
        }
    }
}
