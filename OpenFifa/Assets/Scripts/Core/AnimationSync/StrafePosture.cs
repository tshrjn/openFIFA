using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Which posture a sideways/backwards move is shown in. Off-ball players re-position facing the ball with the sim's
    /// held Jockey (it keeps the body on the ball), so every side-on shuffle used the low defensive jockey clip, and
    /// attackers waiting for a pass looked like they were defending. Only the side out of possession shows the jockey;
    /// the side in possession side-steps upright. The animator blends the two with the Guard parameter
    /// (<see cref="AnimStateNames.ParamGuard"/>), smoothed so a turnover does not snap the pose. Pure.
    /// </summary>
    public static class StrafePosture
    {
        /// <summary>Time constant (s) of the Guard blend.</summary>
        public const float Smoothing = 0.35f;

        /// <summary>
        /// True when <paramref name="myTeam"/> is out of possession: the other side owns the ball, or it is loose and
        /// they touched it last. Team indices: 0 home, 1 away, -1 none.
        /// </summary>
        public static bool Defending(int myTeam, int ownerTeam, int lastTouchTeam)
        {
            if (myTeam < 0) return true;
            if (ownerTeam >= 0) return ownerTeam != myTeam;
            return lastTouchTeam >= 0 && lastTouchTeam != myTeam;
        }

        /// <summary>Guard target: 1 = defensive jockey, 0 = neutral side-step.</summary>
        public static float Target(bool defending) => defending ? 1f : 0f;

        /// <summary>One smoothing step of the Guard blend towards its target.</summary>
        public static float Step(float guard, bool defending, float dt)
        {
            if (dt <= 0f) return guard;
            float k = 1f - (float)Math.Exp(-dt / Smoothing);
            return guard + (Target(defending) - guard) * k;
        }
    }
}
