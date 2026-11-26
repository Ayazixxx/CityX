namespace CityX.Character
{
    /// <summary>
    /// Another player's body in a crowd (CrowdDetail; docs/CROWD_TEST.md): further down the crowd's tiers, less of the pose is worked out. Past
    /// the nearest (<see cref="CrowdTier.Light"/>), the feet and fingers are left as the clip puts them: the foot placement was two thirds of a
    /// copy's posing (measured: 48 of about 75 microseconds a body a frame), and at that distance a planted foot or a closed grip does not show.
    /// Off the screen (<see cref="CrowdTier.Unseen"/>), nothing is posed (its animator no longer moves the bones either, and posing over a held
    /// pose frame after frame would only drift it); only what counts time goes on (a blow's freeze, the crouch). This machine's own body has no
    /// tier and is always posed in full.
    /// </summary>
    public sealed partial class PlayerLocomotion : ICrowdDetailed
    {
        private CrowdTier _crowdTier = CrowdTier.Full;

        public void ShowAt(CrowdTier tier)
        {
            if (tier != CrowdTier.Full && _crowdTier == CrowdTier.Full)
            {
                ForgetTheFeet();   // back near, the feet are placed afresh from where the clip has them, not from where they were left
            }
            _crowdTier = tier;
        }

        /// <summary>Whether the feet are placed and the fingers closed (only in full).</summary>
        private bool PosesTheDetails => _crowdTier == CrowdTier.Full;

        /// <summary>The foot placement's memory let go: no locks, no glide, no stance, and no last place to measure a move from.</summary>
        private void ForgetTheFeet()
        {
            _feet.Reset();
            _footwork.Reset();
            _glide.Reset();
            _stairRun.Reset();
            _stairSteps.Reset();
            _footIkHasLast = false;
            FootIkActive = false;
        }

        /// <summary>Off the screen: only what counts time, as the start of PoseAfterTheAnimator does it for a copy.</summary>
        private void KeepTimeUnseen()
        {
            if (_impactFrozen && _freezeTickedFrame != UnityEngine.Time.frameCount)
            {
                TickImpactFreeze(UnityEngine.Time.deltaTime);
            }
            if (_crouchTickedFrame != UnityEngine.Time.frameCount)
            {
                FollowRemoteCrouch(UnityEngine.Time.deltaTime);
            }
        }
    }
}
