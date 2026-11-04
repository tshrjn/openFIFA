using System;

namespace OpenFifa.Core
{
    /// <summary>One on-screen context button.</summary>
    public struct TouchButtonSpec
    {
        public TouchSlot Slot;
        /// <summary>Centre in normalised screen space (0..1 of width / height, origin bottom-left).</summary>
        public ControlVec2 Center;
        /// <summary>Visual radius as a fraction of screen height.</summary>
        public float Radius;

        public TouchButtonSpec(TouchSlot slot, ControlVec2 center, float radius)
        {
            Slot = slot;
            Center = center;
            Radius = radius;
        }
    }

    /// <summary>
    /// iPad touch layout. Sizes are fractions of screen height so a button is the same physical size on
    /// 4:3 and 3:2 screens; button positions are defined as offsets from the bottom-right corner in height
    /// units and converted to normalised coordinates for the given aspect (width / height, landscape).
    /// </summary>
    public sealed class TouchLayout
    {
        // Offsets from the bottom-right corner in screen-height units: (from right, from bottom, radius).
        private static readonly float[,] Cluster =
        {
            // Slot order: Primary, Strike, Through, Sprint
            { 0.32f, 0.12f, 0.070f },
            { 0.16f, 0.22f, 0.085f },
            { 0.13f, 0.43f, 0.065f },
            { 0.37f, 0.31f, 0.065f },
        };

        private static readonly TouchSlot[] ClusterSlots =
        {
            TouchSlot.Primary, TouchSlot.Strike, TouchSlot.Through, TouchSlot.Sprint,
        };

        /// <summary>Screen width / height.</summary>
        public float Aspect { get; private set; }
        public bool LeftHanded { get; private set; }
        /// <summary>Area where a touch spawns the floating stick (normalised).</summary>
        public ControlRect StickZone { get; private set; }
        /// <summary>Stick travel radius as a fraction of screen height.</summary>
        public float StickRadius { get; private set; } = 0.11f;
        /// <summary>Stick dead zone as a fraction of the stick radius.</summary>
        public float StickDeadZone { get; private set; } = 0.12f;
        /// <summary>Finger travel (fraction of height) on a button that counts as a swipe rather than a tap.</summary>
        public float SwipeMinDistance { get; private set; } = 0.05f;
        /// <summary>Hit area multiplier over the visual radius.</summary>
        public float HitSlop { get; private set; } = 1.15f;
        public TouchButtonSpec[] Buttons { get; private set; }

        public static TouchLayout IPad4x3(bool leftHanded = false) => ForAspect(4f / 3f, leftHanded);
        public static TouchLayout IPad3x2(bool leftHanded = false) => ForAspect(1.5f, leftHanded);

        /// <summary>Builds the layout for any landscape aspect (iPads range from 4:3 = 1.33 to about 1.52).</summary>
        public static TouchLayout ForAspect(float aspect, bool leftHanded = false)
        {
            if (aspect < 1f) aspect = 1f;
            var buttons = new TouchButtonSpec[ClusterSlots.Length];
            for (int i = 0; i < ClusterSlots.Length; i++)
            {
                float x = 1f - Cluster[i, 0] / aspect;
                float y = Cluster[i, 1];
                if (leftHanded) x = 1f - x;
                buttons[i] = new TouchButtonSpec(ClusterSlots[i], new ControlVec2(x, y), Cluster[i, 2]);
            }
            var zone = new ControlRect(0f, 0f, 0.40f, 0.80f);
            return new TouchLayout
            {
                Aspect = aspect,
                LeftHanded = leftHanded,
                StickZone = leftHanded ? zone.MirroredX() : zone,
                Buttons = buttons,
            };
        }

        public TouchButtonSpec GetButton(TouchSlot slot)
        {
            for (int i = 0; i < Buttons.Length; i++)
                if (Buttons[i].Slot == slot) return Buttons[i];
            return default;
        }

        /// <summary>Converts a normalised screen delta to height units (so X and Y are the same physical scale).</summary>
        public ControlVec2 ToHeightUnits(ControlVec2 normalisedDelta) => new ControlVec2(normalisedDelta.X * Aspect, normalisedDelta.Y);

        /// <summary>Distance in height units between two normalised points.</summary>
        public float Distance(ControlVec2 a, ControlVec2 b) => ToHeightUnits(a - b).Magnitude;

        /// <summary>The button under a touch (nearest within its hit radius), or <see cref="TouchSlot.None"/>.</summary>
        public TouchSlot HitTest(ControlVec2 pos)
        {
            TouchSlot best = TouchSlot.None;
            float bestRatio = float.MaxValue;
            for (int i = 0; i < Buttons.Length; i++)
            {
                float d = Distance(pos, Buttons[i].Center);
                float ratio = d / (Buttons[i].Radius * HitSlop);
                if (ratio <= 1f && ratio < bestRatio)
                {
                    bestRatio = ratio;
                    best = Buttons[i].Slot;
                }
            }
            return best;
        }

        /// <summary>Classifies finger travel on a button. Travel is in normalised screen units.</summary>
        public TouchGesture Classify(ControlVec2 normalisedTravel)
        {
            ControlVec2 d = ToHeightUnits(normalisedTravel);
            if (d.Magnitude < SwipeMinDistance) return TouchGesture.Tap;
            if (Math.Abs(d.Y) >= Math.Abs(d.X)) return d.Y > 0f ? TouchGesture.SwipeUp : TouchGesture.SwipeDown;
            return d.X > 0f ? TouchGesture.SwipeRight : TouchGesture.SwipeLeft;
        }
    }
}
