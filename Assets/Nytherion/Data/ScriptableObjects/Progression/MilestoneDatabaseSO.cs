using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Progression
{
    [CreateAssetMenu(fileName = "MilestoneDatabase", menuName = "Data/Progression/Milestone Database")]
    public class MilestoneDatabaseSO : ScriptableObject
    {
        public List<MilestoneData> allMilestones;

        public MilestoneData GetMilestoneById(string id)
        {
            return allMilestones.Find(m => m.milestoneID == id);
        }
    }
}
