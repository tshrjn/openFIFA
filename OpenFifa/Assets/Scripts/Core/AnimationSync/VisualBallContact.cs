using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// The ball is drawn larger than the sim ball for readability (up to 1.8x, resting on the grass, so its centre is
    /// higher too), but contact IK is planned against the sim ball. These move a foot target from the sim ball's surface
    /// onto the drawn ball's surface, so the boot meets the ball the player sees instead of sinking into it. Pure.
    /// </summary>
    public static class VisualBallContact
    {
        /// <summary>
        /// Centre of the drawn ball for a sim ball centre: same ground position; a grounded ball sits on the grass at
        /// its drawn radius, an airborne one keeps its height (never below the drawn radius).
        /// </summary>
        public static SimVec3 VisualCentre(SimVec3 simBall, float simRadius, float visualRadius)
        {
            float lift = Math.Max(0f, visualRadius - simRadius);
            float y = simBall.Y <= simRadius + 0.02f ? simBall.Y + lift : Math.Max(simBall.Y, visualRadius);
            return new SimVec3(simBall.X, y, simBall.Z);
        }

        /// <summary>
        /// Strike target on the drawn ball: the same direction from the ball centre as the planned target, pushed out by
        /// the extra radius, about the drawn centre. Unchanged when the ball is drawn at its true size.
        /// </summary>
        public static SimVec3 StrikeTarget(SimVec3 plannedTarget, SimVec3 simBall, float simRadius, float visualRadius)
        {
            float extra = visualRadius - simRadius;
            if (extra <= 1e-4f) return plannedTarget;
            SimVec3 c = VisualCentre(simBall, simRadius, visualRadius);
            SimVec3 d = plannedTarget - simBall;
            float m = d.Magnitude;
            if (m < 1e-4f) return c;
            return c + d * ((m + extra) / m);
        }

        /// <summary>
        /// Plant-foot target beside the drawn ball: moved away from the ball on the ground by the extra radius (the plant
        /// foot stays on the grass, so its height is kept).
        /// </summary>
        public static SimVec3 PlantTarget(SimVec3 plannedTarget, SimVec3 simBall, float simRadius, float visualRadius)
        {
            float extra = visualRadius - simRadius;
            if (extra <= 1e-4f) return plannedTarget;
            SimVec3 d = (plannedTarget - simBall).Flat;
            float m = d.Magnitude;
            if (m < 1e-4f) return plannedTarget;
            SimVec3 shift = d * (extra / m);
            return new SimVec3(plannedTarget.X + shift.X, plannedTarget.Y, plannedTarget.Z + shift.Z);
        }
    }
}
