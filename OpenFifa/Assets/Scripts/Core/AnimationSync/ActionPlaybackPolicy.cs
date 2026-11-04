namespace OpenFifa.Core
{
    /// <summary>What the animation driver should do with the action its source reports this frame.</summary>
    public enum ActionPlaybackDecision
    {
        /// <summary>Keep the current plan (or keep having none).</summary>
        Keep = 0,
        /// <summary>Plan the reported action from scratch (a new id).</summary>
        Start,
        /// <summary>Same action, but its contact moved: re-plan it, keeping a clip that is already playing.</summary>
        Replan,
        /// <summary>A new running touch arrived while the previous one is still blending out: finish that first.</summary>
        Defer,
        /// <summary>The action was withdrawn before its contact (deflection, someone else got there): stop playing it.</summary>
        Abort,
    }

    /// <summary>
    /// Pure rules for when the presentation re-plans, replaces or abandons a contact-synchronised action. The sim-side
    /// tracker refreshes a prediction under a stable id every tick and snaps it to the resolved tick, so the driver
    /// must not treat "same id" as "same plan". Deterministic and allocation-free.
    /// </summary>
    public static class ActionPlaybackPolicy
    {
        /// <summary>Tick length assumed when the caller does not know it (s).</summary>
        public const float DefaultTickSeconds = 1f / 60f;
        /// <summary>
        /// Contact moves of at least this many ticks re-plan. Contact times are tick-quantised, so 0.5 means "any tick
        /// change": the snap from a prediction to the resolved tick is exactly the error that has to be removed.
        /// </summary>
        public const float ContactSlackTicks = 0.5f;
        /// <summary>A predicted ball-at-contact that moves (flat) further than this re-plans the IK / warp target (m).</summary>
        public const float BallSlack = 0.2f;

        /// <param name="plan">Current plan, or null.</param>
        /// <param name="planId">Id the current plan was made for.</param>
        /// <param name="planStarted">The current plan's clip (or layered touch) has started.</param>
        /// <param name="reportedNone">The source reports no action.</param>
        /// <param name="id">Reported action id.</param>
        /// <param name="contactTime">Reported contact time (s).</param>
        /// <param name="ballAtContact">Reported ball at contact.</param>
        /// <param name="layered">The reported action plays layered on locomotion (running touch).</param>
        /// <param name="now">Presentation sim time (s).</param>
        /// <param name="tickSeconds">Sim tick length (s); 0 or less uses <see cref="DefaultTickSeconds"/>.</param>
        public static ActionPlaybackDecision Decide(ActionSyncPlan plan, int planId, bool planStarted, bool reportedNone,
            int id, float contactTime, SimVec3 ballAtContact, bool layered, float now, float tickSeconds)
        {
            // No plan: start anything new; an action whose plan already ran out (still reported during its hold) is done.
            if (plan == null) return reportedNone || id == planId ? ActionPlaybackDecision.Keep : ActionPlaybackDecision.Start;

            if (reportedNone)
                // Withdrawn before its contact: it is not going to happen. At or after the contact it is simply over
                // (the hold expired) and the plan runs out on its own.
                return now < plan.Request.ContactTime ? ActionPlaybackDecision.Abort : ActionPlaybackDecision.Keep;

            if (id == planId)
                return ContactMoved(plan.Request.ContactTime, plan.Request.BallAtContact, contactTime, ballAtContact, tickSeconds)
                    ? ActionPlaybackDecision.Replan
                    : ActionPlaybackDecision.Keep;

            bool planLayered = plan.Profile.LayeredOnLocomotion;
            // A running touch never cuts a full-body clip that is still playing (e.g. the first-touch or pass clip).
            if (layered && !planLayered && planStarted) return ActionPlaybackDecision.Keep;
            // A running touch that is blending out finishes before the next one takes over, unless the next one's own
            // ramp has already begun (back-to-back retouch): then switching is the smaller jump.
            if (layered && planLayered && planStarted && now >= plan.Request.ContactTime && now < plan.Ik.StrikeReleaseEnd
                && now < contactTime - DribbleTouchSolver.BlendIn)
                return ActionPlaybackDecision.Defer;
            return ActionPlaybackDecision.Start;
        }

        /// <summary>True when a refreshed report differs enough from the plan to re-plan.</summary>
        public static bool ContactMoved(float plannedContact, SimVec3 plannedBall, float contactTime, SimVec3 ballAtContact,
            float tickSeconds)
        {
            float tick = tickSeconds > 0f ? tickSeconds : DefaultTickSeconds;
            float dt = contactTime - plannedContact;
            if (dt < 0f) dt = -dt;
            if (dt >= ContactSlackTicks * tick) return true;
            return (ballAtContact - plannedBall).Flat.SqrMagnitude > BallSlack * BallSlack;
        }

        /// <summary>
        /// Timing for a clip that is already playing when its contact moves to <paramref name="newContact"/>: continue from
        /// the clip time shown now, at the rate (clamped to the profile's range) that lands the contact frame on the new
        /// contact. Past the contact frame the clip just plays out at its current rate. No restart, no skip.
        /// </summary>
        public static ActionTimingPlan Retime(in ActionTimingPlan current, ActionClipProfile profile, float now, float newContact,
            float toleranceSeconds = 1e-3f)
        {
            float clip = current.ClipTimeAt(now);
            if (clip < 0f) clip = current.StartOffset;
            float rate = current.PlaybackRate > 0f ? current.PlaybackRate : 1f;
            float remaining = profile.ContactTime - clip;
            if (remaining <= 0f || profile.ContactTime <= 0f)
                return new ActionTimingPlan(now, clip, rate, now + remaining / rate, 0f, true);

            float available = newContact - now;
            float wanted = available > 0f ? remaining / available : profile.MaxRate;
            float min = profile.MinRate > 0f ? profile.MinRate : 0.1f;
            rate = AnimMath.Clamp(wanted, min, profile.MaxRate > min ? profile.MaxRate : min);
            float predicted = now + remaining / rate;
            float error = predicted - newContact;
            return new ActionTimingPlan(now, clip, rate, predicted, error, error <= toleranceSeconds && error >= -toleranceSeconds);
        }
    }
}
