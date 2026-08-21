import importlib.util
import pathlib
import tempfile
import unittest


_ROOT = pathlib.Path(__file__).parents[1]
_SPEC = importlib.util.spec_from_file_location(
    "wp9_freeze_verify",
    _ROOT / "wp9_freeze_verify.py",
)
_MODULE = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_MODULE)


class FreezeVerifierTests(unittest.TestCase):
    def test_tree_seal_binds_paths_lengths_and_bytes_deterministically(self):
        with tempfile.TemporaryDirectory() as first_directory, tempfile.TemporaryDirectory() as second_directory:
            first = pathlib.Path(first_directory)
            second = pathlib.Path(second_directory)
            (first / "b").write_bytes(b"two")
            (first / "a").write_bytes(b"one")
            (second / "a").write_bytes(b"one")
            (second / "b").write_bytes(b"two")

            first_hash = _MODULE._tree_sha256(first, b"domain")
            second_hash = _MODULE._tree_sha256(second, b"domain")
            self.assertEqual(first_hash, second_hash)

            (second / "b").write_bytes(b"changed")
            self.assertNotEqual(
                first_hash,
                _MODULE._tree_sha256(second, b"domain"),
            )

    def test_tree_seal_exclusion_is_explicit(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / "data").write_bytes(b"stable")
            (root / "receipt").write_bytes(b"first")
            before = _MODULE._tree_sha256(root, b"domain", {"receipt"})
            (root / "receipt").write_bytes(b"second")

            self.assertEqual(
                before,
                _MODULE._tree_sha256(root, b"domain", {"receipt"}),
            )


if __name__ == "__main__":
    unittest.main()
