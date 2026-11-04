using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenFifa.Core;

/// <summary>
/// Headless commentary run: plays an AI v AI match with the real sim, adapter and engine (with the rendered-clip index
/// as catalog) and writes every cue as one JSON line, for tools/commentary/mix_demo.py to turn into audio.
///   CommentaryDemo &lt;projectRoot&gt; &lt;out.jsonl&gt; [seed] [halfSeconds] [homeClub] [awayClub]
/// </summary>
public static class CommentaryDemo
{
    public static int Main(string[] a)
    {
        string project = a[0], outPath = a[1];
        ulong seed = a.Length > 2 ? ulong.Parse(a[2]) : 7;
        float half = a.Length > 3 ? float.Parse(a[3], CultureInfo.InvariantCulture) : 120f;
        int hi = a.Length > 4 ? int.Parse(a[4]) : 0, ai = a.Length > 5 ? int.Parse(a[5]) : 1;
        string sa = Path.Combine(project, "Assets", "StreamingAssets");

        var bank = CommentaryLineBank.Load(File.ReadAllText(Path.Combine(sa, "Commentary", "lines.en.json")), out var errors);
        if (bank == null) { Console.Error.WriteLine(string.Join("\n", errors)); return 1; }
        string indexPath = Path.Combine(sa, "Commentary", "audio", "en", "index.json");
        var index = File.Exists(indexPath) ? CommentaryAudioIndex.Parse(File.ReadAllText(indexPath), out _) : null;

        var problems = new List<string>();
        var clubs = RosterMatchBuilder.LoadLeagues(sa, null, problems)[0].Clubs;
        var home = clubs[hi];
        var away = clubs[ai];
        var options = new SinglePlayerMatchOptions
        {
            Seed = seed,
            Config = new MatchSimConfig { HalfDurationSeconds = half },
            Setup = RosterMatchBuilder.BuildSetup(home, away, seed, out var hs, out var aws, out var names, out var ids),
            HomeTactics = hs.Tactics,
            AwayTactics = aws.Tactics,
            PlayerNames = names,
            PlayerIds = ids,
            TeamNames = new[] { home.Name, away.Name },
        };
        var session = new SinglePlayerMatchSession(options);
        var engine = new CommentaryEngine(bank, null, (uint)seed | 1u) { Catalog = index };
        session.AttachCommentaryEngine(engine);

        var sb = new StringBuilder();
        sb.Append("{\"match\":\"").Append(home.Name).Append(" v ").Append(away.Name).Append("\",\"half\":")
          .Append(half.ToString(CultureInfo.InvariantCulture)).Append(",\"audio\":").Append(index != null ? "true" : "false").Append("}\n");
        const double dt = 1.0 / 30.0;
        double t = 0;
        float crowd = 0.25f;
        while (!session.IsFinished && t < half * 2 + 600)
        {
            engine.SetCrowdIntensity(crowd);
            session.Advance(dt, null, t);
            foreach (var ev in session.FrameEvents)
            {
                switch (ev.Type)
                {
                    case MatchEventType.Goal: crowd = 1f; break;
                    case MatchEventType.PostHit:
                    case MatchEventType.CrossbarHit:
                    case MatchEventType.Save: crowd = Math.Max(crowd, 0.75f); break;
                    case MatchEventType.Shot: crowd = Math.Max(crowd, 0.55f); break;
                    case MatchEventType.Foul: crowd = Math.Max(crowd, 0.45f); break;
                }
                if (ev.Type == MatchEventType.Goal || ev.Type == MatchEventType.Shot || ev.Type == MatchEventType.Save ||
                    ev.Type == MatchEventType.HalfTime || ev.Type == MatchEventType.FullTime)
                    sb.Append("{\"t\":").Append(t.ToString("F3", CultureInfo.InvariantCulture)).Append(",\"event\":\"")
                      .Append(ev.Type).Append("\",\"score\":\"").Append(session.Sim.State.HomeScore).Append("-")
                      .Append(session.Sim.State.AwayScore).Append("\"}\n");
            }
            crowd = Math.Max(0.25f, crowd - 0.12f * (float)dt);
            foreach (var cue in session.FrameCues)
            {
                var u = cue.Utterance;
                sb.Append("{\"t\":").Append(t.ToString("F3", CultureInfo.InvariantCulture))
                  .Append(",\"cue\":\"").Append(cue.Kind).Append("\",\"voice\":\"")
                  .Append(u.Voice == CommentaryVoice.Analyst ? "analyst" : "lead").Append("\",\"category\":\"").Append(u.Category.Id)
                  .Append("\",\"crowd\":").Append(crowd.ToString("F2", CultureInfo.InvariantCulture));
                if (cue.Kind == CommentaryCueKind.Start)
                {
                    sb.Append(",\"clips\":[");
                    for (int i = 0; i < u.Clips.Count; i++) sb.Append(i > 0 ? "," : "").Append('"').Append(u.Clips[i].ClipId).Append('"');
                    sb.Append("],\"text\":\"").Append(u.Transcript.Replace(" | ", " ").Replace("\"", "\\\"")).Append('"');
                }
                sb.Append("}\n");
            }
            t += dt;
        }
        File.WriteAllText(outPath, sb.ToString());
        Console.WriteLine(home.Name + " " + session.Sim.State.HomeScore + "-" + session.Sim.State.AwayScore + " " + away.Name +
                          ", " + t.ToString("F0") + " s, audio index: " + (index != null ? index.Count + " clips" : "none"));
        return 0;
    }
}
