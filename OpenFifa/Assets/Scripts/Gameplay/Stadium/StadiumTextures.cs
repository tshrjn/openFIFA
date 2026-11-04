using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>Colour cells of the stadium palette atlas (one Lit material for all structure).</summary>
    public enum StadiumCell
    {
        Concrete = 0,
        ConcreteDark = 1,
        Tread = 2,
        Roof = 3,
        RoofUnder = 4,
        Fascia = 5,
        FasciaAway = 6,
        Steel = 7,
        Glass = 8,
        Tunnel = 9,
        Apron = 10,
        BoardBack = 11,
        Bench = 12,
        White = 13,
        Wall = 14,
        LampFrame = 15,
    }

    /// <summary>
    /// Procedural textures for the stadium, generated at runtime (no asset files): palette atlas, crowd cards,
    /// LED board panels and the soft light halo. All deterministic for a given seed.
    /// </summary>
    public static class StadiumTextures
    {
        public const int PaletteCells = 4;     // 4 x 4 cells
        public const int PaletteCellPx = 8;
        public const int CrowdSeatPx = 32;
        public const int CrowdSeatsPerTile = 32;
        public const int CrowdBandPx = CrowdVariation.CardBandPx;
        /// <summary>Card height in metres that one crowd band covers (tread to above the heads).</summary>
        public const float CrowdCardHeight = 1.3f;
        public const float SeatWidth = 0.5f;
        public static float CrowdTileMetres => CrowdSeatsPerTile * SeatWidth;
        public const float LedTileMetres = 12f;

        /// <summary>UV of the centre of a palette cell.</summary>
        public static Vector2 CellUv(StadiumCell cell)
        {
            int i = (int)cell;
            return new Vector2(((i % PaletteCells) + 0.5f) / PaletteCells, ((i / PaletteCells) + 0.5f) / PaletteCells);
        }

        public static Texture2D NewPalette()
        {
            int n = PaletteCells * PaletteCellPx;
            return new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "StadiumPalette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        public static void FillPalette(Texture2D tex, Color home, Color away)
        {
            var cells = new Color[PaletteCells * PaletteCells];
            cells[(int)StadiumCell.Concrete] = new Color(0.56f, 0.56f, 0.55f);
            cells[(int)StadiumCell.ConcreteDark] = new Color(0.26f, 0.27f, 0.29f);
            cells[(int)StadiumCell.Tread] = new Color(0.42f, 0.42f, 0.43f);
            cells[(int)StadiumCell.Roof] = new Color(0.62f, 0.64f, 0.67f);
            cells[(int)StadiumCell.RoofUnder] = new Color(0.86f, 0.87f, 0.88f);
            cells[(int)StadiumCell.Fascia] = Shade(home, 0.85f);
            cells[(int)StadiumCell.FasciaAway] = Shade(away, 0.85f);
            cells[(int)StadiumCell.Steel] = new Color(0.38f, 0.4f, 0.43f);
            cells[(int)StadiumCell.Glass] = new Color(0.55f, 0.62f, 0.68f);
            cells[(int)StadiumCell.Tunnel] = new Color(0.03f, 0.03f, 0.035f);
            cells[(int)StadiumCell.Apron] = new Color(0.16f, 0.27f, 0.15f);
            cells[(int)StadiumCell.BoardBack] = new Color(0.16f, 0.17f, 0.19f);
            cells[(int)StadiumCell.Bench] = Shade(home, 0.6f);
            cells[(int)StadiumCell.White] = new Color(0.9f, 0.9f, 0.9f);
            cells[(int)StadiumCell.Wall] = new Color(0.2f, 0.21f, 0.24f);
            cells[(int)StadiumCell.LampFrame] = new Color(0.12f, 0.12f, 0.13f);
            int n = PaletteCells * PaletteCellPx;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = cells[(y / PaletteCellPx) * PaletteCells + x / PaletteCellPx];
            tex.SetPixels32(px);
            tex.Apply(false, false);
        }

        public static Texture2D NewCrowd()
        {
            return new Texture2D(CrowdSeatPx * CrowdSeatsPerTile, CrowdBandPx * CrowdVariation.TotalRows, TextureFormat.RGBA32, true)
            {
                name = "StadiumCrowdCards",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4,
                mipMapBias = -0.3f,
            };
        }

        private static readonly Color[] SkinTones =
        {
            new Color(0.96f, 0.80f, 0.69f), new Color(0.90f, 0.72f, 0.58f), new Color(0.80f, 0.60f, 0.45f),
            new Color(0.65f, 0.46f, 0.33f), new Color(0.47f, 0.32f, 0.22f), new Color(0.33f, 0.22f, 0.15f),
        };

        private static readonly Color[] NeutralShirts =
        {
            new Color(0.12f, 0.12f, 0.14f), new Color(0.88f, 0.88f, 0.86f), new Color(0.38f, 0.4f, 0.44f),
            new Color(0.15f, 0.22f, 0.42f), new Color(0.3f, 0.45f, 0.65f), new Color(0.5f, 0.38f, 0.25f),
            new Color(0.25f, 0.38f, 0.22f), new Color(0.7f, 0.66f, 0.55f), new Color(0.75f, 0.2f, 0.2f),
            new Color(0.9f, 0.75f, 0.2f),
        };

        private static readonly Color[] HairTones =
        {
            new Color(0.08f, 0.06f, 0.05f), new Color(0.2f, 0.13f, 0.08f), new Color(0.35f, 0.24f, 0.13f),
            new Color(0.55f, 0.42f, 0.25f), new Color(0.6f, 0.6f, 0.6f),
        };

        /// <summary>
        /// Seated spectators: <see cref="StadiumCrowd.BandCount"/> crowd mixes x <see cref="CrowdVariation.VariantsPerBand"/>
        /// variant rows, each a 64 px card (<see cref="CrowdCardHeight"/> m) of 32 seats x 32 px. Figures are drawn big and
        /// flat (wide shirt blocks, 16 px heads, little dark gap) so they read as people, not 1-2 px noise, at broadcast
        /// distance. The top CrowdVariation.CardTopPadPx (8) px of every card are background and the bob is clamped inside them, so a card never shows a neighbouring row.
        /// </summary>
        public static void FillCrowd(Texture2D tex, Color home, Color away, uint seed)
        {
            tex.SetPixels(CrowdPixels(tex.width, tex.height, home, away, seed));
            tex.Apply(true, false);
        }

        /// <summary>Pixels for <see cref="FillCrowd"/> (row-major, y = 0 at the bottom); no texture needed, so it can be previewed offline.</summary>
        public static Color[] CrowdPixels(int w, int h, Color home, Color away, uint seed)
        {
            var px = new Color[w * h];
            var bg = new Color(0.1f, 0.105f, 0.12f);
            var seat = Color.Lerp(Shade(home, 0.5f), new Color(0.22f, 0.23f, 0.26f), 0.5f);
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            for (int band = 0; band < StadiumCrowd.BandCount; band++)
            for (int variant = 0; variant < CrowdVariation.VariantsPerBand; variant++)
            {
                int y0 = (band * CrowdVariation.VariantsPerBand + variant) * CrowdBandPx;
                uint vs = seed ^ (uint)(variant * 0x51ED27u);
                for (int s = 0; s < CrowdSeatsPerTile; s++)
                {
                    uint hsh = StadiumCrowd.Hash((uint)(band * 977 + variant * 7919 + s * 131) ^ seed);
                    float r0 = StadiumCrowd.Unit(hsh), r1 = StadiumCrowd.Unit(StadiumCrowd.Hash(hsh + 1)),
                        r2 = StadiumCrowd.Unit(StadiumCrowd.Hash(hsh + 2)), r3 = StadiumCrowd.Unit(StadiumCrowd.Hash(hsh + 3)),
                        r4 = StadiumCrowd.Unit(StadiumCrowd.Hash(hsh + 4)), r5 = StadiumCrowd.Unit(StadiumCrowd.Hash(hsh + 5));
                    int cx = s * CrowdSeatPx + CrowdSeatPx / 2;
                    // Seat back (shows on empty seats and between spectators).
                    FillRect(px, w, h, cx - 13, y0 + 8, cx + 13, y0 + 30, seat * 0.85f, seat, 2f);
                    int kind = StadiumCrowd.SeatKind(band, s, vs);
                    if (kind < 0) continue;

                    Color shirt = kind == 0 ? home : kind == 1 ? away : NeutralShirts[(int)(r0 * NeutralShirts.Length) % NeutralShirts.Length];
                    shirt = Shade(shirt, 0.85f + 0.3f * r1);
                    int jx = cx + (int)((r2 - 0.5f) * 4f);
                    int halfW = 11 + (int)(r3 * 3f);
                    int shoulder = y0 + CrowdVariation.ShoulderBasePx + Mathf.Min(CrowdVariation.ShoulderJitterPx, (int)(r4 * 4f));
                    int headR = CrowdVariation.HeadRadiusMaxPx - 1 + (r5 > 0.6f ? 1 : 0);
                    int headY = shoulder + headR + 1;
                    var skin = SkinTones[(int)(r1 * 97f) % SkinTones.Length];
                    var hair = HairTones[(int)(r0 * 53f) % HairTones.Length];

                    // Raised arms on a few spectators (kept below the top pad, CrowdVariation.CardTopPadPx).
                    if (r5 < 0.08f)
                    {
                        int armTop = y0 + CrowdBandPx - CrowdVariation.CardTopPadPx;
                        FillRect(px, w, h, jx - halfW - 2, shoulder - 6, jx - halfW + 3, armTop, shirt * 0.9f, shirt, 0f);
                        FillRect(px, w, h, jx + halfW - 3, shoulder - 6, jx + halfW + 2, armTop, shirt * 0.9f, shirt, 0f);
                    }
                    // Torso: one strong colour block with rounded shoulders.
                    FillRect(px, w, h, jx - halfW, y0 + 6, jx + halfW, shoulder, shirt * 0.82f, shirt, 5f);
                    // Scarf or shirt stripe on some home/away fans.
                    if (kind < 2 && r4 > 0.7f)
                        FillRect(px, w, h, jx - halfW + 2, shoulder - 6, jx + halfW - 2, shoulder - 2, Color.white * 0.88f, Color.white * 0.92f, 0f);
                    // Neck and head, hair on top.
                    FillRect(px, w, h, jx - 3, shoulder - 1, jx + 3, shoulder + 2, skin * 0.8f, skin * 0.8f, 0f);
                    FillCircle(px, w, h, jx, headY, headR, skin, hair, r3 > 0.25f ? 0.2f : 1.1f);
                }
            }
            return px;
        }

        /// <summary>One ad cell is LedAdPx x LedRowPx; the texture holds the ad row (top) and the goal row (bottom).</summary>
        public const int LedAdPx = 256, LedRowPx = 48;

        public static Texture2D NewLed()
        {
            return new Texture2D(LedAdPx * LedBoardSchedule.AdCount, LedRowPx * 2, TextureFormat.RGBA32, true)
            {
                name = "StadiumLedBoards",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4,
                mipMapBias = -0.3f,
            };
        }

        /// <summary>Original, sponsor-free wordmarks (generic football phrases; no real brands).</summary>
        public static readonly string[] LedWords =
        {
            "OPENFOOTBALL", "FAIR PLAY", "MATCHDAY", "RESPECT", "PITCHSIDE", "KICK OFF", "TEAM SPIRIT", "GOAL RUSH",
        };

        /// <summary>
        /// Eight ad designs (background, graphic and a pixel-font wordmark in club and neutral colours) in the top row,
        /// and the goal graphic ("GOAL!" on alternating club colours) in the bottom row.
        /// </summary>
        public static void FillLed(Texture2D tex, Color home, Color away)
        {
            tex.SetPixels(LedPixels(tex.width, tex.height, home, away));
            tex.Apply(true, false);
        }

        /// <summary>Pixels for <see cref="FillLed"/> (row-major, y = 0 at the bottom).</summary>
        public static Color[] LedPixels(int w, int h, Color home, Color away)
        {
            var px = new Color[w * h];
            Color[] bgs =
            {
                Shade(home, 0.8f), new Color(0.02f, 0.03f, 0.08f), new Color(0.93f, 0.93f, 0.93f), Shade(away, 0.75f),
                new Color(0.02f, 0.28f, 0.32f), new Color(0.03f, 0.03f, 0.05f), new Color(0.96f, 0.76f, 0.06f), Shade(home, 0.45f),
            };
            Color[] fgs =
            {
                Color.white, new Color(0.15f, 0.85f, 1f), Shade(home, 0.9f), Color.white,
                new Color(0.96f, 0.96f, 0.96f), new Color(1f, 0.58f, 0.12f), new Color(0.05f, 0.05f, 0.08f), new Color(1f, 0.9f, 0.35f),
            };
            int row = LedRowPx;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int ad = x / LedAdPx;
                int lx = x % LedAdPx;
                bool goalRow = y < row;
                int ly = goalRow ? y : y - row;
                float u = lx / (float)LedAdPx, v = ly / (float)(row - 1);
                Color c;
                if (goalRow)
                {
                    // Goal row: GOAL! centred in each cell, alternating home / away backgrounds, with chevrons.
                    bool homeCell = (ad & 1) == 0;
                    var gb = homeCell ? Shade(home, 0.85f) : Shade(away, 0.85f);
                    var gf = Color.white;
                    bool ink = TextInk("GOAL!", lx, ly, LedAdPx, row, 4) || Mathf.Abs(Mathf.Repeat(u * 10f - Mathf.Abs(v - 0.5f) * 2f, 1f) - 0.5f) < 0.07f && (u < 0.18f || u > 0.82f);
                    c = ink ? gf : gb;
                }
                else
                {
                    bool fg;
                    switch (ad % 4)
                    {
                        case 0: fg = u < 0.09f && v > 0.25f && v < 0.75f && ((int)(u * 60) & 1) == 0; break;                       // ticks
                        case 1: fg = u < 0.1f && Mathf.Abs(Mathf.Repeat(u * 18f - Mathf.Abs(v - 0.5f) * 1.6f, 1f) - 0.5f) < 0.18f; break; // chevrons
                        case 2: { float dx = (u - 0.055f) * LedAdPx / row, dy = v - 0.5f; float r = Mathf.Sqrt(dx * dx + dy * dy); fg = r > 0.16f && r < 0.27f; break; } // ring
                        default: fg = u < 0.1f && Mathf.Repeat(u * 30f + v * 1.5f, 1f) < 0.45f && v > 0.15f && v < 0.85f; break; // stripes
                    }
                    // Wordmark in the rest of the cell, clear of the graphic.
                    int tx = (int)(LedAdPx * 0.11f);
                    fg |= TextInk(LedWords[ad % LedWords.Length], lx - tx, ly, LedAdPx - tx, row, 3);
                    c = fg ? fgs[ad] : bgs[ad];
                }
                // Dark seam between cells, slight scanlines.
                if (lx == 0 || ly == 0 || ly == row - 1) c = new Color(0.01f, 0.01f, 0.01f);
                if ((y & 1) == 1) c *= 0.94f;
                c.a = 1f;
                px[y * w + x] = c;
            }
            return px;
        }

        /// <summary>Pixel-font text centred in a cellW x cellH cell at an integer scale (shrunk to fit). y = 0 is the bottom row.</summary>
        private static bool TextInk(string text, int x, int y, int cellW, int cellH, int scale)
        {
            int tw = PixelFont.Width(text);
            while (scale > 1 && tw * scale > cellW - 8) scale--;
            int ox = (cellW - tw * scale) / 2, oy = (cellH - PixelFont.GlyphHeight * scale) / 2;
            int fx = x - ox, fy = y - oy;
            if (fx < 0 || fy < 0) return false;
            fx /= scale;
            fy /= scale;
            return PixelFont.TextPixel(text, fx, PixelFont.GlyphHeight - 1 - fy);
        }

        /// <summary>Soft radial glow, premultiplied (rgb fades with alpha), for additive or alpha-blended halos.</summary>
        public static Texture2D NewHalo()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true)
            {
                name = "StadiumHalo",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a * (0.35f + 0.65f * a) + (r < 0.12f ? 0.4f * (1f - r / 0.12f) : 0f);
                a = Mathf.Clamp01(a);
                px[y * N + x] = new Color(a, a * 0.97f, a * 0.9f, a);
            }
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        private static Color Shade(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

        /// <summary>Rectangle [x0,x1) x [y0,y1) with a bottom-to-top colour ramp and optional rounded top corners.</summary>
        private static void FillRect(Color[] px, int w, int h, int x0, int y0, int x1, int y1, Color bottom, Color top, float cornerR)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
            {
                float t = y1 - 1 > y0 ? (y - y0) / (float)(y1 - 1 - y0) : 1f;
                var c = Color.Lerp(bottom, top, t);
                c.a = 1f;
                for (int xx = x0; xx < x1; xx++)
                {
                    if (cornerR > 0f)
                    {
                        float cy = y1 - cornerR;
                        if (y > cy)
                        {
                            float cxl = x0 + cornerR, cxr = x1 - 1 - cornerR;
                            float ddx = xx < cxl ? cxl - xx : xx > cxr ? xx - cxr : 0f;
                            float ddy = y - cy;
                            if (ddx * ddx + ddy * ddy > cornerR * cornerR) continue;
                        }
                    }
                    int x = ((xx % w) + w) % w;
                    px[y * w + x] = c;
                }
            }
        }

        /// <summary>Head disc: skin, with hair over the top part (hairFrom = fraction of radius above centre where hair starts).</summary>
        private static void FillCircle(Color[] px, int w, int h, int cx, int cy, int r, Color skin, Color hair, float hairFrom)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int xx = cx - r; xx <= cx + r; xx++)
                {
                    float dx = xx - cx, dy = y - cy;
                    if (dx * dx + dy * dy > (r + 0.3f) * (r + 0.3f)) continue;
                    var c = dy > hairFrom * r ? hair : skin * (0.85f + 0.15f * (dy / r + 1f) * 0.5f);
                    c.a = 1f;
                    int x = ((xx % w) + w) % w;
                    px[y * w + x] = c;
                }
            }
        }
    }
}
