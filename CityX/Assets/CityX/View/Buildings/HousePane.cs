using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// One window pane of Old Guy's house (owner, 2026-10-31: "make the windows shatterable"), taking blows as the city's windows do (BlowStrength):
    /// a fist cracks it and the next one shatters it, a blade, a thrown bottle or a shoulder shatters it at once, a bullet cracks it and goes on
    /// through. Shattered, it falls in pieces (GlassFractureBurst) and leaves the opening clear. The shattering is shared
    /// (TutorialHouseDamage): another player sees it go, and a player who joins later finds it gone; a crack is this machine's own.
    /// </summary>
    public sealed class HousePane : MonoBehaviour, IBreakableSurface
    {
        /// <summary>This pane's number in the house (OldGuysHouse: after the front door's planks, in the order the windows are written).</summary>
        public int Piece;

        /// <summary>The pane's width along its wall and its height, metres.</summary>
        public Vector2 Size;

        /// <summary>Its wall runs along the house's x (else along its z).</summary>
        public bool AlongLocalX;

        public Material ShardMaterial;

        public AudioClip[] CrackSounds;

        public AudioClip[] BreakSounds;

        /// <summary>How far its sounds carry, metres: a window across the street is heard.</summary>
        public const float HearingRange = 30.0f;

        /// <summary>A cracked pane is paler and less clear, so the next blow is seen coming.</summary>
        private static readonly Color CrackedTint = new Color(0.86f, 0.90f, 0.94f, 0.5f);

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        public bool Cracked { get; private set; }

        public bool Shattered { get; private set; }

        private Vector3 _blowPoint, _blowDirection;
        private BlowStrength _blowStrength = BlowStrength.Heavy;

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
                Clear();   // broken before this machine built the house
            }
        }

        public bool TakePunch(Vector3 point) => TakeBlow(point, Vector3.zero, BlowStrength.Light);

        public bool TakeBlow(Vector3 point, Vector3 direction, BlowStrength strength)
        {
            if (Shattered)
            {
                return false;
            }
            if (strength == BlowStrength.Heavy || strength == BlowStrength.Light && Cracked)
            {
                _blowPoint = point;
                _blowDirection = direction;
                _blowStrength = strength;
                TutorialHouseDamage.Shared.Break(Piece);
            }
            else if (!Cracked)
            {
                Crack(point);
            }
            return true;
        }

        public bool IsGlassAt(Vector3 point) => !Shattered;

        private void Crack(Vector3 point)
        {
            Cracked = true;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColour, CrackedTint);
            GetComponent<MeshRenderer>().SetPropertyBlock(block);
            Play(CrackSounds, point, 0.5f);
        }

        private void OnPieceBroken(int piece)
        {
            if (piece != Piece || Shattered)
            {
                return;
            }
            Vector3 outward = transform.TransformDirection(AlongLocalX ? Vector3.forward : Vector3.right);
            bool here = _blowPoint != Vector3.zero;
            Vector3 point = here ? _blowPoint : transform.position;
            Vector3 through = GlassBreaker.ShardDirection(outward, here ? _blowDirection : Vector3.zero);
            if (ShardMaterial != null)
            {
                GlassFractureBurst.Shatter(ShardMaterial, Frame(), point, through, _blowStrength, unchecked(Piece * 7919 + Time.frameCount));
            }
            Play(BreakSounds, point, 0.8f);
            Clear();
        }

        /// <summary>Gone: nothing drawn, nothing to collide with.</summary>
        private void Clear()
        {
            Shattered = true;
            GetComponent<MeshRenderer>().enabled = false;
            GetComponent<Collider>().enabled = false;
        }

        /// <summary>The pane as the fracture cuts it: its bottom-left corner, across and up its face, the way it faces.</summary>
        private PaneFrame Frame()
        {
            Vector3 across = transform.TransformDirection(AlongLocalX ? Vector3.right : Vector3.forward);
            Vector3 up = transform.up;
            Vector3 normal = Vector3.Cross(across, up).normalized;
            Vector3 origin = transform.position - across * (Size.x * 0.5f) - up * (Size.y * 0.5f);
            return new PaneFrame(origin, across, up, normal, Size.x, Size.y);
        }

        private static void Play(AudioClip[] bank, Vector3 point, float volume)
        {
            if (bank == null || bank.Length == 0)
            {
                return;
            }
            AudioClip clip = bank[Random.Range(0, bank.Length)];
            if (clip != null)
            {
                PositionalSound.Play(clip, point, volume, Random.Range(0.9f, 1.1f), 0.0f, HearingRange);
            }
        }
    }
}
