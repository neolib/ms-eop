@echo off
setlocal
goto :start

:build
echo Building in %1
cd /d %1
if errorlevel 1 (call :no_dir %1
) else (
getdeps /build:latest
build -cC
timeout 3)
echo.
goto :EOF

:no_dir
echo Direcotry not found!
goto :EOF

:start
