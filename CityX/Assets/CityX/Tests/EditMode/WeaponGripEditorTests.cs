using CityX.Character;
using CityX.Weapons;
using CityX.WeaponsEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The editor side of adjusting a weapon's hold. The buttons themselves need a live Inspector window, so
    /// what is checked here is what can go wrong without one: the custom Inspectors are actually attached to
    /// the weapon and its marker, and the settings asset exists where the game loads it from.
    /// </summary>
    public sealed class WeaponGripEditorTests
    {
        private GameObject _object;

        [TearDown]
        public void TearDown()
        {
            if (_object != null) Object.DestroyImmediate(_object);
        }

        private Editor EditorFor(System.Type weapon)
        {
            _object = new GameObject("held");
            _object.AddComponent<SphereCollider>();   // SwordWeapon and PistolWeapon require one
            var component = (Component)_object.AddComponent(weapon);
            return Editor.CreateEditor(component);
        }

        [Test]
        public void TheSwordHasItsOwnInspector()
        {
            Editor editor = EditorFor(typeof(SwordWeapon));
            try { Assert.IsInstanceOf<SwordWeaponEditor>(editor); }
            finally { Object.DestroyImmediate(editor); }
        }

        [Test]
        public void ThePistolHasItsOwnInspector()
        {
            Editor editor = EditorFor(typeof(PistolWeapon));
            try { Assert.IsInstanceOf<PistolWeaponEditor>(editor); }
            finally { Object.DestroyImmediate(editor); }
        }

        [Test]
        public void TheMarkerHasItsOwnInspectorSoItCanSaveToo()
        {
            _object = new GameObject("marker");
            var marker = _object.AddComponent<GripMarker>();
            Editor editor = Editor.CreateEditor(marker);
            try { Assert.IsInstanceOf<GripMarkerEditor>(editor); }
            finally { Object.DestroyImmediate(editor); }
        }

        [Test]
        public void TheSettingsAssetIsWhereTheGameLoadsItFromAndStartsUntuned()
        {
            var asset = AssetDatabase.LoadAssetAtPath<WeaponGripSettings>(WeaponGripSettings.AssetPath);
            Assert.IsNotNull(asset, "the WeaponGrips asset is missing -- run CityX > Weapon Grips > Open the grip settings");
            Assert.IsNotNull(Resources.Load<WeaponGripSettings>(WeaponGripSettings.ResourceName),
                "the asset is not in a Resources folder the game can load it from");
            Assert.That(asset.TorsoFollowStrength, Is.InRange(0.0f, 1.0f));
        }
    }
}
