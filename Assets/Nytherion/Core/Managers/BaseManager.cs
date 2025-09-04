using System;
using UnityEngine;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using Zenject;

namespace Nytherion.Core.Managers
{
    public abstract class BaseManager : MonoBehaviour, ISaveable, IInitializable
    {
        [Header("Base Manager Settings")]
        [SerializeField] protected bool autoInitializeOnAwake = false;
        
        
        public event Action OnInitialized;
        
        public bool IsInitialized { get; private set; }
        
        public bool IsActive { get; private set; } = true;

        protected virtual void Awake()
        {
            if (autoInitializeOnAwake)
            {
                Initialize();
            }
        }

        public virtual void Initialize()
        {
            if (IsInitialized)
            {
                Debug.LogWarning($"[{GetType().Name}] Already initialized. Skipping duplicate initialization.");
                return;
            }

            try
            {
                OnInitializeInternal();
                IsInitialized = true;
                OnInitialized?.Invoke();
                
                Debug.Log($"[{GetType().Name}] Successfully initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{GetType().Name}] Failed to initialize: {e.Message}");
                throw;
            }
        }

        protected virtual void OnInitializeInternal()
        {
            // 기본 구현은 비어있음 - 하위 클래스에서 필요에 따라 오버라이드
        }

        public virtual void SetActive(bool active)
        {
            if (IsActive == active) return;
            
            IsActive = active;
            OnActiveStateChanged(active);
        }

        /// <param name="active">새로운 활성화 상태</param>
        protected virtual void OnActiveStateChanged(bool active)
        {
            // 하위 클래스에서 필요에 따라 오버라이드
        }
        
        public abstract void PopulateSaveData(SaveData saveData);

        
        /// <param name="saveData">불러올 데이터 객체</param>
        public abstract void LoadFromSaveData(SaveData saveData);

        protected virtual void OnDestroy()
        {
            OnInitialized = null;
        }

        public virtual string GetStatusInfo()
        {
            return $"[{GetType().Name}] Initialized: {IsInitialized}, Active: {IsActive}";
        }

#if UNITY_EDITOR
        
        [ContextMenu("Show Status Info")]
        private void ShowStatusInfo()
        {
            Debug.Log(GetStatusInfo());
        }
#endif
    }
}