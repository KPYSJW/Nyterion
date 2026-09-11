using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Core.Managers;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Nytherion.UI.Controllers
{
    public class EngraveUI : MonoBehaviour
    {
        public Image[] engraveSlots;
        
        private PlayerManager playerManager;
        
        [Inject]
        public void Construct(PlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }

        private void Update()
        {
            UpdateEngraveUI();
        }

        public void UpdateEngraveUI()
        {
            if (playerManager == null) return;
            
            foreach (Image slot in engraveSlots)
            {
                slot.gameObject.SetActive(false);
            }
            List<RelicData> engrave = playerManager.playerRelicManager.GetCurrentRelics();
            for (int i=0;i< engrave.Count;++i)
            {
                engraveSlots[i].gameObject.SetActive(true);
                engraveSlots[i].sprite = engrave[i].Image;
            }
        }
    }
}

