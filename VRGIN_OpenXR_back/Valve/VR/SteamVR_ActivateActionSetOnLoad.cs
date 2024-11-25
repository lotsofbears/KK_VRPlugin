using UnityEngine;

namespace Valve.VR
{
    public class SteamVR_ActivateActionSetOnLoad : MonoBehaviour
    {
        public SteamVR_ActionSet actionSet = SteamVR_Input.GetActionSet("default");

        public SteamVR_Input_Sources forSources;

        public bool disableAllOtherActionSets;

        public bool activateOnStart = true;

        public bool deactivateOnDestroy = true;

        public int initialPriority;

        private void Start()
        {
            if (actionSet != null && activateOnStart) actionSet.Activate(forSources, initialPriority, disableAllOtherActionSets);
        }

        private void OnDestroy()
        {
            if (actionSet != null && deactivateOnDestroy) actionSet.Deactivate(forSources);
        }
    }
}
