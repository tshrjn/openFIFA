using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// Minimal IMGUI front end for single player: difficulty picker (seven levels) with club selection, score and clock
    /// bug, commentary subtitles, power bar, pause and full-time screens, and the iPad touch overlay (shown only when no
    /// gamepad is connected). Placeholder UI until the real menu/HUD art lands.
    /// </summary>
    [RequireComponent(typeof(SinglePlayerMatchController))]
    public sealed class SinglePlayerHud : MonoBehaviour
    {
        private SinglePlayerMatchController _c;
        private GUIStyle _title, _big, _label, _small, _button, _sub;
        private Texture2D _white;
        private TeamAIDifficultyLevel _pick = TeamAIDifficultyLevel.Professional;

        /// <summary>Force the touch overlay on (screenshots, desktop testing).</summary>
        public bool ForceTouchOverlay;

        private void Awake()
        {
            _c = GetComponent<SinglePlayerMatchController>();
            _pick = _c.Level;
        }

        private float Scale => Mathf.Max(0.6f, Screen.height / 720f);

        private void EnsureStyles()
        {
            if (_title != null) return;
            _white = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = Color.white;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _big.normal.textColor = Color.white;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
            _small.normal.textColor = new Color(0.85f, 0.88f, 0.95f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            _sub = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _sub.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            EnsureStyles();
            float s = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s, h = Screen.height / s;

            if (!_c.Started)
            {
                DrawPicker(w, h);
                return;
            }
            DrawScoreBug();
            DrawSubtitle(w, h);
            DrawPowerBar(w, h);
            if (ShowTouchOverlay) DrawTouchOverlay(w, h);
            if (_c.Session.Paused) DrawPause(w, h);
            if (_c.Session.IsFinished) DrawFullTime(w, h);
        }

        private bool ShowTouchOverlay =>
            ForceTouchOverlay || (!SinglePlayerInput.GamepadConnected && UnityEngine.InputSystem.Touchscreen.current != null && !_c.AiVsAi);

        private void Panel(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        private void DrawPicker(float w, float h)
        {
            Panel(new Rect(0, 0, w, h), new Color(0.03f, 0.05f, 0.09f, 0.82f));
            GUI.Label(new Rect(0, h * 0.08f, w, 50), SinglePlayerMatchController.ProductName, _title);
            GUI.Label(new Rect(0, h * 0.08f + 46, w, 30), "Single Player  -  choose a difficulty", _label);

            var clubs = _c.Clubs;
            float y = h * 0.08f + 96;
            if (clubs != null && clubs.Count >= 2)
            {
                float cx = w * 0.5f;
                ClubSelector(new Rect(cx - 330, y, 300, 36), true, clubs);
                GUI.Label(new Rect(cx - 30, y, 60, 36), "v", _label);
                ClubSelector(new Rect(cx + 30, y, 300, 36), false, clubs);
                y += 54;
            }
            else
            {
                GUI.Label(new Rect(0, y, w, 30), "Default squads (no roster loaded)", _small);
                y += 40;
            }

            float bw = 150, bh = 64, gap = 10;
            float total = TeamAIDifficultyLadder.LevelCount * bw + (TeamAIDifficultyLadder.LevelCount - 1) * gap;
            float x0 = (w - total) * 0.5f;
            for (int i = 0; i < TeamAIDifficultyLadder.LevelCount; i++)
            {
                var level = (TeamAIDifficultyLevel)i;
                var r = new Rect(x0 + i * (bw + gap), y, bw, bh);
                if (level == _pick) Panel(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(1f, 0.8f, 0.1f));
                if (GUI.Button(r, TeamAIDifficultyLadder.DisplayName(level), _button)) _pick = level;
            }
            y += bh + 18;

            var prof = SinglePlayerDifficulty.For(_pick);
            GUI.Label(new Rect(0, y, w, 26),
                $"Opponent AI {prof.OpponentTheta:P0}   Teammate AI {prof.TeammateTheta:P0}   Pass assist {prof.PassAssist:P0}   Opponent keeper {Signed(prof.OpponentKeeperReflexDelta)}",
                _label);
            y += 44;
            if (GUI.Button(new Rect(w * 0.5f - 120, y, 240, 56), "Kick Off", _button)) _c.StartMatch(_pick);
            GUI.Label(new Rect(20, h - 34, w - 40, 26),
                SinglePlayerInput.GamepadConnected ? "Gamepad connected: FC Classic controls" : "No gamepad: touch controls (keyboard WASD / Space / F on desktop)", _small);
        }

        private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();

        private void ClubSelector(Rect r, bool home, System.Collections.Generic.IReadOnlyList<RosterClub> clubs)
        {
            int idx = home ? _c.HomeClubIndex : _c.AwayClubIndex;
            idx = Mathf.Clamp(idx, 0, clubs.Count - 1);
            if (GUI.Button(new Rect(r.x, r.y, 36, r.height), "<", _button)) idx = (idx + clubs.Count - 1) % clubs.Count;
            GUI.Label(new Rect(r.x + 36, r.y, r.width - 72, r.height), clubs[idx].Name, _label);
            if (GUI.Button(new Rect(r.xMax - 36, r.y, 36, r.height), ">", _button)) idx = (idx + 1) % clubs.Count;
            if (home) _c.HomeClubIndex = idx; else _c.AwayClubIndex = idx;
        }

        private void DrawScoreBug()
        {
            var st = _c.Session.Sim.State;
            var r = new Rect(16, 14, 360, 40);
            Panel(r, new Color(0.04f, 0.06f, 0.1f, 0.85f));
            Panel(new Rect(r.x, r.y, 6, r.height), new Color(1f, 0.8f, 0.1f));
            GUI.Label(new Rect(r.x + 10, r.y, 70, r.height), _c.HomeShort, _label);
            GUI.Label(new Rect(r.x + 80, r.y, 90, r.height), $"{st.HomeScore} - {st.AwayScore}", _big);
            GUI.Label(new Rect(r.x + 170, r.y, 70, r.height), _c.AwayShort, _label);
            GUI.Label(new Rect(r.x + 250, r.y, 100, r.height), _c.ClockText(), _label);
            string who = _c.AiVsAi ? "AI vs AI" : "You: " + (_c.HumanTeam == SimTeam.Home ? _c.HomeShort : _c.AwayShort);
            int id = _c.AiVsAi ? -1 : _c.Session.ControlledId;
            string name = id >= 0 && _c.Names != null ? "  |  " + _c.Names[id] : string.Empty;
            GUI.Label(new Rect(r.x, r.yMax + 2, 520, 22),
                $"{SinglePlayerMatchController.ProductName}  {TeamAIDifficultyLadder.DisplayName(_c.Level)}  |  {who}{name}", _small);
        }

        private void DrawSubtitle(float w, float h)
        {
            if (string.IsNullOrEmpty(_c.Subtitle) || Time.unscaledTime > _c.SubtitleUntil) return;
            var r = new Rect(w * 0.2f, h - 92, w * 0.6f, 40);
            Panel(r, new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(r, _c.Subtitle, _sub);
        }

        private void DrawPowerBar(float w, float h)
        {
            var input = _c.Input;
            if (input == null || _c.AiVsAi) return;
            float charge = input.Last.Charge;
            if (charge <= 0f) return;
            var r = new Rect(w * 0.5f - 100, h - 130, 200, 14);
            Panel(r, new Color(0f, 0f, 0f, 0.6f));
            Panel(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(charge), r.height - 4), Color.Lerp(Color.green, Color.red, charge));
        }

        private void DrawTouchOverlay(float w, float h)
        {
            var input = _c.Input;
            var layout = input != null ? input.Touch.Layout : TouchLayout.ForAspect(w / h);
            bool attack = input != null && input.LastContext.InPossession;
            // Stick zone and stick.
            var zone = layout.StickZone;
            Panel(new Rect(zone.XMin * w, (1f - zone.YMax) * h, (zone.XMax - zone.XMin) * w, (zone.YMax - zone.YMin) * h), new Color(1f, 1f, 1f, 0.04f));
            if (input != null && input.Touch.StickActive)
            {
                var o = input.Touch.StickOrigin;
                float rad = layout.StickRadius * h;
                Panel(new Rect(o.X * w - rad, (1f - o.Y) * h - rad, rad * 2, rad * 2), new Color(1f, 1f, 1f, 0.15f));
            }
            foreach (var b in layout.Buttons)
            {
                float rad = b.Radius * h;
                var r = new Rect(b.Center.X * w - rad, (1f - b.Center.Y) * h - rad, rad * 2, rad * 2);
                Panel(r, new Color(0.05f, 0.08f, 0.15f, 0.55f));
                GUI.Label(r, TouchControlScheme.LabelFor(b.Slot, attack), _label);
            }
        }

        private void DrawPause(float w, float h)
        {
            Panel(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(new Rect(0, h * 0.3f, w, 50), "Paused", _title);
            if (GUI.Button(new Rect(w * 0.5f - 110, h * 0.45f, 220, 50), "Resume", _button)) _c.Session.Resume();
        }

        private void DrawFullTime(float w, float h)
        {
            var r = _c.Session.Result;
            Panel(new Rect(w * 0.25f, h * 0.25f, w * 0.5f, h * 0.45f), new Color(0.03f, 0.05f, 0.09f, 0.92f));
            GUI.Label(new Rect(0, h * 0.28f, w, 50), "Full Time", _title);
            GUI.Label(new Rect(0, h * 0.36f, w, 40), $"{_c.HomeShort} {r.HomeScore} - {r.AwayScore} {_c.AwayShort}", _big);
            string verdict = _c.AiVsAi ? "" : (r.Outcome > 0 ? "You win" : r.Outcome < 0 ? "You lose" : "Draw");
            GUI.Label(new Rect(0, h * 0.43f, w, 30), verdict, _label);
            GUI.Label(new Rect(0, h * 0.48f, w, 30),
                $"Shots {r.HomeShots} - {r.AwayShots}    Possession {r.HomePossession:P0} - {1f - r.HomePossession:P0}    {TeamAIDifficultyLadder.DisplayName(r.Level)}", _label);
        }
    }
}
