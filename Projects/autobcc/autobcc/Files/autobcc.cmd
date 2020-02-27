@echo off
REM autobcc script for {project}
setlocal
goto :start

:build
echo Building in %1
cd /d %1
if errorlevel 1 (goto :EOF
) else (
getdeps /build:latest
build -cC)
goto :EOF

:start
