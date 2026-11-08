@echo off
rem Double-click to start the MediaPipe hand tracker before opening Unity.
rem Add --update to upgrade mediapipe to the latest version.
chcp 65001 >nul
cd /d "%~dp0"
title Let it Go - Hand Tracker
py -3 run_hand_tracker.py %*
if errorlevel 1 pause
