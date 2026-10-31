using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// One of the planks nailed across Old Guy's front door (owner, 2026-10-31: "leave the boarded door, make it so you have to break the wood
    /// planks"): three punches, two blows of a blade or a bat, or three bullets (which stop in it) and it is torn off and falls. A swing that
    /// rakes the plank over several frames counts once. Breaking is shared (TutorialHouseDamage): every player sees the plank fall, and a player
    /// who joins later finds it gone. When all three are gone the door behind them opens (BoardedDoor).
    /// </summary>
    public sealed class BoardedPlank : MonoBehaviour, IBreakableSurface
    {
        /// <summary>This plank's number in the house (0 to OldGuysHouse.FrontDoorPlanks - 1).</summary>
        public int Piece;

        /// <summary>A fist or a bullet takes one off; a blade, a bat or a thrown bottle two.</summary>
        public const int Health = 3;

        /// <summary>Blows closer together than this are one blow (a sword's sweep meets the plank on several frames).</summary>
        public const float BlowSeconds = 0.25f;

        /// <summary>How long the torn-off plank lies on the porch before it is cleared away.</summary>
        public const float LieSeconds = 8.0f;

        public int Left { get; private set; } = Health;

        public bool Broken { get; private set; }

        private float _lastBlow = float.NegativeInfinity;
        private Vector3 _blowDirection;

        private void OnEnable()
        {
            TutorialHouseDamage.Shared.PieceBroken += OnPieceBroken;
        }

        private void OnDisable()
        {
            TutorialHouseDamage.Shared.PieceBroken -= OnPieceBroken;
        }

        private void Start()
        {
            if (TutorialHouseDamage.Shared.IsBroken(Piece))
            {
                Broken = true;
                Destroy(gameObject);   // torn off before this machine built the house
            }
        }

        public bool TakePunch(Vector3 point) => TakeBlow(point, Vector3.zero, BlowStrength.Light);

        /// <summary>True when the blow landed; a bullet stops in the wood, so for a bullet it says "not glass".</summary>
        public bool TakeBlow(Vector3 point, Vector3 direction, BlowStrength strength)
        {
            if (Broken)
            {
                return false;
            }
            if (Time.time - _lastBlow >= BlowSeconds)
            {
                _lastBlow = Time.time;
                Left -= strength == BlowStrength.Heavy ? 2 : 1;
                if (Left <= 0)
                {
                    _blowDirection = direction;
                    TutorialHouseDamage.Shared.Break(Piece);
                }
            }
            return strength != BlowStrength.Bullet;
        }

        private void OnPieceBroken(int piece)
        {
            if (piece != Piece || Broken)
            {
                return;
            }
            Broken = true;
            // Torn off: it falls on its own, pushed the way the blow went (or straight off the door when another machine broke it).
            transform.SetParent(null, true);
            var body = gameObject.AddComponent<Rigidbody>();
            body.mass = 4.0f;
            Vector3 push = _blowDirection.sqrMagnitude > 1e-4f ? _blowDirection.normalized : transform.forward;
            body.linearVelocity = push * 2.5f + Vector3.up * 1.0f;
            body.angularVelocity = new Vector3(Random.Range(-3f, 3f), Random.Range(-3f, 3f), Random.Range(-3f, 3f));
            Destroy(gameObject, LieSeconds);
        }
    }
}
