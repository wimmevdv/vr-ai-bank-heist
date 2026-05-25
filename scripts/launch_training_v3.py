"""
Launch mlagents-learn for the v3 run with torch.onnx.export monkey-patched
to use the legacy exporter (dynamo=False). This lets us enable
checkpoint_interval safely - the auto-ONNX-on-checkpoint won't crash from
missing onnxscript.

IMPORTANT: On Windows, mlagents uses multiprocessing.spawn for env workers.
spawn re-imports the launcher in each child, so the mlagents_main() call
must live behind `if __name__ == "__main__"` or every child re-enters
training and crashes with the "freeze_support" error.
"""
import sys
import multiprocessing


def _install_legacy_onnx_export():
    import torch
    _orig_onnx_export = torch.onnx.export

    def _legacy_onnx_export(*args, **kwargs):
        kwargs.setdefault("dynamo", False)
        return _orig_onnx_export(*args, **kwargs)

    torch.onnx.export = _legacy_onnx_export


if __name__ == "__main__":
    multiprocessing.freeze_support()
    _install_legacy_onnx_export()
    from mlagents.trainers.learn import main as mlagents_main
    sys.exit(mlagents_main())
