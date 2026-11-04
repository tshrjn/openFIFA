# PR #5 review findings (from the visual/code review thread, at 6e8a56e)

MAJOR
1. A penalty at the end of a half is cut off on contact. CheckClock only waits while PendingRestart==Penalty, and ResolveKick clears it in the same Step (Rules.cs:378, Actions.cs:279). Keep a PenaltyInProgress flag until the ball is dead, saved, scored or touched.
2. The ground-pass solver ignores most drag (KickSolver.cs:98-99 assumes RollingResistance*g+0.25, but the real figure is ~2.6 m/s^2 at 12 m/s and ~4.8 at 20). A 30 m pass at power 0 stops at 24 m, and a 30 m full-power pass takes 4.6 s. Solve by bisection against BallIntegrator, like LoftedSpeed, and add more tests.
3. AI ball-timing doesn't match the sim. TeamAIOnBall.cs:66-72 assumes v0=9+0.45d, and TeamAIMath.cs:88-93 PredictBall uses exp decay and treats lofted balls as rolling. Use the sim's ball prediction, or tables precomputed from BallIntegrator.
4. Keeper saves resolve at up to 2.5 m, and a caught ball teleports to the hands (Possession.cs:74-87, 99-107). Resolve at closest approach, within reach along the dive direction.
5. There are no ball-body or player-player collisions in Core/Sim. Balls above 1.9 m pass through players, and players overlap. Add capsule-vs-ball deflection in StepBall and a separation pass.
6. A goal after a keeper parry counts as a keeper own goal (Possession.cs:96-97 + Rules.cs:257-258). Keep Shooter/ShotLive attribution through save deflections.
7. TeamAIDifficultyLadder isn't consumed in committed code (OpponentReactionBonus is unused). Add behaviour-level difficulty tests.
MINOR
8. Direct goals from an IFK or throw-in are allowed, because ScoreGoal doesn't check LastKickRestart (Rules.cs:253).
9. Free kicks near the goal line push the wall 9.15 m off the pitch (Rules.cs:165-175). Clamp it.
10. The keeper leaves his line for a penalty against (TeamAIBrain.cs:156-159).
11. A 5* weak foot is stronger than the strong foot: 0.92+0.02*5 = 1.02 (KickSolver.cs:121). Clamp it to <=1.
12. The ball can tunnel through posts at 45 m/s (BallIntegrator.cs:199-218). Use a swept test or extra substeps near goal.
13. A comment says grounded Magnus applies, but the code doesn't apply it (BallIntegrator.cs:36).
14. Determinism holds only within one runtime, and possession ties always favour Home. Randomise the tie-break (seeded) and note the cross-runtime limit for replays.
15. There are no corner or penalty set-piece routines (TeamAIFormationShape.cs:41, TeamAIBrain.cs:534).
16. Touch: holding Sprint turns every kick into a driven one (TouchControlScheme.ResolveAttack).
17. InputActionsAssetTests.cs:20-37 pass trivially. Parse the JSON and assert action->binding pairs.
18. IosBuild.cs: Fail() exits before finally restores settings (~181); the header comment still says com.tshrjn.openfifa; an empty APPLE_TEAM_ID may swallow the next arg. (LFS for FBX: deferred to the owner.)
19. PlayerAnimationDriver snaps to the sim position with no interpolation, so a 60 Hz sim stutters on 120 Hz screens. Interpolate between snapshots. Also, FacingYaw is in degrees while the sim uses radians.
20. default_league.json:4 says "OpenFifa Premier Division" and uses openfifa-* ids. Rename these to OpenFootball (via the generator).
Also add tests for a penalty at the whistle, IFK/throw-in direct goals, save timing, long passes, and TeamAI driving MatchSim end-to-end.
