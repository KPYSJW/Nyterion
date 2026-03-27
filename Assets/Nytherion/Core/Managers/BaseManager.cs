using System;
using UnityEngine;
using Nytherion.Core.Data;
using Nytherion.Core.Interfaces;
using VContainer;
using VContainer.Unity;

namespace Nytherion.Core.Managers
{
    public abstract class BaseManager : MonoBehaviour, ISaveable, IInitializable
    {
        [Header("Base Manager Settings")]
        [SerializeField] protected bool autoInitializeOnAwake = false;

        public event Action OnInitialized;
        
        [System.NonSerialized]
        private bool isInitialized;
        public bool IsInitialized
        {
            get => isInitialized;
            private set => isInitialized = value;
        }

        [System.NonSerialized]
        private bool isActive = true;
        public bool IsActive
        {
            get => isActive;
            private set => isActive = value;
        }

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
                return;
            }

            try
            {
                OnInitializeInternal();
                IsInitialized = true;
                OnInitialized?.Invoke();
                
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

        protected virtual void OnActiveStateChanged(bool active)
        {
            // 하위 클래스에서 필요에 따라 오버라이드
        }
        
        public virtual void PopulateSaveData(SaveData saveData)
        {

        }

        public virtual void LoadFromSaveData(SaveData saveData)
        {

        }

        protected virtual void OnDestroy()
        {
            OnInitialized = null;
        }

        public virtual string GetStatusInfo()
        {
            return $"[{GetType().Name}] Initialized: {IsInitialized}, Active: {IsActive}";
        }
    }
}