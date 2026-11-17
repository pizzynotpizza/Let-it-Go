@echo off
rem Double-click to start the MediaPipe hand tracker before opening Unity.
rem First run on a new computer creates HandTrackerEnv (needs internet, a few minutes).
rem Add --update to upgrade the packages, --setup-only to only install.
chcp 65001 >nul
cd /d "%~dp0"
title Let it Go - Hand Tracker
where py >nul 2>nul
if %errorlevel%==0 (
    py -3 run_hand_tracker.py %*
) else (
    python run_hand_tracker.py %*
)
if errorlevel 1 pause
