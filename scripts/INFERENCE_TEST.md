# Inference Validation Checklist

After training finishes, follow this to confirm the policy actually works
before integrating into the real VR scene.

## 1 — Generate the .onnx
```
conda activate mlagents-bank
python C:\VR\scripts\convert_to_onnx.py
```
Expect: `[convert] OK → C:\VR\results\BankGuard_Curr_v2\BankGuardAgent\<step>.onnx`

## 2 — Wire it into Unity
1. Open scene `Assets/Scenes/BankGuardTestArena.unity`
2. Select the **BankGuardAgent** GameObject inside the EnvRoot prefab
3. In **Behavior Parameters**:
   - `Model` → drag the .onnx from `Assets/` (copy it under `Assets/Models/`
     first so Unity imports it as an NNModel asset)
   - `Behavior Type` → **Inference Only**
   - `Inference Device` → CPU (GPU is rarely faster for these tiny nets)
4. Disable the `Decision Requester`'s "Take Actions Between Decisions" if you
   want pure inference behavior; otherwise leave defaults.
5. **Disable curriculum side-effects** for the test: in HeistEnvController
   inspector, manually set:
   - `episodeSeconds` = 120 (give the guard time to demonstrate)
   - Enable all 6 deposits (set their GameObjects active)
   - Set `thief` GameObject **inactive** for the first test (you want to see
     the guard navigate first, before adding a moving threat)

## 3 — What "working" looks like
Press Play. Within 60s you should observe:
- [ ] Guard turns toward the nearest visible deposit
- [ ] Guard walks (not jitters) to that deposit
- [ ] Guard does NOT spin in place or get stuck on walls
- [ ] Guard re-targets after reaching a deposit (Stage 2+ behavior)
- [ ] Standard deviation of position is high — the guard explores, not loops

## 4 — Failure modes & what they mean
| Symptom | Likely cause |
|---|---|
| Guard spins or vibrates in place | Action output is saturated; check `normalize: true` in trainer config matches what was used |
| Guard walks into a wall and stays | Ray sensors aren't being read; verify `RayPerceptionSensor3D` is on the same GameObject as the Agent |
| Mean reward in inference << training | BehaviorName mismatch — the .onnx is loaded but actions are random because the runtime can't match it |
| `NullReferenceException` on first decision | The Agent's observation count doesn't match the .onnx input — only happens if you edited the agent script after training |

## 5 — Re-enable thief for full test
Once basic navigation looks right:
- Set thief GameObject active
- Set HeistEnvController distractor noise on
- Set 1 random deposit alarmed
- Now you're testing what the agent learned in S4–S6
