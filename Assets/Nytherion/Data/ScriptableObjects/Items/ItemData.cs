using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Items
{
    [CreateAssetMenu(fileName = "NewItemData", menuName = "Data/Item")]

    public class ItemData : ScriptableObject
    {
        // public string ID => itemName; // 또는 고유한 ID를 위한 별도 필드 추가
        [SerializeField] private string _uniqueID;
        public string ID
        {
            get
            {
                // Ensure _uniqueID is initialized, especially for items created before this change.
                // This might also be a good place for a one-time migration if needed,
                // but for now, we'll focus on ensuring new items get a GUID.
                if (string.IsNullOrEmpty(_uniqueID))
                {
                    // This is primarily for editor-time initialization.
                    // Consider if runtime generation for existing items is needed
                    // or if they should be updated manually/via script.
                    // For safety, we log an error if accessed at runtime without being set,
                    // as OnValidate won't run in a build for existing unset IDs.
                    #if UNITY_EDITOR
                    if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        _uniqueID = System.Guid.NewGuid().ToString();
                        UnityEditor.EditorUtility.SetDirty(this); // Mark as dirty to save the new ID
                        Debug.Log($"Generated new ID for {this.name}: {_uniqueID}");
                    } else if (string.IsNullOrEmpty(_uniqueID)) {
                         Debug.LogError($"ItemData '{this.name}' has an empty uniqueID at runtime. Please ensure IDs are generated and saved in the editor.");
                    }
                    #else
                    if (string.IsNullOrEmpty(_uniqueID)) {
                        Debug.LogError($"ItemData '{this.name}' has an empty uniqueID at runtime. This should have been generated in the editor.");
                        // Fallback for builds if absolutely necessary, though editor generation is preferred.
                        // _uniqueID = System.Guid.NewGuid().ToString();
                    }
                    #endif
                }
                return _uniqueID;
            }
            // Optional: Add a private setter or a method if IDs need to be changed programmatically,
            // but generally, once set, they shouldn't change.
        }
        public string itemName;
        public Sprite icon;
        [TextArea] public string description;
        public bool isStackable = true;
        public int maxStack = 99;

        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_uniqueID))
            {
                _uniqueID = System.Guid.NewGuid().ToString();
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"ItemData '{this.name}' created or updated with new ID: {_uniqueID}");
            }
        }
        #endif
    }
}