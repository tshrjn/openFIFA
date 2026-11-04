namespace OpenFifa.Core
{
    /// <summary>Run-up plan: how many footfalls until the plant foot lands, and how to scale stride and cadence.</summary>
    public readonly struct StridePlan
    {
        /// <summary>Footfalls until and including the plant step (0 = already planted / no run-up).</summary>
        public readonly int Steps;
        public readonly float StrideLength;
        public readonly float NominalStrideLength;
        /// <summary>StrideLength / nominal.</summary>
        public readonly float StrideScale;
        /// <summary>Steps per second required.</summary>
        public readonly float Cadence;
        /// <summary>Required cadence / nominal cadence at the current speed. Use as locomotion playback rate.</summary>
        public readonly float CadenceScale;
        public readonly bool Feasible;

        public StridePlan(int steps, float strideLength, float nominal, float strideScale, float cadence, float cadenceScale, bool feasible)
        {
            Steps = steps;
            StrideLength = strideLength;
            NominalStrideLength = nominal;
            StrideScale = strideScale;
            Cadence = cadence;
            CadenceScale = cadenceScale;
            Feasible = feasible;
        }
    }

    /// <summary>
    /// Stride adjustment for run-ups (EA calls it "ball approach"): choose the step count so the plant foot is the
    /// correct foot, then spread the remaining distance evenly. Step length model: L(v) = a + b*v, clamped.
    /// </summary>
    public sealed class StrideAdjuster
    {
        public float StepLengthBase = 0.10f;
        public float StepLengthPerSpeed = 0.26f;
        public float MinStepLength = 0.45f;
        public float MaxStepLength = 2.2f;
        /// <summary>A shortened "stutter" step of ~30% is normal right before a plant.</summary>
        public float MinStrideScale = 0.70f;
        public float MaxStrideScale = 1.30f;
        public float MinCadenceScale = 0.70f;
        public float MaxCadenceScale = 1.50f;
        /// <summary>Run-up speeds above this (m/s) are not humanly plausible and are reported infeasible.</summary>
        public float MaxRunSpeed = 10.5f;

        /// <summary>Nominal single-step length (m) at a running speed (m/s).</summary>
        public float NominalStepLength(float speed)
        {
            if (speed < 0f) speed = -speed;
            return AnimMath.Clamp(StepLengthBase + StepLengthPerSpeed * speed, MinStepLength, MaxStepLength);
        }

        /// <param name="distanceToPlant">Horizontal distance (m) from the next footfall's origin to the plant-foot target.</param>
        /// <param name="timeToPlant">Seconds until the plant foot must land.</param>
        /// <param name="speed">Current running speed (m/s); used only when timeToPlant is not positive.</param>
        /// <param name="nextFootfall">Which foot lands next.</param>
        /// <param name="plantFoot">Foot that must land as the plant (support) foot.</param>
        public StridePlan Plan(float distanceToPlant, float timeToPlant, float speed, ActionSide nextFootfall, ActionSide plantFoot)
        {
            // Root motion is off: the sim covers distanceToPlant in timeToPlant, so the run-up speed is d/T.
            // Fall back to the current speed when the time is not usable.
            float runSpeed = timeToPlant > 1e-3f ? distanceToPlant / timeToPlant : speed;
            float nominal = NominalStepLength(runSpeed);
            if (distanceToPlant <= 1e-4f)
                return new StridePlan(0, 0f, nominal, 0f, 0f, 1f, true);

            // Footfall k (1-based) is nextFootfall when k is odd.
            bool oddIsPlant = nextFootfall == plantFoot;
            int raw = (int)System.Math.Round(distanceToPlant / nominal);
            if (raw < 1) raw = 1;

            int best = -1;
            float bestCost = float.MaxValue;
            for (int n = raw - 1; n <= raw + 1; n++)
            {
                if (n < 1) continue;
                bool parityOk = (n % 2 == 1) == oddIsPlant;
                if (!parityOk) continue;
                float scale = distanceToPlant / n / nominal;
                float cost = scale > 1f ? scale - 1f : 1f - scale;
                if (cost < bestCost - 1e-6f) { bestCost = cost; best = n; }
            }
            if (best < 1) best = oddIsPlant ? 1 : 2;

            float stride = distanceToPlant / best;
            float strideScale = stride / nominal;
            float cadence = timeToPlant > 1e-4f ? best / timeToPlant : float.PositiveInfinity;
            // Cadence is compared with what the locomotion clip plays now (current speed), so CadenceScale can drive its playback rate.
            float gaitSpeed = speed > 1e-3f ? speed : runSpeed;
            float nominalCadence = gaitSpeed > 1e-3f ? gaitSpeed / NominalStepLength(gaitSpeed) : 0f;
            float cadenceScale = nominalCadence > 0f ? cadence / nominalCadence : 1f;
            bool feasible = strideScale >= MinStrideScale && strideScale <= MaxStrideScale
                && (nominalCadence <= 0f || (cadenceScale >= MinCadenceScale && cadenceScale <= MaxCadenceScale))
                && timeToPlant > 0f && runSpeed <= MaxRunSpeed;
            return new StridePlan(best, stride, nominal, strideScale, cadence, nominalCadence > 0f ? AnimMath.Clamp(cadenceScale, MinCadenceScale, MaxCadenceScale) : 1f, feasible);
        }
    }
}
