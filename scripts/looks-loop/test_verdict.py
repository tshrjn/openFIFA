"""Regression tests for verdict.decide: python3 scripts/looks-loop/test_verdict.py"""
import unittest
from verdict import decide

S = lambda o: {"face": o, "hair": o, "skin": o, "body": o, "overall": o}


class DecideTests(unittest.TestCase):
    def test_incomplete_ballots_do_not_count(self):
        # GPT's council example: two incomplete A-preferring ballots must not outvote one complete ballot.
        bad = {"A": {"overall": 10}, "better": "A"}
        good = {"A": S(1), "B": S(2), "better": "B"}
        v = decide({"claude": bad, "gpt": dict(bad), "gemini": good}, "A")
        self.assertFalse(v["keep"])
        self.assertEqual(v["answered"], 1)
        self.assertEqual(sorted(v["invalid"]), ["claude", "gpt"])
        self.assertEqual(v["cand_mean"]["overall"], 1.0)

    def test_out_of_range_and_bad_enum_rejected(self):
        v = decide({"claude": {"A": S(11), "B": S(2), "better": "A"},
                    "gpt": {"A": S(3), "B": S(2), "better": "a"},
                    "gemini": {"A": S(3), "B": S(2), "better": "A"}}, "A")
        self.assertEqual(v["answered"], 1)
        self.assertFalse(v["keep"])  # fewer than two valid ballots

    def test_majority_keeps(self):
        v = decide({"claude": {"A": S(4), "B": S(3), "better": "A"},
                    "gpt": {"A": S(4), "B": S(3), "better": "A"},
                    "gemini": {"A": S(3), "B": S(3), "better": "tie"}}, "A")
        self.assertTrue(v["keep"])
        self.assertEqual(v["votes"], 2)

    def test_tie_counts_against(self):
        v = decide({"claude": {"A": S(4), "B": S(3), "better": "A"},
                    "gpt": {"A": S(3), "B": S(3), "better": "tie"}}, "A")
        self.assertFalse(v["keep"])


if __name__ == "__main__":
    unittest.main()
