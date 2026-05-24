@echo off
REM Resume v4 training from latest checkpoint. Uses the updated curriculum
REM (single-stage full task) but keeps the network weights from S1 training.

call C:\Users\marwa\anaconda3\Scripts\activate.bat mlagents-bank

cd /d C:\VR

python scripts\launch_training_v3.py ^
    config\BankGuard_Curriculum_v4.yaml ^
    --run-id=BankGuard_Curr_v4 ^
    --env=C:\VR\Builds\VR_project.exe ^
    --no-graphics ^
    --num-envs=1 ^
    --timeout-wait=180 ^
    --resume ^
    > training.log 2> training.err

echo.
echo Training exited. See training.log / training.err.
