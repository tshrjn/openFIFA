using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Decouples render-frame touch sampling from fixed sim ticks. The device layer calls <see cref="PushFrame"/> once
    /// per rendered frame with that frame's touch samples; the per-tick input read calls <see cref="Drain"/> and gets
    /// every touch edge since the previous drain, in order, exactly once.
    ///
    /// Why: at 120 Hz with a 60 Hz sim, half the frames run no tick, so reading phases per tick loses Began/Ended
    /// (dropped presses, stuck charge); at 30 Hz a frame runs two ticks, so the same Began is seen twice.
    ///
    /// Rules (all keyed by finger id):
    /// - A finger's first sample is its Began, whatever phase the device reports (it may already say Moved/Stationary).
    ///   Began for a finger already down is treated as a continuation (devices that hold the phase for a frame).
    /// - Moved is emitted only when the position changed; consecutive undrained Moveds of one finger coalesce into one.
    /// - Ended/Canceled for a finger that is not down is ignored. A down finger that is missing from a frame's samples
    ///   is Canceled (focus loss, dropped end event), so a charge can never stick.
    /// - Each drain also reports Stationary for fingers still down that had no event since the last drain, so a tick
    ///   always sees every held finger.
    /// - <see cref="CancelAll"/> (sim paused or frozen): fingers the consumer has seen get Canceled on the next drain,
    ///   and fingers currently on the glass are ignored until they lift, so a tap on a menu never reaches the match.
    /// No allocation after warm-up.
    /// </summary>
    public sealed class TouchEventQueue
    {
        private struct FingerState
        {
            public int Id;
            public ControlVec2 Position;
        }

        /// <summary>Events waiting for the next drain.</summary>
        private readonly List<TouchPoint> _pending = new List<TouchPoint>(16);
        /// <summary>Fingers on the glass as of the last pushed frame.</summary>
        private readonly List<FingerState> _down = new List<FingerState>(10);
        /// <summary>Fingers whose Began has been drained and whose end has not.</summary>
        private readonly List<FingerState> _delivered = new List<FingerState>(10);
        /// <summary>Fingers to ignore until they lift (pressed or held across a CancelAll).</summary>
        private readonly List<int> _ignored = new List<int>(10);
        private readonly List<int> _seen = new List<int>(10);

        /// <summary>Events waiting for the next drain.</summary>
        public int PendingCount => _pending.Count;
        /// <summary>Fingers currently on the glass (excluding ignored ones).</summary>
        public int DownCount => _down.Count;

        /// <summary>Record one rendered frame's touch samples (every finger the device reports this frame).</summary>
        public void PushFrame(IReadOnlyList<TouchPoint> samples)
        {
            _seen.Clear();
            int n = samples != null ? samples.Count : 0;
            for (int i = 0; i < n; i++) PushSample(samples[i]);

            // A finger that vanished without an end event is cancelled.
            for (int i = _down.Count - 1; i >= 0; i--)
            {
                if (_seen.Contains(_down[i].Id)) continue;
                Enqueue(new TouchPoint(_down[i].Id, _down[i].Position, ControlTouchPhase.Canceled));
                _down.RemoveAt(i);
            }
            for (int i = _ignored.Count - 1; i >= 0; i--)
                if (!_seen.Contains(_ignored[i])) _ignored.RemoveAt(i);
        }

        private void PushSample(TouchPoint t)
        {
            _seen.Add(t.FingerId);
            bool ending = t.Phase == ControlTouchPhase.Ended || t.Phase == ControlTouchPhase.Canceled;
            int ig = _ignored.IndexOf(t.FingerId);
            if (ig >= 0)
            {
                if (ending) _ignored.RemoveAt(ig);
                return;
            }

            int d = IndexOf(_down, t.FingerId);
            if (ending)
            {
                if (d < 0) return; // already ended, or never seen
                _down.RemoveAt(d);
                Enqueue(t);
                return;
            }
            if (d < 0)
            {
                _down.Add(new FingerState { Id = t.FingerId, Position = t.Position });
                Enqueue(new TouchPoint(t.FingerId, t.Position, ControlTouchPhase.Began));
                return;
            }
            var f = _down[d];
            if (f.Position.X == t.Position.X && f.Position.Y == t.Position.Y) return;
            f.Position = t.Position;
            _down[d] = f;
            EnqueueMove(t.FingerId, t.Position);
        }

        /// <summary>
        /// Moves every event since the last drain into <paramref name="into"/> (cleared first), followed by a
        /// Stationary sample for each delivered finger that had no event in this batch.
        /// </summary>
        public void Drain(List<TouchPoint> into)
        {
            into.Clear();
            for (int i = 0; i < _pending.Count; i++)
            {
                var e = _pending[i];
                into.Add(e);
                int d = IndexOf(_delivered, e.FingerId);
                switch (e.Phase)
                {
                    case ControlTouchPhase.Began:
                        if (d >= 0) _delivered.RemoveAt(d);
                        _delivered.Add(new FingerState { Id = e.FingerId, Position = e.Position });
                        break;
                    case ControlTouchPhase.Ended:
                    case ControlTouchPhase.Canceled:
                        if (d >= 0) _delivered.RemoveAt(d);
                        break;
                    default:
                        if (d >= 0) _delivered[d] = new FingerState { Id = e.FingerId, Position = e.Position };
                        break;
                }
            }
            int batch = _pending.Count;
            _pending.Clear();
            for (int i = 0; i < _delivered.Count; i++)
            {
                bool had = false;
                for (int k = 0; k < batch && !had; k++) had = into[k].FingerId == _delivered[i].Id;
                if (!had) into.Add(new TouchPoint(_delivered[i].Id, _delivered[i].Position, ControlTouchPhase.Stationary));
            }
        }

        /// <summary>
        /// Drops undrained events, cancels (on the next drain) every finger the consumer has seen, and ignores the
        /// fingers on the glass until they lift. Call every frame the sim is not advancing.
        /// </summary>
        public void CancelAll()
        {
            _pending.Clear();
            for (int i = 0; i < _delivered.Count; i++)
                _pending.Add(new TouchPoint(_delivered[i].Id, _delivered[i].Position, ControlTouchPhase.Canceled));
            for (int i = 0; i < _down.Count; i++)
                if (!_ignored.Contains(_down[i].Id)) _ignored.Add(_down[i].Id);
            _down.Clear();
        }

        /// <summary>Forget everything (input source switched away from touch).</summary>
        public void Reset()
        {
            _pending.Clear();
            _down.Clear();
            _delivered.Clear();
            _ignored.Clear();
        }

        private void Enqueue(TouchPoint t) => _pending.Add(t);

        private void EnqueueMove(int id, ControlVec2 pos)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].FingerId != id) continue;
                if (_pending[i].Phase == ControlTouchPhase.Moved)
                {
                    _pending[i] = new TouchPoint(id, pos, ControlTouchPhase.Moved);
                    return;
                }
                break;
            }
            _pending.Add(new TouchPoint(id, pos, ControlTouchPhase.Moved));
        }

        private static int IndexOf(List<FingerState> list, int id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == id) return i;
            return -1;
        }
    }
}
