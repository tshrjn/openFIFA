using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Lives on the GameObject that has the Animator (the visual model). Unity only sends OnAnimatorIK and
    /// animation events to that GameObject, so this forwards them to the driver on the player root.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class PlayerAnimatorHooks : MonoBehaviour
    {
        public PlayerAnimationDriver Driver;

        private void Awake()
        {
            if (Driver == null) Driver = GetComponentInParent<PlayerAnimationDriver>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (Driver != null) Driver.HandleAnimatorIK(layerIndex);
        }

        /// <summary>Animation event fired on the contact frame of action clips (name = AnimStateNames.ContactEvent).</summary>
        public void OnActionContact()
        {
            if (Driver != null) Driver.HandleContactEvent();
        }

        /// <summary>Animation-event overload some clips may use.</summary>
        public void OnActionContact(int unused) => OnActionContact();
    }
}
