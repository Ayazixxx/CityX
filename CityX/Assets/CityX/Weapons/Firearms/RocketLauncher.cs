using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BODY'S RPG ROCKETS AS OTHER PLAYERS SEE THEM (docs/RPG.md, docs/MULTIPLAYER_STATE.md): on the player body, each rocket fired (from where
    /// and which way: their copy of it flies the same way) and where it burst (their copy bursts there, showing the explosion and hurting no
    /// one: the shooter's machine dealt the blast, and it reaches their bodies through their copies, as every blow does). A copy that meets
    /// something before the word comes bursts there itself; the word then shows nothing more if it burst near.
    /// </summary>
    public sealed class RocketLauncher : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "rocket";

        /// <summary>The events (<see cref="AbilityEvent.Kind"/>): Launched (Origin where it left, Direction its way), Exploded (Origin where).</summary>
        public const int Launched = 0;

        public const int Exploded = 1;

        /// <summary>A copy that burst within this of where its owner's burst is taken for the same one, metres.</summary>
        public const float SameBurstMetres = 8.0f;

        /// <summary>How long a copy's own burst is remembered to match its owner's word, seconds.</summary>
        public const float RememberBurstSeconds = 2.0f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private readonly List<FlyingRocket> _copies = new List<FlyingRocket>();
        private readonly List<KeyValuePair<Vector3, float>> _burst = new List<KeyValuePair<Vector3, float>>();

        /// <summary>This body fires a rocket looking like <paramref name="look"/> (its launcher's loaded round): it flies, and other players are told.</summary>
        public FlyingRocket Launch(GameObject look, Vector3 from, Vector3 direction)
        {
            FlyingRocket rocket = FlyingRocket.Launch(look, from, direction, gameObject, false);
            if (rocket == null)
            {
                return null;
            }
            rocket.Exploded += at => Fired?.Invoke(new AbilityEvent(Exploded, at, Vector3.zero));
            Fired?.Invoke(new AbilityEvent(Launched, from, direction.normalized));
            return rocket;
        }

        /// <summary>Another player's rocket, on this copy of them: fired the same way, bursting where theirs did.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (!Finite(evt.Origin))
            {
                return;
            }
            switch (evt.Kind)
            {
                case Launched:
                    if (!Finite(evt.Direction) || evt.Direction.sqrMagnitude < 1e-4f)
                    {
                        return;
                    }
                    FlyingRocket copy = FlyingRocket.Launch(LoadedRound(), evt.Origin, evt.Direction, gameObject, true);
                    if (copy != null)
                    {
                        copy.Exploded += at => Remember(at);
                        _copies.Add(copy);
                    }
                    break;
                case Exploded:
                    _copies.RemoveAll(c => c == null || c.HasExploded);
                    _burst.RemoveAll(b => Time.time - b.Value > RememberBurstSeconds);
                    if (_burst.Exists(b => Vector3.Distance(b.Key, evt.Origin) < SameBurstMetres))
                    {
                        _burst.RemoveAll(b => Vector3.Distance(b.Key, evt.Origin) < SameBurstMetres);
                        return;   // this copy's rocket already burst there
                    }
                    FlyingRocket nearest = null;
                    float best = float.MaxValue;
                    foreach (FlyingRocket flying in _copies)
                    {
                        float d = Vector3.Distance(flying.transform.position, evt.Origin);
                        if (d < best)
                        {
                            best = d;
                            nearest = flying;
                        }
                    }
                    if (nearest != null)
                    {
                        _copies.Remove(nearest);
                        nearest.transform.position = evt.Origin;
                        nearest.Burst(evt.Origin, null);
                        _burst.RemoveAll(b => Vector3.Distance(b.Key, evt.Origin) < 0.01f);
                    }
                    else
                    {
                        Explosion.Show(evt.Origin);
                    }
                    break;
            }
        }

        private void Remember(Vector3 at) => _burst.Add(new KeyValuePair<Vector3, float>(at, Time.time));

        /// <summary>The loaded round of the launcher this body holds (on a copy, the mounted one): what its rockets look like here.</summary>
        private GameObject LoadedRound()
        {
            foreach (PistolWeapon gun in GetComponentsInChildren<PistolWeapon>(true))
            {
                if (gun.Profile != null && gun.Profile.FiresRocket && gun.MagazinePart != null)
                {
                    return gun.MagazinePart.gameObject;
                }
            }
            return null;
        }

        private static bool Finite(Vector3 v) => !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z);
    }
}
