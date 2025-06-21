using UnityEngine;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Engravings;

namespace Nytherion.Services
{
    public class PlayerPrefsEngravingSaveService : IEngravingSaveService
    {
        private const string SAVE_KEY = "EngravingGridData";

        public void SaveEngravings(EngravingGridState state)
        {
            if (state == null) return;
            string json = JsonUtility.ToJson(state);
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();
        }

        public EngravingGridState LoadEngravings()
        {
            if (!PlayerPrefs.HasKey(SAVE_KEY))
            {
                return null;
            }

            string json = PlayerPrefs.GetString(SAVE_KEY);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            
            return JsonUtility.FromJson<EngravingGridState>(json);
        }
    }
}