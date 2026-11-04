// Headless perf benchmark for the OpenFootball match sim + team AI (pure C#, no Unity).
// Runs AI-vs-AI full matches through SinglePlayerMatchSession.StepOnce and reports per-tick cost,
// GC allocation and a final state hash (must stay identical across optimizations: no gameplay change).
using System;
using System.Diagnostics;
using OpenFifa.Core;

public static class Bench
{
    public static int Main(string[] args)
    {
        int matches = args.Length > 0 ? int.Parse(args[0]) : 3;
        ulong seed0 = args.Length > 1 ? ulong.Parse(args[1]) : 1UL;
        // Warm-up (JIT) on a short run.
        Run(seed0 + 1000, 2000, out _, out _, out _, out _);
        double totalMs = 0; long totalAlloc = 0; long totalTicks = 0; double worst = 0;
        var all = new System.Collections.Generic.List<double>();
        for (int m = 0; m < matches; m++)
        {
            var samples = Run(seed0 + (ulong)m, int.MaxValue, out long ticks, out long alloc, out ulong hash, out string score);
            double ms = 0; foreach (var s in samples) { ms += s; if (s > worst) worst = s; }
            all.AddRange(samples);
            totalMs += ms; totalAlloc += alloc; totalTicks += ticks;
            Console.WriteLine($"match seed={seed0 + (ulong)m} ticks={ticks} score={score} hash={hash:X16} ms={ms:F1} us/tick={ms * 1000 / ticks:F2} alloc/tick={alloc / (double)ticks:F1}B");
        }
        all.Sort();
        double P(double q) => all[Math.Min(all.Count - 1, (int)(q * all.Count))] * 1000;
        Console.WriteLine($"SUMMARY ticks={totalTicks} mean_us={totalMs * 1000 / totalTicks:F2} p50_us={P(0.5):F2} p99_us={P(0.99):F2} p999_us={P(0.999):F2} max_us={worst * 1000:F1} alloc_per_tick_B={totalAlloc / (double)totalTicks:F1} gen0={GC.CollectionCount(0)}");
        return 0;
    }

    static System.Collections.Generic.List<double> Run(ulong seed, int maxTicks, out long ticks, out long alloc, out ulong hash, out string score)
    {
        var session = new SinglePlayerMatchSession(new SinglePlayerMatchOptions { Seed = seed, HumanTeam = SimTeam.Home });
        var samples = new System.Collections.Generic.List<double>(400000);
        var sw = new Stopwatch();
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        ticks = 0;
        while (!session.IsFinished && ticks < maxTicks)
        {
            sw.Restart();
            session.StepOnce(null);
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
            ticks++;
        }
        alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
        hash = session.Sim.ComputeStateHash();
        score = $"{session.Sim.State.HomeScore}-{session.Sim.State.AwayScore}";
        return samples;
    }
}
