using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.GamePlay.Characters.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI
{
    public class EngraveUI : MonoBehaviour
    {
        public Image[] engraveSlots;

        private void Update()
        {
            UpdateEngraveUI();


        }

        public void UpdateEngraveUI()
        {
            foreach (Image slot in engraveSlots)
            {
                slot.gameObject.SetActive(false);
            }
            List<EngravingData> engrave = PlayerManager.Instance.playerEngravingManager.GetCurrentEngravings();
            for (int i=0;i< engrave.Count;++i)
            {
                engraveSlots[i].gameObject.SetActive(true);
                engraveSlots[i].sprite = engrave[i].Image;
            }
        }
    }
}

