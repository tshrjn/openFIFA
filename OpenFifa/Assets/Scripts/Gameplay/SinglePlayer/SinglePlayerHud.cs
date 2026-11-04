using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// Minimal IMGUI front end for single player: difficulty picker (seven levels) with club selection, score and clock
    /// bug, commentary subtitles, power bar, pause and full-time screens, and the iPad touch overlay (shown only when no
    /// gamepad is connected). Placeholder UI until the real menu/HUD art lands.
    ///
    /// Steady-state OnGUI allocates nothing: every composed string (score, clock, status line, picker stats, full-time
    /// summary) is cached and rebuilt only when its inputs change (score, clock second, controlled player, pick).
    /// </summary>
    [RequireComponent(typeof(SinglePlayerMatchController))]
    public sealed class SinglePlayerHud : MonoBehaviour
    {
        private SinglePlayerMatchController _c;
        private GUIStyle _title, _big, _label, _small, _button, _sub;
        private Texture2D _white;
        private TeamAIDifficultyLevel _pick = TeamAIDifficultyLevel.Professional;
        private readonly MenuRect[] _levelRects = new MenuRect[TeamAIDifficultyLadder.LevelCount];

        // Cached HUD strings and the keys they were built from.
        private string _scoreText = string.Empty, _clockText = string.Empty, _statusText = string.Empty, _statsText = string.Empty;
        private string _ftScore = string.Empty, _ftVerdict = string.Empty, _ftStats = string.Empty;
        private long _scoreKey = long.MinValue, _statusKey = long.MinValue;
        private int _clockKey = int.MinValue, _statsKey = int.MinValue;
        private SinglePlayerMatchSession _ftSession, _statusSession;

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
                _ftSession = null;
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

            // Fits the width (one row down to ~950 units, e.g. 4:3 iPads at 961-1031), else wraps to 4 + 3.
            const float bh = 64f;
            int rows = MenuGridLayout.Layout(TeamAIDifficultyLadder.LevelCount, w, y, _levelRects, bh);
            for (int i = 0; i < TeamAIDifficultyLadder.LevelCount; i++)
            {
                var level = (TeamAIDifficultyLevel)i;
                var m = _levelRects[i];
                var r = new Rect(m.X, m.Y, m.Width, m.Height);
                if (level == _pick) Panel(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(1f, 0.8f, 0.1f));
                if (GUI.Button(r, TeamAIDifficultyLadder.DisplayName(level), _button)) _pick = level;
            }
            y += MenuGridLayout.BlockHeight(rows, bh) + 18;

            if (_statsKey != (int)_pick)
            {
                _statsKey = (int)_pick;
                var prof = SinglePlayerDifficulty.For(_pick);
                _statsText = $"Opponent AI {prof.OpponentTheta:P0}   Teammate AI {prof.TeammateTheta:P0}   Pass assist {prof.PassAssist:P0}   Opponent keeper {Signed(prof.OpponentKeeperReflexDelta)}";
            }
            GUI.Label(new Rect(0, y, w, 26), _statsText, _label);
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
            long scoreKey = ((long)st.HomeScore << 32) | (uint)st.AwayScore;
            if (scoreKey != _scoreKey)
            {
                _scoreKey = scoreKey;
                _scoreText = $"{st.HomeScore} - {st.AwayScore}";
            }
            int clock = _c.ClockSeconds();
            if (clock != _clockKey)
            {
                _clockKey = clock;
                _clockText = $"{clock / 60:00}:{clock % 60:00}";
            }
            GUI.Label(new Rect(r.x + 80, r.y, 90, r.height), _scoreText, _big);
            GUI.Label(new Rect(r.x + 170, r.y, 70, r.height), _c.AwayShort, _label);
            GUI.Label(new Rect(r.x + 250, r.y, 100, r.height), _clockText, _label);

            int id = _c.AiVsAi ? -1 : _c.Session.ControlledId;
            long statusKey = ((long)(id + 1) << 16) | ((long)_c.Level << 8) | ((long)_c.HumanTeam << 1) | (_c.AiVsAi ? 1L : 0L);
            if (statusKey != _statusKey || !ReferenceEquals(_statusSession, _c.Session))
            {
                _statusKey = statusKey;
                _statusSession = _c.Session;
                string who = _c.AiVsAi ? "AI vs AI" : "You: " + (_c.HumanTeam == SimTeam.Home ? _c.HomeShort : _c.AwayShort);
                string name = id >= 0 && _c.Names != null ? "  |  " + _c.Names[id] : string.Empty;
                _statusText = $"{SinglePlayerMatchController.ProductName}  {TeamAIDifficultyLadder.DisplayName(_c.Level)}  |  {who}{name}";
            }
            GUI.Label(new Rect(r.x, r.yMax + 2, 520, 22), _statusText, _small);
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

        /// <summary>Result plus the way out: Rematch (same clubs and level, new seed), change teams/difficulty, quit to menu.</summary>
        private void DrawFullTime(float w, float h)
        {
            if (!ReferenceEquals(_ftSession, _c.Session))
            {
                _ftSession = _c.Session;
                var r = _c.Session.Result;
                _ftScore = $"{_c.HomeShort} {r.HomeScore} - {r.AwayScore} {_c.AwayShort}";
                _ftVerdict = _c.AiVsAi ? "" : (r.Outcome > 0 ? "You win" : r.Outcome < 0 ? "You lose" : "Draw");
                _ftStats = $"Shots {r.HomeShots} - {r.AwayShots}    Possession {r.HomePossession:P0} - {1f - r.HomePossession:P0}    {TeamAIDifficultyLadder.DisplayName(r.Level)}";
            }
            float pw = Mathf.Min(w - 32f, Mathf.Max(w * 0.5f, 640f));
            float px = (w - pw) * 0.5f;
            Panel(new Rect(px, h * 0.22f, pw, h * 0.56f), new Color(0.03f, 0.05f, 0.09f, 0.92f));
            GUI.Label(new Rect(0, h * 0.25f, w, 50), "Full Time", _title);
            GUI.Label(new Rect(0, h * 0.33f, w, 40), _ftScore, _big);
            GUI.Label(new Rect(0, h * 0.40f, w, 30), _ftVerdict, _label);
            GUI.Label(new Rect(px, h * 0.45f, pw, 30), _ftStats, _label);

            float bw = Mathf.Min(200f, (pw - 40f) / 3f), bh = 50f, gap = 10f;
            float x0 = (w - (3f * bw + 2f * gap)) * 0.5f, y = h * 0.56f;
            if (GUI.Button(new Rect(x0, y, bw, bh), "Rematch", _button))
            {
                _c.Rematch();
                return;
            }
            if (GUI.Button(new Rect(x0 + bw + gap, y, bw, bh), "Teams / Difficulty", _button))
            {
                _pick = _c.Level;
                _c.EndMatch();
                return;
            }
            if (GUI.Button(new Rect(x0 + 2f * (bw + gap), y, bw, bh), "Quit to Menu", _button))
            {
                _c.QuitToMenu();
                _pick = _c.Level;
            }
        }
    }
}
