@echo off
REM v5 training in kean_scene_AItraining (production bank layout).
REM LSTM memory + random deposit positions + proximity reward.

call C:\Users\marwa\anaconda3\Scripts\activate.bat mlagents-bank

cd /d C:\VR

python scripts\launch_training_v3.py ^
    config\BankGuard_v5_kean.yaml ^
    --run-id=BankGuard_v5_kean ^
    --env=C:\VR\Builds\VR_project.exe ^
    --no-graphics ^
    --num-envs=1 ^
    --timeout-wait=300 ^
    --force ^
    > training.log 2> training.err

echo.
echo Training exited. See training.log / training.err.
