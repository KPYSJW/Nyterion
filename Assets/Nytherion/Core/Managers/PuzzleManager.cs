using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Enums;
using System;

namespace Nytherion.Core.Managers
{
    public class PuzzleManager : BaseManager, ISaveable
    {
        private Dictionary<string, PuzzleAttemptData> puzzleAttempts;
        private EventManager eventManager;

        // 이벤트 정의
        public event Action<string, int> OnAttemptUsed; // puzzleId, remainingAttempts
        public event Action<string> OnPuzzleCompleted; // puzzleId
        public event Action<string> OnPuzzleFailed; // puzzleId
        public event Action<string> OnPuzzleReset; // puzzleId

        [Inject]
        public void Construct(EventManager eventManager)
        {
            this.eventManager = eventManager;
        }

        public override void Initialize()
        {
            base.Initialize();
            puzzleAttempts = new Dictionary<string, PuzzleAttemptData>();
            Debug.Log("<color=cyan>[PuzzleManager] 퍼즐 매니저 초기화 완료</color>");
        }

        /// <summary>
        /// 새 퍼즐을 등록합니다
        /// </summary>
        public void RegisterPuzzle(string puzzleId, PuzzleType puzzleType, int maxAttempts = 3)
        {
            if (puzzleAttempts.ContainsKey(puzzleId))
            {
                Debug.LogWarning($"[PuzzleManager] 이미 등록된 퍼즐입니다: {puzzleId}");
                return;
            }

            PuzzleAttemptData puzzleData = new PuzzleAttemptData(puzzleId, puzzleType, maxAttempts);
            puzzleAttempts[puzzleId] = puzzleData;
            Debug.Log($"<color=cyan>[PuzzleManager] 퍼즐 등록: {puzzleId} (최대 시도 횟수: {maxAttempts})</color>");
        }

        /// <summary>
        /// 퍼즐 시도 가능 여부를 확인합니다
        /// </summary>
        public bool CanAttemptPuzzle(string puzzleId)
        {
            if (!puzzleAttempts.ContainsKey(puzzleId))
            {
                Debug.LogError($"[PuzzleManager] 등록되지 않은 퍼즐: {puzzleId}");
                return false;
            }

            return puzzleAttempts[puzzleId].CanAttempt();
        }

        /// <summary>
        /// 퍼즐 시도 횟수를 소모합니다
        /// </summary>
        public bool UseAttempt(string puzzleId)
        {
            if (!puzzleAttempts.ContainsKey(puzzleId))
            {
                Debug.LogError($"[PuzzleManager] 등록되지 않은 퍼즐: {puzzleId}");
                return false;
            }

            PuzzleAttemptData puzzleData = puzzleAttempts[puzzleId];
            if (!puzzleData.CanAttempt())
            {
                Debug.LogWarning($"[PuzzleManager] 퍼즐 시도 불가능: {puzzleId} (시도 횟수 소진)");
                return false;
            }

            puzzleData.UseAttempt();
            OnAttemptUsed?.Invoke(puzzleId, puzzleData.remainingAttempts);

            if (puzzleData.isFailed)
            {
                OnPuzzleFailed?.Invoke(puzzleId);
                Debug.Log($"<color=red>[PuzzleManager] 퍼즐 실패: {puzzleId} (시도 횟수 소진)</color>");
            }

            return true;
        }

        /// <summary>
        /// 퍼즐 성공을 처리합니다
        /// </summary>
        public void CompletePuzzle(string puzzleId)
        {
            if (!puzzleAttempts.ContainsKey(puzzleId))
            {
                Debug.LogError($"[PuzzleManager] 등록되지 않은 퍼즐: {puzzleId}");
                return;
            }

            PuzzleAttemptData puzzleData = puzzleAttempts[puzzleId];
            puzzleData.CompleteSuccess();
            OnPuzzleCompleted?.Invoke(puzzleId);
            Debug.Log($"<color=green>[PuzzleManager] 퍼즐 성공: {puzzleId}</color>");
        }

        /// <summary>
        /// 퍼즐 정보를 조회합니다
        /// </summary>
        public PuzzleAttemptData GetPuzzleData(string puzzleId)
        {
            if (!puzzleAttempts.ContainsKey(puzzleId))
            {
                Debug.LogError($"[PuzzleManager] 등록되지 않은 퍼즐: {puzzleId}");
                return null;
            }

            return puzzleAttempts[puzzleId];
        }

        /// <summary>
        /// 퍼즐을 리셋합니다 (디버그/관리자 기능)
        /// </summary>
        public void ResetPuzzle(string puzzleId)
        {
            if (!puzzleAttempts.ContainsKey(puzzleId))
            {
                Debug.LogError($"[PuzzleManager] 등록되지 않은 퍼즐: {puzzleId}");
                return;
            }

            puzzleAttempts[puzzleId].Reset();
            OnPuzzleReset?.Invoke(puzzleId);
            Debug.Log($"<color=yellow>[PuzzleManager] 퍼즐 리셋: {puzzleId}</color>");
        }

        /// <summary>
        /// 남은 시도 횟수를 가져옵니다
        /// </summary>
        public int GetRemainingAttempts(string puzzleId)
        {
            if (!puzzleAttempts.ContainsKey(puzzleId))
            {
                Debug.LogError($"[PuzzleManager] 등록되지 않은 퍼즐: {puzzleId}");
                return 0;
            }

            return puzzleAttempts[puzzleId].remainingAttempts;
        }

        /// <summary>
        /// 퍼즐 완료 여부를 확인합니다
        /// </summary>
        public bool IsPuzzleCompleted(string puzzleId)
        {
            if (!puzzleAttempts.ContainsKey(puzzleId))
            {
                return false;
            }

            return puzzleAttempts[puzzleId].isCompleted;
        }

        /// <summary>
        /// 퍼즐 실패 여부를 확인합니다
        /// </summary>
        public bool IsPuzzleFailed(string puzzleId)
        {
            if (!puzzleAttempts.ContainsKey(puzzleId))
            {
                return false;
            }

            return puzzleAttempts[puzzleId].isFailed;
        }

        // ISaveable 구현 - 나중에 사용 예정으로 주석 처리
        public override void PopulateSaveData(SaveData saveData)
        {
            // 퍼즐 시스템 비활성화로 인해 주석 처리
            /*
            saveData.puzzleAttempts = new Dictionary<string, PuzzleAttemptData>();

            foreach (var kvp in puzzleAttempts)
            {
                saveData.puzzleAttempts[kvp.Key] = kvp.Value;
            }

            Debug.Log($"[PuzzleManager] 퍼즐 데이터 저장: {saveData.puzzleAttempts.Count}개 퍼즐");
            */
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            // 퍼즐 시스템 비활성화로 인해 주석 처리
            /*
            if (saveData.puzzleAttempts != null)
            {
                puzzleAttempts = new Dictionary<string, PuzzleAttemptData>(saveData.puzzleAttempts);
                Debug.Log($"[PuzzleManager] 퍼즐 데이터 로드: {puzzleAttempts.Count}개 퍼즐");
            }
            else
            {
                puzzleAttempts = new Dictionary<string, PuzzleAttemptData>();
                Debug.Log("[PuzzleManager] 새로운 퍼즐 데이터 시작");
            }
            */
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}