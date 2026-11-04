using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>A club turned into a sim squad: 11 starters in sim slot order plus names and tactics.</summary>
    public sealed class RosterTeamSheet
    {
        public RosterClub Club;
        /// <summary>Starters in sim slot order (0 = GK).</summary>
        public RosterPlayer[] Starters = new RosterPlayer[MatchSetup.PlayersPerTeam];
        public SimPlayerAttributes[] Attributes = new SimPlayerAttributes[MatchSetup.PlayersPerTeam];
        /// <summary>Kick-off spots in the attack frame, same order.</summary>
        public SimVec3[] Formation = new SimVec3[MatchSetup.PlayersPerTeam];
        public TeamAITactics Tactics;
        public int KickOffTakerSlot = 10;
    }

    /// <summary>
    /// Builds a <see cref="MatchSetup"/> from two roster clubs: picks a starting XI for the club's formation, maps the
    /// roster's 1-20 attributes to the sim's 1-99 scale, and produces names for HUD and commentary.
    /// </summary>
    public static class RosterMatchBuilder
    {
        /// <summary>Roster 1-20 to sim 1-99: 1 -> 15, 14 -> 72, 20 -> 99 (linear).</summary>
        public static int ToSimRating(int roster)
        {
            int r = roster < 1 ? 1 : (roster > 20 ? 20 : roster);
            return 15 + (int)Math.Round((r - 1) * 84.0 / 19.0, MidpointRounding.AwayFromZero);
        }

        public static TeamAIFormation ParseFormation(string shape)
        {
            switch ((shape ?? string.Empty).Trim())
            {
                case "4-2-3-1": return TeamAIFormation.F4231;
                case "4-4-2":
                case "4-4-1-1": return TeamAIFormation.F442;
                case "3-5-2":
                case "3-4-3":
                case "5-3-2":
                case "3-4-1-2": return TeamAIFormation.F352;
                case "4-3-3":
                case "4-1-4-1":
                case "4-5-1":
                default: return TeamAIFormation.F433;
            }
        }

        public static SimPlayerAttributes ToSimAttributes(RosterPlayer p)
        {
            int G(RosterAttribute a) => ToSimRating(p.Get(a));
            var a = new SimPlayerAttributes
            {
                Pace = G(RosterAttribute.TopSpeed),
                Acceleration = G(RosterAttribute.Acceleration),
                Agility = G(RosterAttribute.Agility),
                Balance = G(RosterAttribute.Balance),
                Stamina = G(RosterAttribute.Stamina),
                Strength = G(RosterAttribute.Strength),
                Reactions = G(RosterAttribute.Reactions),
                Dribbling = G(RosterAttribute.Dribbling),
                BallControl = G(RosterAttribute.FirstTouch),
                ShortPassing = G(RosterAttribute.ShortPassing),
                LongPassing = G(RosterAttribute.LongPassing),
                Crossing = G(RosterAttribute.Crossing),
                Vision = G(RosterAttribute.Vision),
                Finishing = G(RosterAttribute.Finishing),
                ShotPower = G(RosterAttribute.ShotPower),
                LongShots = G(RosterAttribute.LongShots),
                Curve = G(RosterAttribute.Curl),
                Heading = G(RosterAttribute.Heading),
                Composure = G(RosterAttribute.Composure),
                StandingTackle = G(RosterAttribute.StandingTackle),
                SlidingTackle = G(RosterAttribute.SlidingTackle),
                Aggression = G(RosterAttribute.Aggression),
                GkDiving = G(RosterAttribute.GkDiving),
                GkHandling = G(RosterAttribute.GkHandling),
                GkReflexes = G(RosterAttribute.GkReflexes),
                GkPositioning = G(RosterAttribute.GkPositioning),
                WeakFoot = p.WeakFoot,
                PreferredFoot = p.PreferredFoot == PreferredFoot.Left ? SimFoot.Left : SimFoot.Right,
                Height = p.HeightCm / 100f,
                IsGoalkeeper = p.PrimaryPosition == RosterPosition.GK,
            };
            return a;
        }

        /// <summary>Roster position that best describes a team AI slot.</summary>
        public static RosterPosition PositionFor(TeamAISlot slot)
        {
            bool left = slot.Lateral < -0.1f, right = slot.Lateral > 0.1f;
            switch (slot.Role)
            {
                case TeamAIRole.GK: return RosterPosition.GK;
                case TeamAIRole.CB: return RosterPosition.CB;
                case TeamAIRole.FB: return left ? RosterPosition.LB : RosterPosition.RB;
                case TeamAIRole.DM: return RosterPosition.CDM;
                case TeamAIRole.CM: return RosterPosition.CM;
                case TeamAIRole.AM: return RosterPosition.CAM;
                case TeamAIRole.W: return left ? RosterPosition.LW : (right ? RosterPosition.RW : RosterPosition.CAM);
                default: return RosterPosition.ST;
            }
        }

        /// <summary>
        /// Picks the XI slot by slot (template order; keeper first) taking the best unused player by
        /// OverallTenthsAt(slot position), ties to the roster order. Deterministic.
        /// </summary>
        public static RosterTeamSheet BuildSheet(RosterClub club, PitchSpec pitch = null)
        {
            if (club == null) throw new ArgumentNullException(nameof(club));
            if (club.Players.Count < MatchSetup.PlayersPerTeam) throw new ArgumentException("club needs 11 players");
            pitch = pitch ?? PitchSpec.Standard;
            var sheet = new RosterTeamSheet { Club = club };
            var formation = ParseFormation(club.Formation);
            float press = 0.35f + 0.1f * club.Reputation;
            sheet.Tactics = new TeamAITactics(formation, 0.5f, 0.4f + 0.04f * club.Reputation, press);

            var used = new bool[club.Players.Count];
            int strikerSlot = -1;
            for (int s = 0; s < MatchSetup.PlayersPerTeam; s++)
            {
                var slot = TeamAIFormations.GetSlot(formation, s);
                var pos = PositionFor(slot);
                int best = -1, bestScore = int.MinValue;
                for (int i = 0; i < club.Players.Count; i++)
                {
                    if (used[i]) continue;
                    var p = club.Players[i];
                    bool isGk = p.PrimaryPosition == RosterPosition.GK;
                    if ((s == 0) != isGk && HasAlternative(club, used, s == 0)) continue;
                    int score = p.OverallTenthsAt(pos) + (p.CanPlay(pos) ? 15 : 0);
                    if (score > bestScore) { bestScore = score; best = i; }
                }
                used[best] = true;
                sheet.Starters[s] = club.Players[best];
                sheet.Attributes[s] = ToSimAttributes(club.Players[best]);
                if (s == 0) sheet.Attributes[s].IsGoalkeeper = true;
                sheet.Formation[s] = KickOffSpot(slot, pitch, s);
                if (slot.Role == TeamAIRole.ST && strikerSlot < 0) strikerSlot = s;
            }
            sheet.KickOffTakerSlot = strikerSlot > 0 ? strikerSlot : 10;
            return sheet;
        }

        private static bool HasAlternative(RosterClub club, bool[] used, bool wantGk)
        {
            for (int i = 0; i < club.Players.Count; i++)
                if (!used[i] && (club.Players[i].PrimaryPosition == RosterPosition.GK) == wantGk) return true;
            return false;
        }

        /// <summary>Kick-off spot for a template slot: own half, outside the centre circle (sim attack frame).</summary>
        public static SimVec3 KickOffSpot(TeamAISlot slot, PitchSpec pitch, int index)
        {
            if (slot.Role == TeamAIRole.GK) return new SimVec3(0f, 0f, -pitch.HalfLength + 2.5f);
            float x = slot.Lateral * pitch.HalfWidth * 0.72f;
            float z = -36f + slot.BlockDepth * 30f;
            // Keep out of the centre circle (radius 9.15 m) with a margin.
            float r = (float)Math.Sqrt(x * x + z * z);
            if (r < 10.5f)
            {
                if (Math.Abs(x) < 1f) z = -10.5f;
                else z = -(float)Math.Sqrt(Math.Max(0.0, 10.5 * 10.5 - x * x)) - 0.5f;
            }
            return new SimVec3(x, 0f, Math.Min(z, -1f));
        }

        /// <summary>Full match setup for two clubs. Names/ids are indexed by sim player id (0-21).</summary>
        public static MatchSetup BuildSetup(RosterClub home, RosterClub away, ulong seed,
            out RosterTeamSheet homeSheet, out RosterTeamSheet awaySheet, out string[] names, out string[] ids)
        {
            homeSheet = BuildSheet(home);
            awaySheet = BuildSheet(away);
            var setup = new MatchSetup
            {
                Seed = seed,
                Home = homeSheet.Attributes,
                Away = awaySheet.Attributes,
                HomeFormation = homeSheet.Formation,
                AwayFormation = awaySheet.Formation,
                KickOffTakerSlot = homeSheet.KickOffTakerSlot,
            };
            names = new string[MatchSimState.PlayerCount];
            ids = new string[MatchSimState.PlayerCount];
            for (int s = 0; s < MatchSetup.PlayersPerTeam; s++)
            {
                names[s] = homeSheet.Starters[s].KnownAs;
                ids[s] = homeSheet.Starters[s].Id;
                names[s + MatchSetup.PlayersPerTeam] = awaySheet.Starters[s].KnownAs;
                ids[s + MatchSetup.PlayersPerTeam] = awaySheet.Starters[s].Id;
            }
            return setup;
        }

        /// <summary>
        /// Loads the bundled leagues plus any user leagues. Order: shipped league packs (Rosters/Packs/*.roster.json,
        /// by file name, so the Premier League pack is the default when present), then the fictional default league,
        /// then the user's local files by name. Appends every failure report to <paramref name="problems"/>.
        /// </summary>
        public static List<RosterLeague> LoadLeagues(string streamingAssetsRoot, string documentsDir, List<string> problems)
        {
            var sources = new List<IRosterSource>();
            if (streamingAssetsRoot != null)
            {
                sources.AddRange(BundledRosterSource.DiscoverPacks(streamingAssetsRoot));
                sources.Add(new BundledRosterSource(streamingAssetsRoot));
            }
            sources.AddRange(LocalFileRosterSource.Discover(documentsDir));
            var leagues = new List<RosterLeague>();
            foreach (var src in sources)
            {
                var r = src.Load();
                if (r.Success) leagues.Add(r.League);
                else problems?.Add(r.Describe());
            }
            return leagues;
        }
    }
}
