using UnityEngine;

namespace Nytherion.GamePlay.Relics
{
    [DisallowMultipleComponent]
    public class DropRelicVFXAnimationEvent : MonoBehaviour
    {
        // DropRelicEffect 애니메이션 마지막 프레임의 Animation Event에서 호출한다.
        public void AnimationFinished()
        {
            Destroy(gameObject);
        }
    }
}
