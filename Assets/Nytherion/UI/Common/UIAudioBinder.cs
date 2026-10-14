using Nytherion.Core.Managers;
using Nytherion.UI.Components;
using UnityEngine.UI;
using VContainer.Unity;

namespace Nytherion.UI
{
    /// <summary>활성 UI 컨트롤과 동적으로 생성한 드롭다운 항목에 공통 효과음을 연결합니다.</summary>
    public sealed class UIAudioBinder : ITickable
    {
        private readonly AudioManager audioManager;
        private Selectable[] selectables = new Selectable[64];

        public UIAudioBinder(AudioManager audioManager)
        {
            this.audioManager = audioManager;
        }

        public void Tick()
        {
            if (selectables.Length < Selectable.allSelectableCount)
                selectables = new Selectable[Selectable.allSelectableCount * 2];

            int count = Selectable.AllSelectablesNoAlloc(selectables);
            for (int i = 0; i < count; i++)
            {
                Selectable control = selectables[i];
                if (control == null || control is Scrollbar) continue;
                if (!control.TryGetComponent(out UISelectableSound sound))
                {
                    sound = control.gameObject.AddComponent<UISelectableSound>();
                    sound.Bind(audioManager, control);
                }
            }
            // 비활성/제거된 컨트롤을 버퍼가 계속 참조하지 않도록 합니다.
            System.Array.Clear(selectables, 0, selectables.Length);
        }
    }
}
