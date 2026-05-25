@echo off
REM Resume BankGuard training from checkpoint with the current curriculum YAML.
REM Run this from any terminal; uses the mlagents-bank conda env.

call C:\Users\marwa\anaconda3\Scripts\activate.bat mlagents-bank

cd /d C:\VR

mlagents-learn config\BankGuard_Curriculum.yaml ^
    --run-id=BankGuard_Curr_v2 ^
    --env=C:\VR\Builds\VR_project.exe ^
    --no-graphics ^
    --resume ^
    --num-envs=1 ^
    > training.log 2> training.err

echo.
echo Training exited. See training.log / training.err.
pause
