@echo off
REM handy-paste.cmd - Handy "External Script" paste hook (Windows).
REM
REM Handy invokes:  handy-paste.cmd "<transcript>"  and blocks until it exits.
REM Handy pastes nothing on this path, so this script does both:
REM   1. POST the text to the game's local REST endpoint
REM   2. put it on the clipboard and send Ctrl+V to the foreground window,
REM      unless the game answered {"handled":true}, meaning it consumed the text
REM
REM The text is handed to PowerShell through an environment variable, so no
REM quoting in the transcript can break the command line.
REM
REM Ships with the se-speech-to-text plugin; the plugin's config dialog
REM shows the full path to copy. Handy hides the External Script option in the
REM Windows UI, so with Handy closed set these in
REM %APPDATA%\com.pais.handy\settings_store.json:
REM     "paste_method": "external_script",
REM     "external_script_path": "<path from the plugin's config dialog>"
REM
REM Only needed if Handy has no Webhook paste method: a Handy that offers one
REM can post to the plugin directly, using the URL from the same dialog, and
REM offers it in the UI on Windows too.
REM
REM Config (set these in the environment to override):
REM   HANDY_HOOK_PORT     listener port                       (default 5115)
REM   HANDY_HOOK_URL      full endpoint, overrides PORT
REM   HANDY_HOOK_TIMEOUT  whole seconds to wait for the API   (default 1)
REM   HANDY_HOOK_LOG      log file; Handy discards our stdio  (default none)
REM   HANDY_HOOK_PASTE    0 = notify only, do not paste       (default 1)
REM   HANDY_HOOK_DELAY    ms between clipboard and Ctrl+V     (default 50)

setlocal EnableExtensions DisableDelayedExpansion

if "%~1"=="" exit /b 0
set "HANDY_TEXT=%~1"

if not defined HANDY_HOOK_PORT    set "HANDY_HOOK_PORT=5115"
if not defined HANDY_HOOK_URL     set "HANDY_HOOK_URL=http://127.0.0.1:%HANDY_HOOK_PORT%/handy"
if not defined HANDY_HOOK_TIMEOUT set "HANDY_HOOK_TIMEOUT=1"
if not defined HANDY_HOOK_PASTE   set "HANDY_HOOK_PASTE=1"
if not defined HANDY_HOOK_DELAY   set "HANDY_HOOK_DELAY=50"

REM Built up in fragments for readability. Keep every fragment free of double
REM quotes and percent signs - PowerShell single quotes only.
set "PS=$ErrorActionPreference='SilentlyContinue';"
set "PS=%PS% $t=$env:HANDY_TEXT;"
set "PS=%PS% function Log($m){ if($env:HANDY_HOOK_LOG){ Add-Content -LiteralPath $env:HANDY_HOOK_LOG -Value ((Get-Date).ToString('o')+' '+$m) -Encoding UTF8 } };"

REM 1. Preliminary warning, strictly before the text reaches the clipboard.
set "PS=%PS% $body=[Text.Encoding]::UTF8.GetBytes((@{source='handy';event='pre-paste';text=$t;timestamp=(Get-Date).ToString('o')} | ConvertTo-Json -Compress));"
set "PS=%PS% $handled=$false; [Net.ServicePointManager]::Expect100Continue=$false;"
set "PS=%PS% try{ $r=Invoke-RestMethod -Uri $env:HANDY_HOOK_URL -Method Post -ContentType 'application/json; charset=utf-8' -Body $body -TimeoutSec ([int]$env:HANDY_HOOK_TIMEOUT); $handled=($r.handled -eq $true); Log ('notified '+$env:HANDY_HOOK_URL+' handled='+$handled+' ('+$t.Length+' chars)') }"
set "PS=%PS% catch{ Log ('notify failed: '+$_.Exception.Message+' - pasting anyway') };"

REM 2. The paste itself, unless the game took the text.
set "PS=%PS% if($env:HANDY_HOOK_PASTE -ne '0' -and -not $handled){"
set "PS=%PS%   try{ Set-Clipboard -Value $t }catch{ Add-Type -AssemblyName System.Windows.Forms; [Windows.Forms.Clipboard]::SetText($t) };"
set "PS=%PS%   Start-Sleep -Milliseconds ([int]$env:HANDY_HOOK_DELAY);"
set "PS=%PS%   Add-Type -AssemblyName System.Windows.Forms;"
set "PS=%PS%   [Windows.Forms.SendKeys]::SendWait('^v')"
set "PS=%PS% }"

powershell.exe -NoProfile -NonInteractive -Sta -ExecutionPolicy Bypass -Command "%PS%"

endlocal
exit /b 0
