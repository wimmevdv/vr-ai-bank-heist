"""
Convert the latest ML-Agents .pt checkpoint to .onnx for use in Unity.

mlagents' built-in ONNX export tries to use onnxscript (not installed in our
env, and installing it cascades into a protobuf upgrade that breaks training).
We bypass that by loading the policy directly from the .pt and calling
torch.onnx.export ourselves.

Usage:
    C:\\Users\\marwa\\anaconda3\\envs\\mlagents-bank\\python.exe C:\\VR\\scripts\\convert_to_onnx.py
"""

import argparse
import sys
from pathlib import Path

import torch

# torch 2.12's torch.onnx.export defaults to dynamo=True, which requires
# onnxscript. We don't have onnxscript installed (it would force a protobuf
# upgrade that breaks mlagents). Monkey-patch to force the legacy exporter.
_orig_onnx_export = torch.onnx.export
def _legacy_onnx_export(*args, **kwargs):
    kwargs.setdefault("dynamo", False)
    return _orig_onnx_export(*args, **kwargs)
torch.onnx.export = _legacy_onnx_export

# Importing the PPO trainer module populates the trainer registry, so that
# TrainerSettings(trainer_type='ppo') doesn't KeyError on construction.
import mlagents.trainers.ppo.trainer  # noqa: F401
from mlagents.trainers.ppo.optimizer_torch import PPOSettings
from mlagents.trainers.settings import TrainerSettings, NetworkSettings
from mlagents.trainers.torch_entities.model_serialization import ModelSerializer
from mlagents.trainers.policy.torch_policy import TorchPolicy
from mlagents_envs.base_env import BehaviorSpec, ActionSpec, ObservationSpec, DimensionProperty, ObservationType


def find_latest_pt(run_dir: Path, behavior: str) -> Path:
    candidates = sorted(run_dir.glob(f"{behavior}/*.pt"))
    if not candidates:
        raise FileNotFoundError(f"No .pt files under {run_dir}/{behavior}")

    def stepkey(p: Path):
        stem = p.stem
        digits = "".join(ch for ch in stem if ch.isdigit())
        return int(digits) if digits else 0
    candidates.sort(key=stepkey)
    return candidates[-1]


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--results-dir", default=r"C:\VR\results")
    p.add_argument("--run", default="BankGuard_Curr_v2")
    p.add_argument("--behavior", default="BankGuardAgent")
    p.add_argument("--out", default=None)
    args = p.parse_args()

    run_dir = Path(args.results_dir) / args.run
    pt_path = find_latest_pt(run_dir, args.behavior)
    out_path = Path(args.out) if args.out else pt_path.with_suffix(".onnx")

    print(f"[convert] Loading {pt_path}")
    state = torch.load(str(pt_path), map_location="cpu", weights_only=False)
    print(f"[convert] State keys: {list(state.keys())}")

    # The .pt file stores the policy state_dict under 'Policy' and also has
    # 'modules' or similar. The cleanest path is to rebuild the policy with
    # the original trainer settings and load the state.
    #
    # However we don't know the exact settings. Try to read them from the
    # `configuration.yaml` mlagents writes next to checkpoints.
    config_yaml = run_dir / "configuration.yaml"
    if not config_yaml.exists():
        print(f"[convert] ERROR: cannot find {config_yaml}")
        sys.exit(2)

    import yaml
    with config_yaml.open() as f:
        cfg = yaml.safe_load(f)

    behavior_cfg = cfg["behaviors"][args.behavior]
    print(f"[convert] hidden_units={behavior_cfg['network_settings']['hidden_units']} "
          f"num_layers={behavior_cfg['network_settings']['num_layers']}")

    # Build a minimal BehaviorSpec. We need observation shapes and action spec.
    # Read these from the .pt — the policy stores them.
    # Fallback: hardcode for our project (51 obs, 2 continuous actions).
    obs_count = behavior_cfg.get("network_settings", {}).get("num_layers", 2)
    print(f"[convert] Building TorchPolicy ...")

    network_settings = NetworkSettings(**behavior_cfg["network_settings"])

    # BankGuardAgent has TWO observation sources:
    # - RayPerceptionSensor3D: 13 rays * 5 floats = 65 obs (processor 0)
    # - VectorSensor (CollectObservations): 51 obs (processor 1)
    # Plus 2 continuous actions + 1 discrete action (size 1).
    obs_specs = [
        ObservationSpec(
            shape=(65,),
            dimension_property=(DimensionProperty.NONE,),
            observation_type=ObservationType.DEFAULT,
            name="RayPerceptionSensor",
        ),
        ObservationSpec(
            shape=(51,),
            dimension_property=(DimensionProperty.NONE,),
            observation_type=ObservationType.DEFAULT,
            name="VectorSensor_size51",
        ),
    ]
    action_spec = ActionSpec(
        continuous_size=2,
        discrete_branches=(1,),
    )
    behavior_spec = BehaviorSpec(obs_specs, action_spec)

    # Build the same actor used at train time.
    from mlagents.trainers.torch_entities.networks import SimpleActor
    actor_kwargs = {
        "conditional_sigma": False,
        "tanh_squash": False,
    }
    policy = TorchPolicy(
        seed=0,
        behavior_spec=behavior_spec,
        network_settings=network_settings,
        actor_cls=SimpleActor,
        actor_kwargs=actor_kwargs,
    )
    policy.load_weights(state["Policy"])

    serializer = ModelSerializer(policy)
    serializer.export_policy_model(str(out_path))

    if out_path.exists():
        print(f"[convert] OK -> {out_path}  ({out_path.stat().st_size/1024:.1f} KB)")
    else:
        print("[convert] FAILED - no .onnx written")
        sys.exit(1)


if __name__ == "__main__":
    main()
