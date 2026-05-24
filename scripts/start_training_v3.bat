@echo off
REM Start a FRESH v3 training run. Uses the monkey-patched launcher so
REM checkpoint_interval can write .pt every 1M steps without crashing on
REM the missing onnxscript module.

call C:\Users\marwa\anaconda3\Scripts\activate.bat mlagents-bank

cd /d C:\VR

python scripts\launch_training_v3.py ^
    config\BankGuard_Curriculum_v3.yaml ^
    --run-id=BankGuard_Curr_v3 ^
    --env=C:\VR\Builds\VR_project.exe ^
    --no-graphics ^
    --num-envs=1 ^
    --timeout-wait=180 ^
    --force ^
    > training.log 2> training.err

echo.
echo Training exited. See training.log / training.err.
