@echo off
setlocal
cd "%~dp0"
nuget restore -PackagesDirectory packages
