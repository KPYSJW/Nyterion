using System;
using Nytherion.Core.Enums;

namespace Nytherion.Core.Interfaces
{
    /// <summary>
    /// 순수 데이터 관리 매니저를 위한 인터페이스
    /// UI 관련 기능은 포함하지 않음
    /// </summary>
    public interface IDataManager
    {
        /// <summary>
        /// 매니저 초기화 상태
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 매니저 초기화
        /// </summary>
        void Initialize();
    }

    /// <summary>
    /// 데이터 변경 알림을 위한 제네릭 인터페이스
    /// UI는 이 이벤트를 구독하여 업데이트
    /// </summary>
    public interface IDataChangeNotifier<T>
    {
        /// <summary>
        /// 데이터가 변경되었을 때 발생하는 이벤트
        /// </summary>
        event Action<T> OnDataChanged;
    }

    /// <summary>
    /// 특정 데이터 타입에 대한 변경 알림
    /// </summary>
    public interface IInventoryDataNotifier : IDataChangeNotifier<InventoryChangeData>
    {
    }

    public interface ICurrencyDataNotifier : IDataChangeNotifier<CurrencyChangeData>
    {
    }

    public interface IPuzzleDataNotifier : IDataChangeNotifier<PuzzleChangeData>
    {
    }

    // 데이터 변경 정보를 담는 구조체들
    [System.Serializable]
    public struct InventoryChangeData
    {
        public int slotIndex;
        public string itemId;
        public int newCount;
        public InventoryChangeType changeType;
    }

    [System.Serializable]
    public struct CurrencyChangeData
    {
        public CurrencyType currencyType;
        public int oldAmount;
        public int newAmount;
        public int changeAmount;
    }

    [System.Serializable]
    public struct PuzzleChangeData
    {
        public string puzzleId;
        public PuzzleChangeType changeType;
        public int remainingAttempts;
        public bool isCompleted;
        public bool isFailed;
    }

    public enum InventoryChangeType
    {
        ItemAdded,
        ItemRemoved,
        ItemCountChanged,
        SlotCleared,
        InventoryLoaded
    }

    public enum PuzzleChangeType
    {
        PuzzleRegistered,
        AttemptUsed,
        PuzzleCompleted,
        PuzzleFailed,
        PuzzleReset
    }
}