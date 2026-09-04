import hashlib
import json
import pathlib
import subprocess
import unittest


TEST_ROOT = pathlib.Path(__file__).resolve().parent
REPOSITORY_ROOT = TEST_ROOT.parents[2]
FREEZE = REPOSITORY_ROOT / "benchmarks" / "scenarios" / "wp13-e1" / "freeze-receipt-v1.json"
DIVERGENCE = (
    REPOSITORY_ROOT / "benchmarks" / "scenarios" / "wp13-e1" / "source-divergence-v1.json"
)


def _load(path):
    return json.loads(path.read_text(encoding="utf-8"))


class Wp13E1SourceDivergenceTests(unittest.TestCase):
    """The E1 provenance chain must survive WP14 development without loosening.

    A frozen source file may only differ from the working tree when the change is
    declared here and the exact frozen bytes are still recoverable from git.
    """

    @classmethod
    def setUpClass(cls):
        cls.freeze = _load(FREEZE)
        cls.divergence = _load(DIVERGENCE)
        cls.frozen_by_path = {
            record["path"]: record for record in cls.freeze["repositoryFiles"]
        }

    def test_every_declared_divergence_names_a_frozen_file(self):
        for entry in self.divergence["divergences"]:
            self.assertIn(entry["path"], self.frozen_by_path)

    def test_every_declared_divergence_pins_the_exact_frozen_hash(self):
        for entry in self.divergence["divergences"]:
            record = self.frozen_by_path[entry["path"]]
            self.assertEqual(entry["frozenSha256"], record["sha256"])
            self.assertEqual(entry["frozenLengthBytes"], record["lengthBytes"])

    def test_every_declared_divergence_names_the_ticket_and_reason(self):
        for entry in self.divergence["divergences"]:
            self.assertTrue(entry["changedByTicket"].strip())
            self.assertTrue(entry["reason"].strip())

    def test_frozen_bytes_are_still_recoverable_for_every_divergence(self):
        commit = self.divergence["recoveryCommit"]

        for entry in self.divergence["divergences"]:
            recovered = subprocess.run(
                ["git", "show", f"{commit}:{entry['path']}"],
                cwd=REPOSITORY_ROOT,
                capture_output=True,
                check=False,
            )
            self.assertEqual(recovered.returncode, 0, entry["path"])
            self.assertEqual(
                hashlib.sha256(recovered.stdout).hexdigest(),
                entry["frozenSha256"],
                entry["path"],
            )

    def test_undeclared_frozen_files_still_match_the_working_tree(self):
        """Anything not declared must be byte-identical on disk, as before."""
        declared = {entry["path"] for entry in self.divergence["divergences"]}
        drifted = []

        for record in self.freeze["repositoryFiles"]:
            if record["path"] in declared:
                continue
            path = REPOSITORY_ROOT / record["path"]
            if (
                not path.is_file()
                or path.stat().st_size != record["lengthBytes"]
                or hashlib.sha256(path.read_bytes()).hexdigest() != record["sha256"]
            ):
                drifted.append(record["path"])

        self.assertEqual(drifted, [], "undeclared drift in frozen E1 source")

    def test_the_declaration_stays_small_enough_to_read(self):
        """A growing list is a signal to re-freeze, not to keep appending."""
        self.assertLessEqual(len(self.divergence["divergences"]), 8)


if __name__ == "__main__":
    unittest.main()
