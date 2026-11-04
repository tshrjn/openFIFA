"""Serialises a commentary line bank in the repo's hand-written layout (one header line per category, one line per entry)."""
import json

HEADER_KEYS = None


def _v(x):
    return json.dumps(x, ensure_ascii=False)


def dumps(bank):
    out = ["{"]
    for k in ("format", "schemaVersion", "language", "notes"):
        if k in bank:
            out.append('  %s: %s,' % (_v(k), _v(bank[k])))
    out.append('  "categories": [')
    cats = bank["categories"]
    for ci, c in enumerate(cats):
        out.append("    {")
        head = ", ".join("%s: %s" % (_v(k), _v(v)) for k, v in c.items() if k != "lines")
        out.append("      " + head + ",")
        out.append('      "lines": [')
        lines = c["lines"]
        for li, l in enumerate(lines):
            if isinstance(l, dict):
                s = "{" + ", ".join("%s: %s" % (_v(k), _v(v)) for k, v in l.items()) + "}"
            else:
                s = _v(l)
            out.append("        " + s + ("," if li < len(lines) - 1 else ""))
        out.append("      ]")
        out.append("    }" + ("," if ci < len(cats) - 1 else ""))
    out.append("  ]")
    out.append("}")
    return "\n".join(out) + "\n"
