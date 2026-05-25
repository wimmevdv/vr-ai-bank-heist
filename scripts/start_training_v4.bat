@echo off
REM v4 training: perception-driven obs, thief-catch primary reward, single-stage.
REM Run AFTER rebuilding Unity (HeadlessBuild) so the new BankGuardAgent CollectObservations
REM (13-dim) is baked into Builds\VR_project.exe.

call C:\Users\marwa\anaconda3\Scripts\activate.bat mlagents-bank

cd /d C:\VR

python scripts\launch_training_v3.py ^
    config\BankGuard_Curriculum_v4.yaml ^
    --run-id=BankGuard_Curr_v4 ^
    --env=C:\VR\Builds\VR_project.exe ^
    --no-graphics ^
    --num-envs=1 ^
    --timeout-wait=180 ^
    --force ^
    > training.log 2> training.err

echo.
echo Training exited. See training.log / training.err.
