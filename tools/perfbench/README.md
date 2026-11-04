# perfbench

A headless benchmark for the match sim and team AI. It plays full AI-vs-AI matches and prints µs per tick, allocation and a final state hash. A pure optimization must leave the hash unchanged.

    tools/perfbench/run.sh [matches=3] [seed=1]

It needs no Unity license, but it uses the .NET runtime and Roslyn that ship with the Unity editor (default `~/Unity/6000.3.10f1/Editor/Data`; override with `UNITY_EDITOR_DATA`). Compare means on a shared machine, since max and p999 track other load.
