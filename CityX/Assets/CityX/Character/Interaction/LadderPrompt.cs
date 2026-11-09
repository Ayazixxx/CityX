using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The ladder a body could get on, named when it is looked at and got on with E (HoverTarget): at an open manhole "Climb down", at a
    /// ladder's foot or beside it "Climb". Players walk past a ladder nothing points out; walking into the foot of one gets on it too
    /// (PlayerLadder.TryWalkOn), but the top of one, a hole in the road, has only this.
    /// </summary>
    public sealed class LadderPrompt : HoverTarget
    {
        public const string Name = "Ladder";
        public const string ClimbKeys = "[E] Climb";
        public const string ClimbDownKeys = "[E] Climb down";

        private PlayerLadder _body;
        private Ladder _ladder;
        private bool _fromTop;
        private bool _found;

        /// <summary>The prompt for one body's ladders (the body of this machine's player).</summary>
        public static LadderPrompt For(PlayerLadder body)
        {
            var holder = new GameObject("LadderPrompt") { hideFlags = HideFlags.HideAndDontSave };
            var prompt = holder.AddComponent<LadderPrompt>();
            prompt._body = body;
            return prompt;
        }

        private void Update()
        {
            if (_body == null)
            {
                Destroy(gameObject);
                return;
            }
            _found = _body.Nearby(out _ladder, out _fromTop);
        }

        /// <summary>The hole in the road from above; from below or beside it, the rungs before the chest.</summary>
        public override Vector3 Centre
        {
            get
            {
                if (_fromTop)
                {
                    return _ladder.Top + _ladder.Normal * PlayerLadder.Standoff;
                }
                float chest = _body != null ? _body.transform.position.y + BodyRig.StandingHeight * 0.6f : _ladder.Foot.y;
                return _ladder.Foot + Vector3.up * Mathf.Clamp(chest - _ladder.Foot.y, 0.5f, _ladder.Height);
            }
        }

        public override string Label => Name;

        public override string Keys => _fromTop ? ClimbDownKeys : ClimbKeys;

        public override float ReachMetres => PlayerLadder.TopReach + 1.5f;

        public override float LookDot => 0.6f;

        public override bool Available => _found;

        public override void Use(GameObject player)
        {
            if (_found && _body != null && player == _body.gameObject)
            {
                _body.GetOn(_ladder, _fromTop);
            }
        }
    }
}
