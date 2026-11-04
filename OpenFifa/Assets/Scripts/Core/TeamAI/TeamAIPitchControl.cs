using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Spearman-style pitch control on a coarse grid (default 32 x 21 cells).
    /// Each cell stores the probability that team 0 controls it, from the difference between the two teams'
    /// fastest times to intercept (reaction time, then a straight run at top speed), passed through a logistic.
    /// The grid is time-sliced: UpdateSlice refreshes a few rows per call so a full refresh spreads over
    /// several 10 Hz decision ticks. No allocations after construction.
    /// </summary>
    public sealed class TeamAIPitchControl
    {
        public const float DefaultReaction = 0.7f;
        /// <summary>Spearman's sigma for time-to-intercept uncertainty (s).</summary>
        public const float Sigma = 0.45f;
        private static readonly float LogisticK = (float)(Math.PI / (Math.Sqrt(3.0) * Sigma));

        public readonly int Cols;
        public readonly int Rows;
        private readonly float[] _control;
        private readonly float _cellW;
        private readonly float _cellL;
        private readonly PitchSpec _pitch;
        private int _nextRow;

        /// <summary>Number of completed full-grid sweeps.</summary>
        public int Sweeps { get; private set; }

        public TeamAIPitchControl(PitchSpec pitch, int cols = 32, int rows = 21)
        {
            _pitch = pitch;
            Cols = cols;
            Rows = rows;
            _control = new float[cols * rows];
            for (int i = 0; i < _control.Length; i++) _control[i] = 0.5f;
            _cellL = pitch.Length / cols;
            _cellW = pitch.Width / rows;
        }

        /// <summary>World centre of a cell. Columns run along Z, rows across X.</summary>
        public SimVec3 CellCentre(int col, int row) => new SimVec3(
            -_pitch.HalfWidth + (row + 0.5f) * _cellW, 0f, -_pitch.HalfLength + (col + 0.5f) * _cellL);

        /// <summary>Refresh `rowCount` rows (wrapping). Returns true when a full sweep completed.</summary>
        public bool UpdateSlice(TeamAIWorld world, int rowCount)
        {
            bool completed = false;
            for (int r = 0; r < rowCount; r++)
            {
                UpdateRow(world, _nextRow);
                _nextRow++;
                if (_nextRow >= Rows)
                {
                    _nextRow = 0;
                    Sweeps++;
                    completed = true;
                }
            }
            return completed;
        }

        public void RebuildAll(TeamAIWorld world)
        {
            for (int r = 0; r < Rows; r++) UpdateRow(world, r);
            _nextRow = 0;
            Sweeps++;
        }

        private void UpdateRow(TeamAIWorld world, int row)
        {
            for (int c = 0; c < Cols; c++)
            {
                var cell = CellCentre(c, row);
                _control[row * Cols + c] = EvaluateTeam0(world, cell, 0f, -1);
            }
        }

        /// <summary>Probability that `team` controls the grid cell containing the point (nearest cell).</summary>
        public float ControlAt(int team, SimVec3 p)
        {
            int c = (int)((p.Z + _pitch.HalfLength) / _cellL);
            int r = (int)((p.X + _pitch.HalfWidth) / _cellW);
            if (c < 0) c = 0; else if (c >= Cols) c = Cols - 1;
            if (r < 0) r = 0; else if (r >= Rows) r = Rows - 1;
            float v = _control[r * Cols + c];
            return team == 0 ? v : 1f - v;
        }

        /// <summary>Raw team-0 value for a cell (tests, debug views).</summary>
        public float CellValue(int col, int row) => _control[row * Cols + col];

        /// <summary>
        /// Exact pitch control for `team` at a point, with an optional ball arrival time (players who would
        /// arrive before the ball wait for it) and an optional player excluded (e.g. the passer).
        /// </summary>
        public static float Evaluate(TeamAIWorld world, int team, SimVec3 point, float ballArrival, int excludeIndex)
        {
            float v = EvaluateTeam0(world, point, ballArrival, excludeIndex);
            return team == 0 ? v : 1f - v;
        }

        private static float EvaluateTeam0(TeamAIWorld world, SimVec3 point, float ballArrival, int excludeIndex)
        {
            float t0 = 999f, t1 = 999f;
            var players = world.Players;
            for (int i = 0; i < world.PlayerCount; i++)
            {
                if (!players[i].Active || i == excludeIndex) continue;
                float t = TeamAIMath.TimeToIntercept(players[i], point, DefaultReaction);
                if (players[i].Team == 0) { if (t < t0) t0 = t; }
                else if (t < t1) t1 = t;
            }
            if (t0 < ballArrival) t0 = ballArrival;
            if (t1 < ballArrival) t1 = ballArrival;
            return TeamAIMath.Sigmoid(LogisticK * (t1 - t0));
        }
    }
}
