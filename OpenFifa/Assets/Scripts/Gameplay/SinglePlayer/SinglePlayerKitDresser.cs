using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// Puts the 22 single-player mannequins in football kits: home / away outfield kits with clash resolution and
    /// distinct goalkeeper kits (<see cref="MatchKitBinder"/>), squad numbers and surnames from the roster the
    /// session uses, and a per-player look. One kit material instance per player (SRP Batcher friendly, no
    /// MaterialPropertyBlocks, no per-player keywords).
    /// When kits are unavailable (no library, kit shader missing or unsupported on this GPU) the model gets plain
    /// URP Lit materials instead, so the controller's colour tint still works and nothing renders magenta.
    /// </summary>
    public static class SinglePlayerKitDresser
    {
        private static Material _litFallback;

        /// <summary>Resolves the match kits for two roster clubs. False = kits unavailable (use the tint fallback).</summary>
        public static bool Prepare(RosterClub home, RosterClub away)
        {
            if (home == null || away == null) return false;
            try
            {
                return MatchKitBinder.SetMatchClubs(home, away);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[OpenFootball] kits unavailable, using tint: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Dresses player <paramref name="id"/> (0-10 home, 11-21 away; slot 0 of each side is the keeper).
        /// Returns false after switching the model to Lit materials when <paramref name="kitsReady"/> is false or the
        /// kit could not be applied; the caller then tints as before.
        /// </summary>
        public static bool Dress(GameObject model, SinglePlayerMatchSession session, RosterClub home, RosterClub away,
            int id, bool kitsReady)
        {
            if (model == null) return false;
            try
            {
                if (kitsReady && TryDress(model, session, home, away, id)) return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[OpenFootball] kit for player " + id + " failed, using tint: " + e.Message);
            }
            UseLitFallback(model);
            return false;
        }

        private static bool TryDress(GameObject model, SinglePlayerMatchSession session, RosterClub home, RosterClub away, int id)
        {
            bool isHome = id < MatchSetup.PlayersPerTeam;
            bool keeper = id == 0 || id == MatchSetup.PlayersPerTeam;
            var ids = session != null ? session.Options.PlayerIds : null;
            string playerId = ids != null && id < ids.Length ? ids[id] : null;
            var player = (isHome ? home : away)?.FindPlayer(playerId);
            string surname = player != null ? player.KnownAs : "";
            int number = player != null && player.ShirtNumber > 0 ? player.ShirtNumber : id % MatchSetup.PlayersPerTeam + 1;
            var jersey = MatchKitBinder.AttachPlayer(model, isHome, keeper, surname, number, playerId ?? id.ToString());
            if (jersey != null) return true;
            var leftover = model.GetComponent<JerseyRenderer>();
            if (leftover != null) Object.Destroy(leftover);
            return false;
        }

        /// <summary>
        /// Replaces kit-shader materials (the importer puts a neutral kit material on the mannequin) with a shared
        /// URP Lit material, so the per-renderer tint applies and an unsupported kit shader never shows.
        /// </summary>
        public static void UseLitFallback(GameObject model)
        {
            if (_litFallback == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) return;
                _litFallback = new Material(shader) { name = "PlayerTintFallback", hideFlags = HideFlags.DontSave };
            }
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].shader != null && mats[i].shader.name != KitShaderIds.ShaderName
                        && mats[i].shader.isSupported) continue;
                    mats[i] = _litFallback;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
