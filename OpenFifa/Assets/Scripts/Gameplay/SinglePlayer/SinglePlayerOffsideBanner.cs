using UnityEngine;
using UnityEngine.UI;

namespace OpenFifa.Gameplay.SinglePlayer
{
    /// <summary>
    /// The offside lower-third on the single-player uGUI HUD ("OpenFootball HUD Canvas"): a compact FC-style plate
    /// bottom-left (where the home name panel sits; the name panels fade out once play stops for the offside) with an
    /// accent bar, the "OFFSIDE" title, a team-colour chip carrying the shirt number, then the player's name and the
    /// team's short name. Slides in from the left on unscaled time, so it plays through the slow motion and freeze.
    /// Built lazily under the HUD's safe-area root on the first Show; without a HUD it builds its own overlay canvas.
    /// </summary>
    [DefaultExecutionOrder(210)] // after SinglePlayerHudCanvas (200) built or refreshed the canvas
    public sealed class SinglePlayerOffsideBanner : MonoBehaviour, IOffsideBannerView
    {
        public const string PlateName = "OffsideBanner";
        // Layout in the HUD's 720-high canvas units.
        public const float Width = 300f, Height = 58f, AccentWidth = 5f, ChipWidth = 34f, ChipHeight = 22f;
        public const float HiddenOffset = -340f;

        private static readonly Color PlateColor = new Color(0.03f, 0.05f, 0.09f, 0.9f);
        private static readonly Color AccentColor = new Color(1f, 0.25f, 0.2f, 1f);
        private static readonly Color DetailColor = new Color(0.72f, 0.76f, 0.85f, 1f);

        private RectTransform _plate;
        private CanvasGroup _group;
        private Image _chip;
        private Text _number, _name, _team;
        private Font _font;
        private GameObject _ownCanvas;
        private float _progress, _target;
        private float _slideDuration = 0.2f;

        public bool IsShowing => _target > 0.5f;
        /// <summary>The plate (null until first shown).</summary>
        public RectTransform Plate => _plate;
        public string ShownName => _name != null ? _name.text : string.Empty;
        public string ShownNumber => _number != null ? _number.text : string.Empty;

        public float SlideDuration
        {
            get => _slideDuration;
            set => _slideDuration = Mathf.Max(0.01f, value);
        }

        public void Show(OffsideBannerInfo info)
        {
            if (_plate == null) Build();
            if (_plate == null) return;
            var c = new Color(info.TeamColor.r, info.TeamColor.g, info.TeamColor.b, 1f);
            _chip.color = c;
            // Dark number on a light kit colour, white otherwise.
            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            _number.color = lum > 0.6f ? new Color(0.05f, 0.07f, 0.12f) : Color.white;
            _number.text = info.ShirtNumber > 0 ? info.ShirtNumber.ToString() : string.Empty;
            _name.text = (info.PlayerName ?? string.Empty).ToUpperInvariant();
            _team.text = (info.TeamLabel ?? string.Empty).ToUpperInvariant();
            _target = 1f;
            _plate.SetAsLastSibling();
        }

        public void Hide() => _target = 0f;

        public void HideImmediate()
        {
            _target = 0f;
            _progress = 0f;
            Apply();
        }

        private void Update()
        {
            if (_plate == null || Mathf.Approximately(_progress, _target)) return;
            _progress = Mathf.MoveTowards(_progress, _target, Time.unscaledDeltaTime / _slideDuration);
            Apply();
        }

        private void Apply()
        {
            if (_plate == null) return;
            float eased = 1f - Mathf.Pow(1f - _progress, 3f);
            _plate.anchoredPosition = new Vector2(Mathf.Lerp(HiddenOffset, SinglePlayerHudCanvas.SideInset, eased),
                SinglePlayerHudCanvas.BottomInset + 8f);
            _group.alpha = eased;
        }

        private void OnDestroy()
        {
            if (_ownCanvas != null) Destroy(_ownCanvas);
            else if (_plate != null) Destroy(_plate.gameObject);
        }

        private void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransform root = null;
            var hud = GetComponent<SinglePlayerHudCanvas>();
            if (hud != null && hud.SafeArea != null) root = hud.SafeArea.Root;
            if (root == null) root = BuildOwnCanvas();

            _plate = Rect(root, PlateName, Vector2.zero, new Vector2(HiddenOffset, SinglePlayerHudCanvas.BottomInset + 8f),
                new Vector2(Width, Height));
            _group = _plate.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            Img(_plate, "Back", Vector2.zero, new Vector2(Width, Height), PlateColor);
            Img(_plate, "Accent", Vector2.zero, new Vector2(AccentWidth, Height), AccentColor);
            var title = Txt(_plate, "Title", new Vector2(14f, Height - 26f), new Vector2(Width - 24f, 24f), 20,
                Color.white, TextAnchor.MiddleLeft);
            title.text = "OFFSIDE";

            _chip = Img(_plate, "TeamChip", new Vector2(14f, 6f), new Vector2(ChipWidth, ChipHeight), Color.white);
            _number = Txt(_chip.rectTransform, "Number", Vector2.zero, new Vector2(ChipWidth, ChipHeight), 15,
                Color.white, TextAnchor.MiddleCenter);
            _number.GetComponent<Shadow>().enabled = false;
            _name = Txt(_plate, "Name", new Vector2(14f + ChipWidth + 8f, 6f), new Vector2(170f, ChipHeight), 17,
                Color.white, TextAnchor.MiddleLeft);
            _name.horizontalOverflow = HorizontalWrapMode.Wrap;
            _name.resizeTextForBestFit = true;
            _name.resizeTextMinSize = 11;
            _name.resizeTextMaxSize = 17;
            _team = Txt(_plate, "Team", new Vector2(Width - 60f, 6f), new Vector2(50f, ChipHeight), 14,
                DetailColor, TextAnchor.MiddleRight);
        }

        private RectTransform BuildOwnCanvas()
        {
            _ownCanvas = new GameObject("OffsideBannerCanvas", typeof(RectTransform));
            var canvas = _ownCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            var scaler = _ownCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, SinglePlayerHudCanvas.RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            return (RectTransform)_ownCanvas.transform;
        }

        private static RectTransform Rect(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Image Img(RectTransform parent, string name, Vector2 pos, Vector2 size, Color c)
        {
            var img = Rect(parent, name, Vector2.zero, pos, size).gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private Text Txt(RectTransform parent, string name, Vector2 pos, Vector2 size, int fontSize, Color c, TextAnchor align)
        {
            var t = Rect(parent, name, Vector2.zero, pos, size).gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
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
    }
}
