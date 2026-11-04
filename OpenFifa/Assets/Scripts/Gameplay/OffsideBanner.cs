using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Lower-third "OFFSIDE" caption with the offender's number, name and team.
    /// Original broadcast-style design: dark plate, coloured accent bar, team colour chip.
    /// Slides in from the left using unscaled time (works while the game is frozen).
    /// </summary>
    public class OffsideBanner : MonoBehaviour, IOffsideBannerView
    {
        private RectTransform _plate;
        private CanvasGroup _group;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _number;
        private TextMeshProUGUI _detail;
        private Image _teamChip;

        private float _slideDuration = 0.2f;
        private float _progress;      // 0 hidden .. 1 shown
        private float _target;
        private const float HiddenOffset = -760f;

        public bool IsShowing => _target > 0.5f;

        public void Initialize(OffsidePresentationConfig config)
        {
            if (_plate != null) return;

            string title = config != null ? config.BannerTitle : "OFFSIDE";
            Color accent = config != null ? config.BannerAccentColor : new Color(1f, 0.25f, 0.2f);
            Color plate = config != null ? config.BannerBackgroundColor : new Color(0.06f, 0.07f, 0.1f, 0.92f);
            _slideDuration = config != null ? Mathf.Max(0.01f, config.BannerSlideDuration) : 0.2f;

            var canvasGo = new GameObject("OffsideBannerCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Plate: anchored bottom-left, above the controls hint bar.
            _plate = NewRect("OffsidePlate", canvasGo.transform);
            _plate.anchorMin = _plate.anchorMax = new Vector2(0f, 0f);
            _plate.pivot = new Vector2(0f, 0f);
            _plate.sizeDelta = new Vector2(640f, 120f);
            _plate.anchoredPosition = new Vector2(HiddenOffset, 96f);
            _group = _plate.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            AddImage(_plate, plate);

            // Accent bar along the left edge.
            var accentRect = NewRect("Accent", _plate);
            Stretch(accentRect, new Vector2(0f, 0f), new Vector2(0f, 1f));
            accentRect.sizeDelta = new Vector2(12f, 0f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            AddImage(accentRect, accent);

            // Title strip.
            var titleRect = NewRect("Title", _plate);
            Stretch(titleRect, new Vector2(0f, 0.45f), new Vector2(1f, 1f));
            titleRect.offsetMin = new Vector2(36f, 0f);
            titleRect.offsetMax = new Vector2(-24f, -6f);
            _title = AddText(titleRect, title, 52f, TextAlignmentOptions.BottomLeft, Color.white);
            _title.characterSpacing = 12f;

            // Number box with team colour chip.
            var chipRect = NewRect("TeamChip", _plate);
            chipRect.anchorMin = chipRect.anchorMax = new Vector2(0f, 0f);
            chipRect.pivot = new Vector2(0f, 0f);
            chipRect.sizeDelta = new Vector2(64f, 44f);
            chipRect.anchoredPosition = new Vector2(36f, 12f);
            _teamChip = AddImage(chipRect, Color.white);
            _number = AddText(chipRect, "", 30f, TextAlignmentOptions.Center, Color.white);

            var detailRect = NewRect("Detail", _plate);
            Stretch(detailRect, new Vector2(0f, 0f), new Vector2(1f, 0.45f));
            detailRect.offsetMin = new Vector2(116f, 12f);
            detailRect.offsetMax = new Vector2(-24f, 0f);
            _detail = AddText(detailRect, "", 26f, TextAlignmentOptions.Left, new Color(0.85f, 0.88f, 0.95f));
        }

        public void Show(OffsideBannerInfo info) =>
            Show(info.ShirtNumber, info.PlayerName, info.TeamLabel, info.TeamColor);

        /// <summary>Slide in with the offender's details.</summary>
        public void Show(int shirtNumber, string playerName, string teamLabel, Color teamColor)
        {
            if (_plate == null) Initialize(null);
            _number.text = shirtNumber > 0 ? shirtNumber.ToString() : "";
            _teamChip.color = new Color(teamColor.r, teamColor.g, teamColor.b, 1f);
            _detail.text = string.IsNullOrEmpty(teamLabel)
                ? (playerName ?? "").ToUpperInvariant()
                : $"{(playerName ?? "").ToUpperInvariant()}  <color=#8C93A6>|</color>  {teamLabel.ToUpperInvariant()}";
            _target = 1f;
        }

        public void Hide()
        {
            _target = 0f;
        }

        /// <summary>Hide instantly (used on skip).</summary>
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
            _plate.anchoredPosition = new Vector2(Mathf.Lerp(HiddenOffset, 48f, eased), 96f);
            _group.alpha = eased;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Image AddImage(RectTransform rect, Color color)
        {
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI AddText(RectTransform parent, string text, float size,
            TextAlignmentOptions alignment, Color color)
        {
            var rect = NewRect("Text", parent);
            Stretch(rect, Vector2.zero, Vector2.one);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            tmp.richText = true;
            return tmp;
        }
    }
}
