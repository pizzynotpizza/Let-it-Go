@echo off
rem Creates the Python venv the game uses for MediaPipe hand tracking.
rem Needs Python 3.13 (MediaPipe has no wheels for 3.15 yet). Run once per machine.
cd /d "%~dp0..\Let it Go"
py -3.13 -m venv HandTrackerEnv || goto :err
HandTrackerEnv\Scripts\python -m pip install --upgrade pip
HandTrackerEnv\Scripts\python -m pip install --upgrade mediapipe opencv-python || goto :err
HandTrackerEnv\Scripts\python -c "import mediapipe; print('mediapipe', mediapipe.__version__, 'OK')"
echo.
echo Test the camera: HandTrackerEnv\Scripts\python Assets\StreamingAssets\HandTracker\hand_tracker.py --preview --no-heartbeat
pause
exit /b 0
:err
echo Setup failed. Install Python 3.13 from python.org and try again.
pause
exit /b 1
