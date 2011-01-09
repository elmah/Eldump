@echo off
setlocal
if not exist "%~dp0packages" call "%~dp0prestore"
if not %errorlevel%==0 exit /b %errorlevel%
call :add_path elmah.corelibrary.1.2.2\lib
call :add_path Fizzler.1.0.0-beta2\lib\net35
call :add_path Fizzler.Systems.HtmlAgilityPack.1.0.0-beta2\lib\net35
call :add_path HtmlAgilityPack.1.4.6\lib\Net20
call :add_path Mannex.2.7.1\lib\net35
"%~dp0packages\IronPython.2.7.3\tools\ipy.exe" "%~dpn0.py" %*
goto :EOF

:add_path
set IRONPYTHONPATH=%IRONPYTHONPATH%;%~dp0packages\%1
goto :EOF
