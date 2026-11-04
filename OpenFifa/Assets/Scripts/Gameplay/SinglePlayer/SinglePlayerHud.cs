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
        private GUIStyle _title, _big, _label, _small, _button;
        private Texture2D _white, _disc, _ring;
        private GUIStyle _buttonLabel, _buttonShadow;
        private TeamAIDifficultyLevel _pick = TeamAIDifficultyLevel.Professional;
        private readonly MenuRect[] _levelRects = new MenuRect[TeamAIDifficultyLadder.LevelCount];

        // Cached HUD strings and the keys they were built from.
        private string _statsText = string.Empty;
        private string _ftScore = string.Empty, _ftVerdict = string.Empty, _ftStats = string.Empty;
        private int _statsKey = int.MinValue;
        private SinglePlayerMatchSession _ftSession;

        /// <summary>Force the touch overlay on (screenshots, desktop testing).</summary>
        public bool ForceTouchOverlay;

        private void Awake()
        {
            _c = GetComponent<SinglePlayerMatchController>();
            _pick = _c.Level;
            // The in-match broadcast HUD lives on a uGUI canvas; add it for scenes made before it existed.
            if (GetComponent<SinglePlayerHudCanvas>() == null) gameObject.AddComponent<SinglePlayerHudCanvas>();
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
            _buttonLabel = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _buttonLabel.normal.textColor = Color.white;
            _buttonShadow = new GUIStyle(_buttonLabel);
            _buttonShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            _disc = CircleTexture(128, 0f);
            _ring = CircleTexture(128, 0.82f);
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
            // Scorebug, name panels, radar and subtitles are on the uGUI canvas (SinglePlayerHudCanvas).
            DrawPowerBar(w, h);
            if (ShowTouchOverlay) DrawTouchOverlay(w, h);
            if (_c.Session.Paused) DrawPause(w, h);
            if (_c.Session.IsFinished) DrawFullTime(w, h);
        }

        /// <summary>True while the touch overlay is drawn (the HUD canvas lifts the name panels above it).</summary>
        public bool TouchOverlayVisible => _c != null && _c.Started && ShowTouchOverlay;

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

        /// <summary>Bottom inset kept clear for the iPad home indicator (HUD units).</summary>
        public const float BottomSafeInset = 20f;

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
            // Possession from the session, not the device reader: SinglePlayerInput.LastContext only updates when it is
            // the active input source (not for the scripted bot or before the first touch), which left the labels on
            // the defending set (Switch/Tackle/Press) while attacking.
            bool attack = _c.Session != null ? _c.Session.CurrentContext().InPossession : input != null && input.LastContext.InPossession;
            // Floating stick: no zone tint (it drew a visible seam across the pitch at the zone's right edge). Base
            // ring + knob while a finger is down; otherwise a faint resting stick where the thumb usually lands.
            var zone = layout.StickZone;
            float srad = layout.StickRadius * h;
            bool touching = input != null && input.Touch.StickActive;
            // Forced overlay (captures, desktop review): show the stick as if held, driven by the seat's actual intent.
            bool active = touching || ForceTouchOverlay;
            Vector2 baseC = touching
                ? new Vector2(input.Touch.StickOrigin.X * w, (1f - input.Touch.StickOrigin.Y) * h)
                : new Vector2(Mathf.Lerp(zone.XMin, zone.XMax, 0.35f) * w, h - BottomSafeInset - srad * 1.6f);
            Circle(_disc, baseC, srad, new Color(0.02f, 0.04f, 0.08f, active ? 0.45f : 0.3f));
            Circle(_ring, baseC, srad, new Color(1f, 1f, 1f, active ? 0.85f : 0.45f));
            Vector2 knob = baseC;
            if (active)
            {
                var mv = _c.Session != null ? _c.Session.LastControlIntent.Move : input.Last.Move;
                float mag = Mathf.Sqrt(mv.X * mv.X + mv.Y * mv.Y);
                if (mag > 1f) mv = new ControlVec2(mv.X / mag, mv.Y / mag);
                knob += new Vector2(mv.X, -mv.Y) * srad;
            }
            Circle(_disc, knob, srad * 0.45f, new Color(1f, 1f, 1f, active ? 0.9f : 0.5f));
            if (!active) Shadowed(new Rect(baseC.x - srad, baseC.y + srad + 2f, srad * 2f, 24f), "Move", _small.fontSize);

            foreach (var b in layout.Buttons)
            {
                float rad = b.Radius * h;
                var c = new Vector2(b.Center.X * w, (1f - b.Center.Y) * h);
                var r = new Rect(c.x - rad, c.y - rad, rad * 2, rad * 2);
                // Opaque-enough fill in a per-button colour plus a white rim: readable over grass, stands or sky.
                Circle(_disc, c, rad, ButtonColour(b.Slot, attack));
                Circle(_ring, c, rad, new Color(1f, 1f, 1f, 0.9f));
                Shadowed(r, TouchControlScheme.LabelFor(b.Slot, attack), _buttonLabel.fontSize);
            }
        }

        private static Color ButtonColour(TouchSlot slot, bool attack)
        {
            switch (slot)
            {
                case TouchSlot.Primary: return attack ? new Color(0.1f, 0.5f, 0.25f, 0.88f) : new Color(0.15f, 0.35f, 0.65f, 0.88f);
                case TouchSlot.Strike: return new Color(0.7f, 0.15f, 0.15f, 0.88f);
                case TouchSlot.Through: return attack ? new Color(0.45f, 0.2f, 0.65f, 0.88f) : new Color(0.75f, 0.42f, 0.05f, 0.88f);
                default: return new Color(0.2f, 0.24f, 0.32f, 0.88f);
            }
        }

        private static void Circle(Texture2D tex, Vector2 centre, float radius, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f), tex);
            GUI.color = old;
        }

        /// <summary>Bold white label with a 2 px dark drop shadow.</summary>
        private void Shadowed(Rect r, string text, int size)
        {
            _buttonShadow.fontSize = size;
            _buttonLabel.fontSize = size;
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, _buttonShadow);
            GUI.Label(r, text, _buttonLabel);
        }

        /// <summary>White anti-aliased disc (inner = 0) or ring (inner = inner radius fraction), generated at runtime.</summary>
        private static Texture2D CircleTexture(int size, float inner)
        {
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f, ro = size * 0.5f - 1f, ri = ro * inner;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(ro - d + 0.5f);
                    if (inner > 0f) a *= Mathf.Clamp01(d - ri + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
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
