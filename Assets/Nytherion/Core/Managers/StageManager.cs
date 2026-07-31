using Nytherion.Data.ScriptableObjects.Stage;
using Nytherion.GamePlay.Dungeon;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Nytherion.Core.Data;
using UnityEngine.SceneManagement;

namespace Nytherion.Core.Managers
{
    public class StageManager : BaseManager
    {
        [Header("스테이지 설정")]
        [Tooltip("게임 시작 시 로드할 가장 첫 번째 스테이지 데이터입니다.")]
        [SerializeField] private StageData startingStage;

        public StageData CurrentStage { get; private set; }

        public event System.Action OnChapterChanged;

        private SceneTransitionManager sceneTransitionManager;
        private DungeonManager dungeonManager;

        public void SetDungeonManager(DungeonManager dungeonManager)
        {
            this.dungeonManager = dungeonManager;
        }

        protected override void Awake()
        {
            base.Awake();
           // transform.SetParent(null);
            //DontDestroyOnLoad(gameObject);
            CurrentStage = startingStage;
            sceneTransitionManager = FindObjectOfType<SceneTransitionManager>();
        }

        /// <summary>
        /// 보스가 죽었을 때 호출되어 승리 포탈을 생성합니다.
        /// </summary>
        public void SpawnBossPortal(Vector3 position)
        {
            if (CurrentStage.dungeonData.victoryPortalPrefab != null)
            {
                GameObject portalObject = Instantiate(CurrentStage.dungeonData.victoryPortalPrefab, position, Quaternion.identity);
                BossPortal portal = portalObject.GetComponent<BossPortal>();
                if (portal != null)
                {
                    portal.Initialize(this);
                }
            }
        }

        /// <summary>
        /// 현재 스테이지를 클리어하고 다음 스테이지로 넘어갑니다.
        /// </summary>
        public void AdvanceToNextStage()
        {
            if (CurrentStage.nextStageData == null)
            {
                Debug.Log("마지막 스테이지 클리어!");
                return;
            }

            StageData nextStage = CurrentStage.nextStageData;

            // 챕터가 전환될 때 이벤트 발생
            if (CurrentStage.chapterNumber != nextStage.chapterNumber)
            {
                OnChapterChanged?.Invoke();
            }

            string sceneToLoad = CurrentStage.victorySceneName;
            CurrentStage = nextStage;

            Debug.Log($"다음 스테이지 '{CurrentStage.stageName}'(으)로 진행합니다.");

            if (sceneToLoad!=SceneManager.GetActiveScene().name)
            {
                Debug.Log($"씬 이동");
                sceneTransitionManager.LoadScene(sceneToLoad);
            }
            else
            {
                dungeonManager?.RegenerateDungeon();
            }
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            // TODO: CurrentStage 정보 저장 구현
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            // TODO: CurrentStage 정보 로드 구현
        }
    }
}