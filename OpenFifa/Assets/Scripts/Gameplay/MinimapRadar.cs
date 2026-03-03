using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// HUD minimap radar showing player and ball positions on a simplified pitch outline.
    /// Attach to a RectTransform — creates its own child UI elements.
    /// </summary>
    public class MinimapRadar : MonoBehaviour
    {
        private RectTransform _rect;
        private float _pitchLength;
        private float _pitchWidth;
        private Transform _ball;
        private readonly List<DotEntry> _dots = new List<DotEntry>();
        private RectTransform _ballDot;

        private struct DotEntry
        {
            public Transform Player;
            public RectTransform Dot;
        }

        public void Initialize(float pitchLength, float pitchWidth,
            Transform ball, List<GameObject> homePlayers, List<GameObject> awayPlayers)
        {
            _rect = GetComponent<RectTransform>();
            _pitchLength = pitchLength;
            _pitchWidth = pitchWidth;
            _ball = ball;

            // Background
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.15f, 0.05f, 0.75f);

            // Pitch outline
            CreatePitchOutline();

            // Player dots
            foreach (var p in homePlayers)
                _dots.Add(CreateDot(p.transform, new Color(0.3f, 0.5f, 1f), 8f));

            foreach (var p in awayPlayers)
                _dots.Add(CreateDot(p.transform, new Color(1f, 0.3f, 0.3f), 8f));

            // Ball dot (white, slightly larger)
            var ballEntry = CreateDot(ball, Color.white, 10f);
            _ballDot = ballEntry.Dot;
            _dots.Add(ballEntry);
        }

        private DotEntry CreateDot(Transform target, Color color, float size)
        {
            var dotGo = new GameObject("Dot");
            dotGo.transform.SetParent(transform, false);
            var dotRect = dotGo.AddComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(size, size);

            var img = dotGo.AddComponent<Image>();
            img.color = color;

            return new DotEntry { Player = target, Dot = dotRect };
        }

        private void CreatePitchOutline()
        {
            // Center line (vertical on minimap)
            CreateOutlineLine(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), 1f);
            // Border
            CreateOutlineLine(new Vector2(0f, 0f), new Vector2(1f, 0f), 1f); // bottom
            CreateOutlineLine(new Vector2(0f, 1f), new Vector2(1f, 1f), 1f); // top
            CreateOutlineLine(new Vector2(0f, 0f), new Vector2(0f, 1f), 1f); // left
            CreateOutlineLine(new Vector2(1f, 0f), new Vector2(1f, 1f), 1f); // right
        }

        private void CreateOutlineLine(Vector2 from, Vector2 to, float width)
        {
            var lineGo = new GameObject("OutlineLine");
            lineGo.transform.SetParent(transform, false);
            var lineRect = lineGo.AddComponent<RectTransform>();

            var mid = (from + to) * 0.5f;
            lineRect.anchorMin = mid;
            lineRect.anchorMax = mid;

            var parentSize = _rect.sizeDelta;
            float dx = (to.x - from.x) * parentSize.x;
            float dy = (to.y - from.y) * parentSize.y;
            float length = Mathf.Sqrt(dx * dx + dy * dy);

            // Determine if horizontal or vertical
            if (Mathf.Abs(dx) > Mathf.Abs(dy))
                lineRect.sizeDelta = new Vector2(length, width);
            else
                lineRect.sizeDelta = new Vector2(width, length);

            lineRect.anchoredPosition = Vector2.zero;

            var img = lineGo.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.3f);
        }

        private void LateUpdate()
        {
            if (_rect == null) return;

            var size = _rect.sizeDelta;

            foreach (var entry in _dots)
            {
                if (entry.Player == null || entry.Dot == null) continue;

                var pos = entry.Player.position;
                // Map world X (-halfLength..halfLength) to minimap (0..width)
                float u = (pos.x / _pitchLength + 0.5f);
                float v = (pos.z / _pitchWidth + 0.5f);

                u = Mathf.Clamp01(u);
                v = Mathf.Clamp01(v);

                entry.Dot.anchorMin = new Vector2(u, v);
                entry.Dot.anchorMax = new Vector2(u, v);
                entry.Dot.anchoredPosition = Vector2.zero;
            }
        }
    }
}
