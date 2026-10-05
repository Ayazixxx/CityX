using System;
using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// THE GUARD THAT KEEPS EVERY FUTURE ABILITY VISIBLE TO OTHER PLAYERS. Standing instruction from the project owner: "when making new abilities
    /// moving forward just make them like that the first time" -- an ability is not finished until other players on a server can see it. These
    /// fail the moment someone adds an ability that does not follow the contract, with a message that says what to do:
    ///
    ///  * a component named like an ability (…Caster, …Ability) that does not implement IReplicatedAbility;
    ///  * an IReplicatedAbility that is not on NetworkedPlayer.prefab (the body every joining player is spawned as -- PlayerAbilityWiring is the
    ///    one list that puts abilities on both that and the host's scene body);
    ///  * two abilities with the same id.
    ///
    /// See docs/ABILITIES_MULTIPLAYER.md.
    /// </summary>
    public sealed class AbilityReplicationGuardTests
    {
        private const string NetworkedPlayerPath = "Assets/CityX/Character/NetworkedPlayer.prefab";

        /// <summary>Every concrete component class in the game's own runtime assemblies (not tests, not editor tools, not the networking spike).</summary>
        private static IEnumerable<Type> GameComponentTypes()
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (!name.StartsWith("CityX.") || name.Contains("Tests") || name.Contains("Editor") || name.Contains("Spike"))
                {
                    continue;
                }
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }
                foreach (Type type in types)
                {
                    if (type.IsClass && !type.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(type))
                    {
                        yield return type;
                    }
                }
            }
        }

        private static List<Type> Abilities() => GameComponentTypes().Where(t => typeof(IReplicatedAbility).IsAssignableFrom(t)).ToList();

        [Test]
        public void TheGuardActuallyFindsTheAbilities()
        {
            // If reflection found nothing, every guard below would pass for the wrong reason.
            List<Type> abilities = Abilities();
            Assert.GreaterOrEqual(abilities.Count, 5, $"found only: {string.Join(", ", abilities.Select(t => t.Name))}");
        }

        [Test]
        public void EveryAbilityLookingComponentIsReplicated()
        {
            var missing = GameComponentTypes()
                .Where(t => (t.Name.EndsWith("Caster") || t.Name.EndsWith("Ability")) && !typeof(IReplicatedAbility).IsAssignableFrom(t))
                .Select(t => t.FullName).ToList();
            Assert.IsEmpty(missing,
                "these look like abilities but do not implement IReplicatedAbility, so other players on a server would never see them: " +
                string.Join(", ", missing) + ". Implement it (Fired + PlayRemote) -- see docs/ABILITIES_MULTIPLAYER.md.");
        }

        [Test]
        public void AbilityIdsAreUnique()
        {
            var seen = new Dictionary<int, string>();
            var holder = new GameObject("IdProbe");
            try
            {
                foreach (Type type in Abilities())
                {
                    var ability = (IReplicatedAbility)holder.AddComponent(type);
                    string id = ability.AbilityId;
                    Assert.IsFalse(string.IsNullOrEmpty(id), $"{type.Name} has no ability id");
                    int hash = AbilityIds.Hash(id);
                    Assert.IsFalse(seen.TryGetValue(hash, out string other), $"{type.Name} and {other} share the ability id '{id}'");
                    seen[hash] = type.Name;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

#if UNITY_EDITOR
        [Test]
        public void EveryAbilityIsOnTheBodyEveryJoiningPlayerIsSpawnedAs()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPath);
            Assert.IsNotNull(prefab, "no NetworkedPlayer.prefab -- run CityX/Rebuild Massing Scene");
            Assert.IsNotNull(prefab.GetComponent<NetworkedAbilitySync>(), "the joining player's body has no NetworkedAbilitySync, so its abilities are never sent");

            var missing = Abilities().Where(t => prefab.GetComponent(t) == null).Select(t => t.Name).ToList();
            Assert.IsEmpty(missing,
                "these abilities are not on NetworkedPlayer.prefab, so a player who joins cannot use them (and the host's would never be matched): " +
                string.Join(", ", missing) + ". Add them in PlayerAbilityWiring.AddAbilities (the one list for both bodies) and rebuild.");
        }
#endif
    }
}
