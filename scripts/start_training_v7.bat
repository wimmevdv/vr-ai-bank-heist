@echo off
REM v7 training in kean_scene_Training2 (real bank layout).
REM 17 obs: vertical awareness + last-known-position + LSTM memory.

call C:\Users\marwa\anaconda3\Scripts\activate.bat mlagents-bank

cd /d C:\VR

python scripts\launch_training_v3.py ^
    config\BankGuard_v7.yaml ^
    --run-id=BankGuard_v7 ^
    --env=C:\VR\Builds\VR_project.exe ^
    --no-graphics ^
    --num-envs=1 ^
    --timeout-wait=600 ^
    --resume ^
    > training_v7.log 2> training_v7.err

echo.
echo Training exited. See training_v7.log / training_v7.err.
