@echo off
REM Overnight automation:
REM   1) Headless rebuild of Builds\VR_project.exe with the new movement code
REM   2) Start a fresh v3 training run from scratch (NO --resume, --force)
REM
REM Run ONCE before going to sleep. The trainer keeps going until max_steps
REM (12M) or a crash; if it crashes, restart by re-running this script.

setlocal

set UNITY="C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe"
set PROJECT=C:\VR
set BUILD_LOG=C:\VR\build.log

echo === [1/2] Headless rebuild ===  > "%BUILD_LOG%"
echo Start: %DATE% %TIME%             >> "%BUILD_LOG%"
%UNITY% -batchmode -quit -nographics -projectPath "%PROJECT%" -logFile "%BUILD_LOG%" -executeMethod Wimme.EditorTools.HeadlessBuild.BuildTraining
if errorlevel 1 (
    echo BUILD FAILED. See %BUILD_LOG%.
    exit /b 1
)

echo === [2/2] Starting training ===
call C:\Users\marwa\anaconda3\Scripts\activate.bat mlagents-bank

cd /d C:\VR

python scripts\launch_training_v3.py ^
    config\BankGuard_Curriculum_v3.yaml ^
    --run-id=BankGuard_Curr_v3 ^
    --env=C:\VR\Builds\VR_project.exe ^
    --no-graphics ^
    --num-envs=1 ^
    --force ^
    > training.log 2> training.err

echo Training exited at %DATE% %TIME%. See training.log / training.err.
