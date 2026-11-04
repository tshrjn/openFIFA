using System;
using OpenFifa.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// In-match broadcast HUD on a uGUI ScreenSpaceOverlay canvas (so it can be routed to another display, unlike
    /// IMGUI): FC-style scorebug (top-left), player name panels with stamina (bottom corners), radar (bottom centre) and
    /// commentary subtitles (above the radar). Built once at the first kick-off under a root named
    /// <see cref="CanvasName"/>; the IMGUI <see cref="SinglePlayerHud"/> keeps the menus, power bar and touch overlay.
    ///
    /// Units are 720 high (CanvasScaler, scale with screen size, match height), the same layout units the IMGUI HUD
    /// uses. Everything sits under a safe-area root (<see cref="SafeAreaRoot"/>). Steady state allocates nothing:
    /// texts are only assigned when their content changes; everything else is positions, alphas and anchors.
    /// </summary>
    [RequireComponent(typeof(SinglePlayerMatchController))]
    [DefaultExecutionOrder(200)] // after the controller and the animation drivers moved things this frame
    public sealed class SinglePlayerHudCanvas : MonoBehaviour
    {
        public const string CanvasName = "OpenFootball HUD Canvas";
        public const float RefHeight = 720f;

        /// <summary>Raised once, right after the canvas hierarchy is built (at the first kick-off).</summary>
        public static event Action<SinglePlayerHudCanvas> HudBuilt;

        public Canvas Canvas { get; private set; }
        public SafeAreaRoot SafeArea { get; private set; }

        // Layout (canvas units).
        public const float ScorebugHeight = 32f;
        public const float PanelWidth = 230f, PanelHeight = 46f, SideInset = 16f, BottomInset = 20f;
        public const float RadarWidth = 190f, RadarHeight = 105f, RadarOpacity = 0.6f;
        public const float SubtitleHeight = 34f;

        private SinglePlayerMatchController _c;
        private SinglePlayerHud _imgui;
        private Font _font;
        private Sprite _dot;

        // Scorebug.
        private Text _homeAbbr, _awayAbbr, _score, _clock;
        private Image _homeChip, _awayChip;
        private int _scoreKey = int.MinValue, _clockKey = int.MinValue;
        private string _homeShown, _awayShown;

        // Name panels: 0 = home (left), 1 = away (right).
        private readonly RectTransform[] _panel = new RectTransform[2];
        private readonly CanvasGroup[] _panelGroup = new CanvasGroup[2];
        private readonly Image[] _panelBar = new Image[2];
        private readonly Text[] _panelNum = new Text[2], _panelName = new Text[2];
        private readonly RectTransform[] _staminaFill = new RectTransform[2];
        private readonly Image[] _staminaImg = new Image[2];
        private readonly int[] _panelId = { -2, -2 };
        private readonly float[] _panelAlpha = new float[2];
        private readonly float[] _staminaShown = { -1f, -1f };

        // Radar.
        private RectTransform _radar;
        private readonly RectTransform[] _radarDot = new RectTransform[MatchSimState.PlayerCount];
        private readonly Image[] _radarDotImg = new Image[MatchSimState.PlayerCount];
        private RectTransform _radarBall, _radarHalo;

        // Subtitle.
        private RectTransform _subtitle;
        private Text _subtitleText;
        private CanvasGroup _subtitleGroup;
        private string _subtitleShown;

        private SinglePlayerMatchSession _session;
        private Color _homeCol, _awayCol;

        private void Awake()
        {
            _c = GetComponent<SinglePlayerMatchController>();
            _imgui = GetComponent<SinglePlayerHud>();
        }

        private void OnDestroy()
        {
            if (Canvas != null) Destroy(Canvas.gameObject);
        }

        private void LateUpdate()
        {
            if (!_c.Started)
            {
                if (Canvas != null && Canvas.gameObject.activeSelf) Canvas.gameObject.SetActive(false);
                return;
            }
            if (Canvas == null) Build();
            if (!Canvas.gameObject.activeSelf) Canvas.gameObject.SetActive(true);
            if (!ReferenceEquals(_session, _c.Session))
            {
                _session = _c.Session;
                _panelId[0] = _panelId[1] = -2;
                _panelAlpha[0] = _panelAlpha[1] = 0f;
                _scoreKey = _clockKey = int.MinValue;
            }
            UpdateColours();
            UpdateScorebug();
            UpdatePanels();
            UpdateRadar();
            UpdateSubtitle();
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _dot = Sprite.Create(DiscTexture(64), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);

            var go = new GameObject(CanvasName, typeof(RectTransform));
            Canvas = go.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // height-driven, like the IMGUI HUD
            DontDestroyOnLoad(go);

            var safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(go.transform, false);
            SafeArea = safe.AddComponent<SafeAreaRoot>();
            var root = (RectTransform)safe.transform;

            BuildScorebug(root);
            BuildPanels(root);
            BuildRadar(root);
            BuildSubtitle(root);
            HudBuilt?.Invoke(this);
        }

        private void BuildScorebug(RectTransform root)
        {
            const float chip = 6f, team = 56f, score = 64f, clockW = 66f;
            float w = chip + team + score + team + chip + clockW;
            var bug = MakeRect(root, "Scorebug", new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(w, ScorebugHeight));
            Img(bug, "Back", new Vector2(0f, 0f), new Vector2(w - clockW, ScorebugHeight), new Color(0.04f, 0.06f, 0.1f, 0.9f));
            _homeChip = Img(bug, "HomeChip", new Vector2(0f, 0f), new Vector2(chip, ScorebugHeight), Color.red);
            _homeAbbr = Txt(bug, "Home", new Vector2(chip, 0f), new Vector2(team, ScorebugHeight), 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Img(bug, "ScoreBox", new Vector2(chip + team, 0f), new Vector2(score, ScorebugHeight), new Color(0.95f, 0.95f, 0.96f, 0.97f));
            _score = Txt(bug, "Score", new Vector2(chip + team, 0f), new Vector2(score, ScorebugHeight), 18, FontStyle.Bold, new Color(0.05f, 0.07f, 0.12f), TextAnchor.MiddleCenter);
            _awayAbbr = Txt(bug, "Away", new Vector2(chip + team + score, 0f), new Vector2(team, ScorebugHeight), 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            _awayChip = Img(bug, "AwayChip", new Vector2(chip + 2f * team + score, 0f), new Vector2(chip, ScorebugHeight), Color.blue);
            Img(bug, "ClockBox", new Vector2(w - clockW, 0f), new Vector2(clockW, ScorebugHeight), new Color(0.1f, 0.13f, 0.2f, 0.9f));
            _clock = Txt(bug, "Clock", new Vector2(w - clockW, 0f), new Vector2(clockW, ScorebugHeight), 16, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
        }

        private void BuildPanels(RectTransform root)
        {
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                var pivot = new Vector2(left ? 0f : 1f, 0f);
                var p = MakeRect(root, left ? "HomePlayerPanel" : "AwayPlayerPanel", pivot,
                    new Vector2(left ? SideInset : -SideInset, BottomInset + 8f), new Vector2(PanelWidth, PanelHeight));
                _panel[side] = p;
                _panelGroup[side] = p.gameObject.AddComponent<CanvasGroup>();
                _panelGroup[side].alpha = 0f;
                Img(p, "Back", Vector2.zero, new Vector2(PanelWidth, PanelHeight), new Color(0.03f, 0.05f, 0.09f, 0.82f));
                _panelBar[side] = Img(p, "TeamBar", new Vector2(left ? 0f : PanelWidth - 6f, 0f), new Vector2(6f, PanelHeight), Color.white);
                _panelNum[side] = Txt(p, "Number", new Vector2(left ? 6f : PanelWidth - 46f, 8f), new Vector2(40f, PanelHeight - 8f),
                    22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                _panelName[side] = Txt(p, "Name", new Vector2(left ? 50f : 8f, 8f), new Vector2(PanelWidth - 58f, PanelHeight - 8f),
                    18, FontStyle.Bold, Color.white, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
                var track = Img(p, "StaminaTrack", new Vector2(left ? 50f : 8f, 5f), new Vector2(PanelWidth - 58f, 4f), new Color(1f, 1f, 1f, 0.18f));
                var fill = Img(track.rectTransform, "StaminaFill", Vector2.zero, Vector2.zero, Color.green);
                var fr = fill.rectTransform;
                fr.anchorMin = new Vector2(left ? 0f : 1f, 0f);
                fr.anchorMax = new Vector2(left ? 1f : 1f, 1f);
                fr.pivot = new Vector2(left ? 0f : 1f, 0.5f);
                fr.offsetMin = fr.offsetMax = Vector2.zero;
                _staminaFill[side] = fr;
                _staminaImg[side] = fill;
            }
        }

        private void BuildRadar(RectTransform root)
        {
            _radar = MakeRect(root, "Radar", new Vector2(0.5f, 0f), new Vector2(0f, BottomInset), new Vector2(RadarWidth, RadarHeight));
            var back = Img(_radar, "Back", Vector2.zero, new Vector2(RadarWidth, RadarHeight), new Color(0.02f, 0.12f, 0.05f, RadarOpacity));
            back.raycastTarget = false;
            var line = new Color(1f, 1f, 1f, 0.35f);
            Img(_radar, "Halfway", new Vector2(RadarWidth * 0.5f - 0.5f, 0f), new Vector2(1f, RadarHeight), line);
            Frame(_radar, "Border", Vector2.zero, new Vector2(RadarWidth, RadarHeight), line);
            var pitch = PitchSpec.Standard;
            float bh = RadarHeight * pitch.PenaltyAreaWidth / pitch.Width, bw = RadarWidth * pitch.PenaltyAreaDepth / pitch.Length;
            Frame(_radar, "BoxLeft", new Vector2(0f, (RadarHeight - bh) * 0.5f), new Vector2(bw, bh), line);
            Frame(_radar, "BoxRight", new Vector2(RadarWidth - bw, (RadarHeight - bh) * 0.5f), new Vector2(bw, bh), line);

            _radarHalo = Dot(_radar, "Controlled", 12f, new Color(1f, 0.85f, 0.1f, 0.95f));
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                var outline = Dot(_radar, "P" + i, 8f, new Color(0f, 0f, 0f, 0.6f));
                var inner = Dot(outline, "Fill", 6.4f, Color.white);
                inner.anchorMin = inner.anchorMax = new Vector2(0.5f, 0.5f);
                inner.anchoredPosition = Vector2.zero;
                _radarDot[i] = outline;
                _radarDotImg[i] = inner.GetComponent<Image>();
            }
            _radarBall = Dot(_radar, "Ball", 5.6f, Color.white);
        }

        private void BuildSubtitle(RectTransform root)
        {
            _subtitle = MakeRect(root, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0f, BottomInset + RadarHeight + 6f), new Vector2(480f, SubtitleHeight));
            _subtitleGroup = _subtitle.gameObject.AddComponent<CanvasGroup>();
            _subtitleGroup.alpha = 0f;
            var back = Img(_subtitle, "Back", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
            Stretch(back.rectTransform);
            _subtitleText = Txt(_subtitle, "Text", Vector2.zero, Vector2.zero, 19, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
            Stretch(_subtitleText.rectTransform);
            _subtitleText.rectTransform.offsetMin = new Vector2(10f, 0f);
            _subtitleText.rectTransform.offsetMax = new Vector2(-10f, 0f);
            _subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _subtitleText.resizeTextForBestFit = true;
            _subtitleText.resizeTextMinSize = 13;
            _subtitleText.resizeTextMaxSize = 19;
        }

        // ------------------------------------------------------------------ update

        private void UpdateColours()
        {
            if (_homeCol == _c.HomeColour && _awayCol == _c.AwayColour) return;
            _homeCol = _c.HomeColour;
            _awayCol = _c.AwayColour;
            _homeChip.color = Opaque(_homeCol);
            _awayChip.color = Opaque(_awayCol);
            _panelBar[0].color = Opaque(_homeCol);
            _panelBar[1].color = Opaque(_awayCol);
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
                _radarDotImg[i].color = Opaque(i < MatchSetup.PlayersPerTeam ? _homeCol : _awayCol);
        }

        private void UpdateScorebug()
        {
            var st = _c.Session.Sim.State;
            if (!ReferenceEquals(_homeShown, _c.HomeShort)) { _homeShown = _c.HomeShort; _homeAbbr.text = _homeShown; }
            if (!ReferenceEquals(_awayShown, _c.AwayShort)) { _awayShown = _c.AwayShort; _awayAbbr.text = _awayShown; }
            int scoreKey = st.HomeScore * 1000 + st.AwayScore;
            if (scoreKey != _scoreKey)
            {
                _scoreKey = scoreKey;
                _score.text = $"{st.HomeScore} - {st.AwayScore}";
            }
            int clock = _c.ClockSeconds();
            if (clock != _clockKey)
            {
                _clockKey = clock;
                _clock.text = $"{clock / 60:00}:{clock % 60:00}";
            }
        }

        /// <summary>
        /// Home panel bottom-left, away bottom-right. A human side shows the controlled player; an AI side its ball
        /// carrier, else its player nearest the ball (the presser when the user has it). Fades out at stoppages. With
        /// the touch overlay on, the panels sit above the stick and the button cluster.
        /// </summary>
        private void UpdatePanels()
        {
            var session = _c.Session;
            var st = session.Sim.State;
            bool live = st.Phase == SimMatchPhase.InPlay && !session.Paused && !session.IsFinished;
            float dt = Time.unscaledDeltaTime;
            float canvasH = RefHeight, canvasW = SafeArea.Root.rect.width;

            float leftY = BottomInset + 8f, rightY = leftY;
            if (_imgui != null && _imgui.TouchOverlayVisible)
            {
                var layout = _c.Input != null ? _c.Input.Touch.Layout : TouchLayout.ForAspect(canvasW / canvasH);
                float top = 0f; // highest button edge, from the bottom (canvas units)
                foreach (var b in layout.Buttons) top = Mathf.Max(top, (b.Center.Y + b.Radius) * canvasH);
                rightY = top + 10f;
                leftY = BottomInset + layout.StickRadius * canvasH * 2.6f + 34f;
            }

            for (int side = 0; side < 2; side++)
            {
                var team = side == 0 ? SimTeam.Home : SimTeam.Away;
                int id = PanelPlayer(session, team);
                if (id != _panelId[side])
                {
                    _panelId[side] = id;
                    if (id >= 0)
                    {
                        _panelNum[side].text = _c.ShirtNumbers != null && id < _c.ShirtNumbers.Length ? _c.ShirtNumbers[id].ToString() : string.Empty;
                        string n = _c.Names != null && id < _c.Names.Length ? _c.Names[id] : null;
                        _panelName[side].text = string.IsNullOrEmpty(n) ? string.Empty : n.ToUpperInvariant();
                    }
                }
                float target = live && id >= 0 ? 1f : 0f;
                _panelAlpha[side] = Mathf.MoveTowards(_panelAlpha[side], target, dt * 4f);
                if (_panelGroup[side].alpha != _panelAlpha[side]) _panelGroup[side].alpha = _panelAlpha[side];
                var pos = new Vector2(side == 0 ? SideInset : -SideInset, side == 0 ? leftY : rightY);
                if (_panel[side].anchoredPosition != pos) _panel[side].anchoredPosition = pos;
                if (id < 0) continue;
                float stamina = Mathf.Round(Mathf.Clamp01(st.Players[id].Stamina) * 100f) / 100f;
                if (stamina != _staminaShown[side])
                {
                    _staminaShown[side] = stamina;
                    var fr = _staminaFill[side];
                    if (side == 0) fr.anchorMax = new Vector2(stamina, 1f);
                    else fr.anchorMin = new Vector2(1f - stamina, 0f);
                    _staminaImg[side].color = new Color(Mathf.Lerp(0.95f, 0.3f, stamina), Mathf.Lerp(0.3f, 0.85f, stamina), 0.25f, 1f);
                }
            }
        }

        private int PanelPlayer(SinglePlayerMatchSession session, SimTeam team)
        {
            var st = session.Sim.State;
            int first = team == SimTeam.Home ? 0 : MatchSetup.PlayersPerTeam;
            if (!_c.AiVsAi)
                for (int k = 0; k < session.HumanSlots.Count; k++)
                    if (session.HumanSlots[k].Team == team) return session.HumanSlots[k].ControlledId;
            if (st.Owner >= first && st.Owner < first + MatchSetup.PlayersPerTeam) return st.Owner;
            var ball = st.Ball.Position;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = first; i < first + MatchSetup.PlayersPerTeam; i++)
            {
                if (st.Players[i].SentOff) continue;
                var p = st.Players[i].Position;
                float d = (p.X - ball.X) * (p.X - ball.X) + (p.Z - ball.Z) * (p.Z - ball.Z);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>Broadcast orientation: +Z (screen-right in the broadcast camera) right, the far touchline (-X) at the top.</summary>
        private void UpdateRadar()
        {
            var st = _c.Session.Sim.State;
            var pitch = _c.Session.Sim.Config.Pitch;
            int controlled = _c.AiVsAi ? -1 : _c.Session.ControlledId;
            for (int i = 0; i < MatchSimState.PlayerCount; i++)
            {
                bool on = !st.Players[i].SentOff;
                if (_radarDot[i].gameObject.activeSelf != on) _radarDot[i].gameObject.SetActive(on);
                if (on) _radarDot[i].anchoredPosition = RadarPoint(st.Players[i].Position, pitch);
            }
            bool halo = controlled >= 0 && !st.Players[controlled].SentOff;
            if (_radarHalo.gameObject.activeSelf != halo) _radarHalo.gameObject.SetActive(halo);
            if (halo) _radarHalo.anchoredPosition = RadarPoint(st.Players[controlled].Position, pitch);
            _radarBall.anchoredPosition = RadarPoint(st.Ball.Position, pitch);
        }

        private static Vector2 RadarPoint(SimVec3 pos, PitchSpec pitch)
        {
            float u = Mathf.Clamp01(pos.Z / pitch.Length + 0.5f);
            float v = Mathf.Clamp01(0.5f - pos.X / pitch.Width); // +X (camera side) at the bottom
            return new Vector2(u * RadarWidth, v * RadarHeight);
        }

        private void UpdateSubtitle()
        {
            string sub = _c.Subtitle;
            bool show = !string.IsNullOrEmpty(sub) && Time.unscaledTime <= _c.SubtitleUntil;
            if (show && !ReferenceEquals(sub, _subtitleShown))
            {
                _subtitleShown = sub;
                _subtitleText.text = sub;
            }
            float a = show ? 1f : 0f;
            if (_subtitleGroup.alpha != a) _subtitleGroup.alpha = a;
        }

        // ------------------------------------------------------------------ helpers

        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        /// <summary>RectTransform anchored and pivoted at <paramref name="anchor"/> (0-1 in the parent).</summary>
        private static RectTransform MakeRect(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Child laid out from the parent's bottom-left corner (pos, size in canvas units).</summary>
        private static RectTransform Child(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var rt = MakeRect(parent, name, Vector2.zero, pos, size);
            return rt;
        }

        private static Image Img(RectTransform parent, string name, Vector2 pos, Vector2 size, Color c)
        {
            var img = Child(parent, name, pos, size).gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private Text Txt(RectTransform parent, string name, Vector2 pos, Vector2 size, int fontSize, FontStyle style, Color c, TextAnchor align)
        {
            var t = Child(parent, name, pos, size).gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = c;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        private RectTransform Dot(RectTransform parent, string name, float size, Color c)
        {
            var rt = MakeRect(parent, name, Vector2.zero, Vector2.zero, new Vector2(size, size));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = _dot;
            img.color = c;
            img.raycastTarget = false;
            return rt;
        }

        private static void Frame(RectTransform parent, string name, Vector2 pos, Vector2 size, Color c)
        {
            var f = Child(parent, name, pos, size);
            Img(f, "T", new Vector2(0f, size.y - 1f), new Vector2(size.x, 1f), c);
            Img(f, "B", Vector2.zero, new Vector2(size.x, 1f), c);
            Img(f, "L", Vector2.zero, new Vector2(1f, size.y), c);
            Img(f, "R", new Vector2(size.x - 1f, 0f), new Vector2(1f, size.y), c);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Texture2D DiscTexture(int size)
        {
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f, ro = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(ro - d + 0.5f) * 255f));
                }
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }
    }

    /// <summary>
    /// Fits its RectTransform to <see cref="Screen.safeArea"/> (notch, home indicator), re-applied when the safe area
    /// or the resolution changes. <see cref="UseFullRect"/>(true) ignores the insets (e.g. an external display).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaRoot : MonoBehaviour
    {
        private Rect _applied = new Rect(-1f, -1f, -1f, -1f);
        private Vector2Int _screen;
        private bool _full;

        public RectTransform Root => (RectTransform)transform;

        /// <summary>True: use the full screen rect, ignoring the safe-area insets.</summary>
        public void UseFullRect(bool full)
        {
            if (_full == full) return;
            _full = full;
            _applied = new Rect(-1f, -1f, -1f, -1f);
            Apply();
        }

        private void Awake() => Apply();
        private void Update() => Apply();

        private void Apply()
        {
            var rt = (RectTransform)transform;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var area = _full ? new Rect(0f, 0f, Screen.width, Screen.height) : Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (area == _applied && screen == _screen) return;
            _applied = area;
            _screen = screen;
            rt.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rt.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
