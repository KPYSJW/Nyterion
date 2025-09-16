using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.UI.EngravingBoard;
using VContainer;
using System.Collections;
using System.Linq;

namespace Nytherion.GamePlay.Engravings
{
    /// <summary>
    /// 각인 시스템의 디버그와 검증을 위한 유틸리티 클래스
    /// </summary>
    public class EngravingSystemDebugger : MonoBehaviour
    {
        [Header("디버그 설정")]
        [SerializeField] private bool enableDebugMode = true;
        [SerializeField] private float systemCheckInterval = 5f;

        private EngravingManager engravingManager;
        private EngravingGridUI engravingGridUI;
        private EngravingTooltip engravingTooltip;

        [Inject]
        public void Construct(EngravingManager engravingManager)
        {
            this.engravingManager = engravingManager;
            Debug.Log($"[EngravingSystemDebugger] VContainer 의존성 주입 완료 - EngravingManager: {engravingManager != null}");
        }

        private void Start()
        {
            if (enableDebugMode)
            {
                StartCoroutine(PeriodicSystemCheck());
                TestSystemConnections();
            }
        }

        private void TestSystemConnections()
        {
            Debug.Log("=== 각인 시스템 연결 테스트 시작 ===");

            // EngravingManager 테스트
            if (engravingManager != null)
            {
                Debug.Log($"✅ EngravingManager 연결 성공");
                Debug.Log($"   - 그리드 크기: {engravingManager.GridRows}x{engravingManager.GridColumns}");
                Debug.Log($"   - 보관소 블록 수: {engravingManager.GetStorageBlocks()?.Count() ?? 0}");
                Debug.Log($"   - 배치된 블록 수: {engravingManager.GetPlacedBlocks()?.Count() ?? 0}");
            }
            else
            {
                Debug.LogError("❌ EngravingManager 연결 실패");
            }

            // EngravingGridUI 찾기 및 테스트
            engravingGridUI = FindObjectOfType<EngravingGridUI>();
            if (engravingGridUI != null)
            {
                Debug.Log($"✅ EngravingGridUI 발견");
                Debug.Log($"   - GameObject: {engravingGridUI.gameObject.name}");
                Debug.Log($"   - Active: {engravingGridUI.gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.LogError("❌ EngravingGridUI를 찾을 수 없습니다");
            }

            // EngravingTooltip 찾기 및 테스트
            engravingTooltip = FindObjectOfType<EngravingTooltip>();
            if (engravingTooltip != null)
            {
                Debug.Log($"✅ EngravingTooltip 발견");
                Debug.Log($"   - GameObject: {engravingTooltip.gameObject.name}");
                Debug.Log($"   - Active: {engravingTooltip.gameObject.activeInHierarchy}");
                Debug.Log($"   - Instance: {EngravingTooltip.Instance != null}");
            }
            else
            {
                Debug.LogError("❌ EngravingTooltip을 찾을 수 없습니다");
            }

            Debug.Log("=== 각인 시스템 연결 테스트 완료 ===");
        }

        private IEnumerator PeriodicSystemCheck()
        {
            while (enableDebugMode)
            {
                yield return new WaitForSeconds(systemCheckInterval);

                if (engravingManager != null)
                {
                    var storageCount = engravingManager.GetStorageBlocks()?.Count() ?? 0;
                    var placedCount = engravingManager.GetPlacedBlocks()?.Count() ?? 0;

                    Debug.Log($"[EngravingSystemDebugger] 주기적 점검 - 보관소: {storageCount}개, 배치됨: {placedCount}개");
                }
            }
        }

        [ContextMenu("강제 시스템 재연결 테스트")]
        public void ForceSystemReconnectionTest()
        {
            TestSystemConnections();
        }

        [ContextMenu("각인 UI 강제 새로고침")]
        public void ForceRefreshEngravingUI()
        {
            if (engravingGridUI != null)
            {
                Debug.Log("[EngravingSystemDebugger] EngravingGridUI 강제 새로고침 중...");
                StartCoroutine(engravingGridUI.Initialize());
            }
            else
            {
                Debug.LogError("[EngravingSystemDebugger] EngravingGridUI를 찾을 수 없습니다");
            }
        }

        [ContextMenu("테스트 블록 추가")]
        public void AddTestBlock()
        {
            if (engravingManager != null)
            {
                Debug.Log("[EngravingSystemDebugger] 테스트 블록 추가 시도...");
                // 이 기능은 EngravingManager에 public 메서드가 필요합니다
            }
        }

        private void OnDestroy()
        {
            enableDebugMode = false;
        }

        #region 디버그 GUI
        private void OnGUI()
        {
            if (!enableDebugMode) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("각인 시스템 디버그", GUI.skin.box);

            if (engravingManager != null)
            {
                GUILayout.Label($"보관소 블록: {engravingManager.GetStorageBlocks()?.Count() ?? 0}");
                GUILayout.Label($"배치된 블록: {engravingManager.GetPlacedBlocks()?.Count() ?? 0}");
            }
            else
            {
                GUILayout.Label("EngravingManager: 연결 안됨", GUI.skin.GetStyle("Label"));
            }

            if (GUILayout.Button("시스템 재연결 테스트"))
            {
                ForceSystemReconnectionTest();
            }

            if (GUILayout.Button("UI 강제 새로고침"))
            {
                ForceRefreshEngravingUI();
            }

            GUILayout.EndArea();
        }
        #endregion
    }
}