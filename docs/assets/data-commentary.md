# Third-party items: data and commentary area

None. Everything in the data and commentary area is original to OpenFifa:

- `OpenFifa/Assets/StreamingAssets/Rosters/default_league.json` is fictional. `tools/rosters/generate_fictional_league.py`
  generates it (seed 20261104) from invented club, ground and surname-syllable lists and common first names.
  No real league, club, crest, player or ratings data (EA FC, sofifa or anything else) was used or consulted.
- `OpenFifa/Assets/StreamingAssets/Commentary/lines.en.json`: every line is original text. No real commentator's catchphrases.
- The JSON parser/writer (`Scripts/Core/Rosters/JsonParser.cs`, `JsonWriter.cs`) was written from scratch and adds no new dependency.
- The Python tools under `tools/rosters/` and `tools/commentary/` use only the Python standard library.

No audio exists yet. When TTS clips are rendered, record the engine, the voice and its licence here.
