@echo off
setlocal
cd "%~dp0"
call prestore
if not %errorlevel%==0 exit /b %errorlevel%
set msbuild=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\msbuild
for %%s in (*.sln) do for %%c in (Debug Release) do "%msbuild%" /p:Configuration=%%c %%s
