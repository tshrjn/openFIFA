using NUnit.Framework;
using OpenFifa.Core;
using OpenFifa.Gameplay;
using UnityEditor;
using UnityEngine;

namespace OpenFifa.Tests.Editor
{
    /// <summary>The single-player mannequin dressed through MatchKitBinder gets one kit material on every slot.</summary>
    [TestFixture]
    [Category("Kits")]
    public class MannequinKitTests
    {
        private const string ModelPath = "Assets/Animations/Quaternius/UAL2/UAL2_Standard.fbx";
        private GameObject _model;

        [TearDown]
        public void TearDown()
        {
            if (_model != null) Object.DestroyImmediate(_model);
            MatchKitBinder.Reset();
        }

        private static RosterClub Club(string id, string primary, string secondary, string away) =>
            new RosterClub(id, id, id.Substring(0, 3).ToUpperInvariant(), primary, secondary, away, "", 0, "4-3-3", 3, new RosterPlayer[0]);

        [Test]
        public void AttachPlayer_Mannequin_SkinnedBodyKitOnEverySlot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null) Assert.Ignore("Mannequin model not in project.");
            _model = Object.Instantiate(prefab);

            Assert.IsTrue(MatchKitBinder.SetMatchClubs(Club("kingsmere", "#B3122E", "#FFFFFF", "#1C1C24"),
                Club("ashbury", "#1B3F8F", "#F2C230", "#FFFFFF")), "kit selection");
            var jersey = MatchKitBinder.AttachPlayer(_model, true, false, "Sabeic", 9, "kingsmere-09");
            Assert.IsNotNull(jersey, "dressed");
            Assert.AreEqual(KitMappingMode.SkinnedBody, jersey.MappingMode);
            foreach (var smr in _model.GetComponentsInChildren<SkinnedMeshRenderer>())
                foreach (var m in smr.sharedMaterials)
                    Assert.AreSame(jersey.KitMaterial, m, smr.name);

            var keeper = MatchKitBinder.AttachPlayer(Object.Instantiate(prefab), false, true, "Orrbourne", 1, "ashbury-01");
            Assert.IsNotNull(keeper);
            Assert.AreEqual(KitVariant.Goalkeeper, keeper.CurrentKit.Variant);
            Object.DestroyImmediate(keeper.gameObject);
        }
    }
}
