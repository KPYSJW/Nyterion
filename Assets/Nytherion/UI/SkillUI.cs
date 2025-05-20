using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.UI
{
    public class SkillUI : MonoBehaviour
    {
        public class SkillSlotUI
        {
            public Image icon;
            public Image cooldownOverlay;
            public TMP_Text cooldownText;
        }

        public SkillSlotUI[] skillSlots;
        
    }
}

