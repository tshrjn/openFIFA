#!/usr/bin/env python3
"""Tests for the roster tools. Run: python3 -m unittest discover -s tools/rosters -p 'test_*.py'"""

import csv
import json
import os
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "import_template"))

import generate_fictional_league as gen  # noqa: E402
import import_csv as imp  # noqa: E402
from roster_common import ATTRIBUTE_KEYS, dump_roster  # noqa: E402

EXAMPLES = os.path.join(HERE, "import_template", "examples")


class GeneratorTests(unittest.TestCase):
    def test_generate_sameSeed_identicalOutput(self):
        with tempfile.TemporaryDirectory() as d:
            a, b = os.path.join(d, "a.json"), os.path.join(d, "b.json")
            dump_roster(gen.generate(7), a)
            dump_roster(gen.generate(7), b)
            with open(a, "rb") as fa, open(b, "rb") as fb:
                self.assertEqual(fa.read(), fb.read())

    def test_generate_default_twentyClubsOfTwentyFive(self):
        doc = gen.generate(gen.DEFAULT_SEED)
        self.assertEqual(20, len(doc["clubs"]))
        for c in doc["clubs"]:
            self.assertGreaterEqual(len(c["players"]), 23)
            for p in c["players"]:
                self.assertEqual(sorted(ATTRIBUTE_KEYS), sorted(p["attributes"]))
                for v in p["attributes"].values():
                    self.assertTrue(1 <= v <= 20)

    def test_committedDefaultLeague_matchesGenerator(self):
        with tempfile.TemporaryDirectory() as d:
            out = os.path.join(d, "x.json")
            dump_roster(gen.generate(gen.DEFAULT_SEED), out)
            with open(out, "rb") as f1, open(gen.DEFAULT_OUT, "rb") as f2:
                self.assertEqual(f1.read(), f2.read(), "regenerate default_league.json")


class ImportTests(unittest.TestCase):
    def test_scale99_endpointsAndMidpoint(self):
        self.assertEqual(1, imp.to_scale20("0", 99, "t"))
        self.assertEqual(20, imp.to_scale20("99", 99, "t"))
        self.assertEqual(11, imp.to_scale20("52", 99, "t"))

    def test_scale99_outOfRange_raises(self):
        with self.assertRaises(imp.ImportError_):
            imp.to_scale20("120", 99, "t")

    def test_csvJsonCsv_roundTrip_identical(self):
        with tempfile.TemporaryDirectory() as d:
            src_p = os.path.join(EXAMPLES, "example_players_scale20.csv")
            src_c = os.path.join(EXAMPLES, "example_clubs.csv")
            doc = imp.csv_to_roster(src_p, src_c, scale=20)
            p2, c2 = os.path.join(d, "p.csv"), os.path.join(d, "c.csv")
            imp.roster_to_csv(doc, p2, c2)
            with open(src_p) as a, open(p2) as b:
                self.assertEqual(a.read(), b.read())
            with open(src_c) as a, open(c2) as b:
                self.assertEqual(a.read(), b.read())

    def test_importedExample_clubsMatchGeneratedLeague(self):
        doc = imp.csv_to_roster(os.path.join(EXAMPLES, "example_players_scale20.csv"),
                                os.path.join(EXAMPLES, "example_clubs.csv"), scale=20)
        with open(gen.DEFAULT_OUT, encoding="utf-8") as f:
            league = {c["id"]: c for c in json.load(f)["clubs"]}
        for club in doc["clubs"]:
            self.assertEqual(league[club["id"]], club)

    def test_aliasHeaders_mapToAttributes(self):
        doc = imp.csv_to_roster(os.path.join(HERE, "import_template", "players_template.csv"),
                                os.path.join(HERE, "import_template", "clubs_template.csv"), scale=99)
        striker = doc["clubs"][0]["players"][1]
        self.assertEqual(["ST", "CF"], striker["positions"])
        self.assertEqual(imp.to_scale20("82", 99, "t"), striker["attributes"]["topSpeed"])
        self.assertEqual(imp.to_scale20("76", 99, "t"), striker["attributes"]["firstTouch"])

    def test_missingAttribute_reportsColumn(self):
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "bad.csv")
            with open(path, "w", newline="") as f:
                w = csv.writer(f)
                w.writerow(["club", "last_name", "positions", "finishing"])
                w.writerow(["A", "B", "ST", "50"])
            with self.assertRaises(imp.ImportError_) as ctx:
                imp.csv_to_roster(path)
            self.assertIn("acceleration", str(ctx.exception))

    def test_groupColumns_fillMissingAttributes(self):
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "g.csv")
            with open(path, "w", newline="") as f:
                w = csv.writer(f)
                w.writerow(["club", "last_name", "positions", "pace", "shooting", "passing", "dribbling",
                            "defending", "physical", "finishing"])
                w.writerow(["A", "B", "ST", "99", "60", "60", "60", "30", "70", "0"])
            p = imp.csv_to_roster(path)["clubs"][0]["players"][0]
            self.assertEqual(20, p["attributes"]["topSpeed"])
            self.assertEqual(1, p["attributes"]["finishing"])
            self.assertEqual(3, p["attributes"]["gkDiving"])


if __name__ == "__main__":
    unittest.main()
