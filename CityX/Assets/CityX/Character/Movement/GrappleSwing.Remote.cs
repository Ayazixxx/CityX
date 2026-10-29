using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE ROPE AS OTHER PLAYERS SEE IT (docs/GRAPPLE_RAGDOLL.md, docs/MULTIPLAYER_STATE.md; owner, 2026-10-29: "fix the grapple gap"). Until
    /// now another player's copy showed no rope at all, in either swing. The owner's swing says when the hook catches (and where) and when the
    /// rope goes (<see cref="IReplicatedAbility"/>); the copy draws the rope from the hook in its own hand to the same point for as long as it is
    /// out. The body hanging limp from it is every limp body's sync (NetworkedRagdollSync, RagdollMirror): the copy's hand, posed as the
    /// owner's is, is where the rope comes from.
    /// </summary>
    public sealed partial class GrappleSwing : IReplicatedAbility
    {
        public const string Id = "grapple-swing";

        /// <summary>The events (<see cref="AbilityEvent.Kind"/>): the hook caught (Origin the hook point), the rope gone (Origin the body).</summary>
        public const int Hooked = 0;

        public const int Unhooked = 1;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private IGrappleLauncher _remoteHook;
        private bool _remoteRope;
        private Vector3 _remoteAnchor;

        /// <summary>A copy drawing its owner's rope.</summary>
        public bool IsShowingRemoteRope => _remoteRope;

        public void PlayRemote(AbilityEvent evt)
        {
            switch (evt.Kind)
            {
                case Hooked:
                    HideRemoteRope();
                    _remoteAnchor = evt.Origin;
                    _remoteRope = true;
                    break;
                case Unhooked:
                    HideRemoteRope();
                    break;
            }
        }

        private void HideRemoteRope()
        {
            _remoteRope = false;
            if (HookAlive(_remoteHook))
            {
                _remoteHook.HideRope();
            }
            _remoteHook = null;
        }

        private static bool HookAlive(IGrappleLauncher hook)
        {
            return hook != null && !(hook is Object unityObject && unityObject == null);
        }

        /// <summary>A copy's rope, from the hook in its hand (found again if the hand's item changed) to the point its owner's caught.</summary>
        private void DrawRemoteRope()
        {
            if (!_remoteRope)
            {
                return;
            }
            if (!HookAlive(_remoteHook))
            {
                _remoteHook = GetComponentInChildren<IGrappleLauncher>();
                if (!HookAlive(_remoteHook))
                {
                    return;
                }
                _remoteHook.ShowRope(_remoteAnchor);
            }
            _remoteHook.UpdateRope(_remoteAnchor);
        }
    }
}
