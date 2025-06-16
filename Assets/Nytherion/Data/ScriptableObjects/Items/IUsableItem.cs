using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Items
{

    public interface IUseableItem
    {

        void Use();

        void CancelUse();
    }
}
