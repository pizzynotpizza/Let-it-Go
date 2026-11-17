@echo off
rem Creates (or repairs) the Python venv the game uses for MediaPipe hand tracking.
rem Run once on every new computer. Needs Python 3.13 (or 3.12 / 3.11) from python.org.
chcp 65001 >nul
cd /d "%~dp0.."
call "Start Hand Tracker.bat" --setup-only
