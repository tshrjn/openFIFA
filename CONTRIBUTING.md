# Contributing to openFIFA

Thanks for helping build openFIFA. The development workflow (user stories in [`prd.json`](prd.json), tests first,
coding conventions) is described in the [Contributing section of the README](README.md#contributing),
[`docs/IMPLEMENT.md`](docs/IMPLEMENT.md) and [`CLAUDE.md`](CLAUDE.md). This file covers licensing.

## License of your contribution

openFIFA is licensed under the GNU Affero General Public License v3.0 or later, with the additional permission in
[`LICENSE-EXCEPTION.md`](LICENSE-EXCEPTION.md) for the Unity engine and Apple platform SDKs. See
[README: License](README.md#license).

## Contributor License Agreement (CLA)

openFIFA is dual-licensed: it is open source under the AGPL, and the copyright holder also offers separate commercial
licenses. For that to work, every contribution needs a Contributor License Agreement.

The CLA is in [`CLA.md`](CLA.md). In short:

- You keep the copyright in your contribution.
- You grant Tushar Jain a perpetual, worldwide, irrevocable, royalty-free license to use and distribute it, including
  the right to relicense it under other terms (commercial licenses included).
- For as long as Tushar Jain distributes openFIFA under the AGPL, your contribution stays available under the AGPL
  (with the Unity exception) as part of it. Copies already released under the AGPL stay under the AGPL.
- You confirm you have the right to submit it (it is your own work, or you have your employer's permission).

**How to agree:** tick the CLA checkbox in the pull request description. The
[pull request template](.github/pull_request_template.md) includes it:

```
- [x] I have read the openFIFA Contributor License Agreement (CLA.md) and I agree to it for this and my future contributions.
```

Pull requests without the ticked box cannot be merged.

## Third-party assets and code

Third-party assets and code keep their own licenses. If your pull request adds any, list them in
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) with their source and license, and follow the asset license rules
in [`docs/ASSETS.md`](docs/ASSETS.md). Only commit code and assets that may be redistributed in source form and whose
license allows commercial use and relicensing as part of openFIFA (for example CC0, CC-BY, MIT or Apache-2.0). Do not
commit anything licensed with ShareAlike or NonCommercial terms, or anything that may only be shipped inside a built
game (for example Mixamo downloads or Unity Asset Store content).

## SPDX headers for new files

New source files should start with an SPDX license identifier:

```csharp
// SPDX-License-Identifier: AGPL-3.0-or-later
```

Use the comment syntax of the file type (`#` for shell and Python). The Unity exception in `LICENSE-EXCEPTION.md`
applies to the whole repository, so no per-file exception text is needed. There is no need to add headers to existing
files in unrelated pull requests.
