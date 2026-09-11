using UnityEngine;

namespace Nytherion.UI
{
    /// <summary>
    /// 게임 내 모든 독점 UI(인벤토리, 스킬, 상점 등)의 열림 상태를 중앙 관리
    /// VContainer를 통해 주입되어 관리됨
    /// </summary>
    public class GlobalUIManager : MonoBehaviour
    {
        private UIPanelBase currentActivePanel;

        /// <summary>
        /// 새로운 패널이 열릴 때 호출되어 기존 패널을 닫음
        /// </summary>
        public void RegisterOpenedPanel(UIPanelBase newPanel)
        {
            if (currentActivePanel != null && currentActivePanel != newPanel)
            {
                currentActivePanel.Close();
            }

            currentActivePanel = newPanel;
        }

        /// <summary>
        /// 패널이 닫힐 때 호출되어 현재 활성화된 패널 정보를 비움
        /// </summary>
        public void RegisterClosedPanel(UIPanelBase panel)
        {
            if (currentActivePanel == panel)
            {
                currentActivePanel = null;
            }
        }

        public bool IsAnyPanelOpen() => currentActivePanel != null;

        public void CloseCurrentPanel()
        {
            if (currentActivePanel != null)
            {
                currentActivePanel.Close();
            }
        }
    }
}
