@echo off
:: ==========================================
:: BƯỚC 1: TỰ ĐỘNG KIỂM TRA VÀ YÊU CẦU QUYỀN ADMIN
:: ==========================================
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"

if '%errorlevel%' NEQ '0' (
    echo Dang yeu cau quyen Administrator...
    goto UACPrompt
) else ( goto gotAdmin )

:UACPrompt
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    echo UAC.ShellExecute "%~s0", "", "", "runas", 1 >> "%temp%\getadmin.vbs"
    "%temp%\getadmin.vbs"
    exit /B

:gotAdmin
    if exist "%temp%\getadmin.vbs" ( del "%temp%\getadmin.vbs" )
    pushd "%CD%"
    CD /D "%~dp0"

:: Thay tên file exe của bạn vào đây
start "" "source/ServerApp/WebSocketTest/bin/Debug/net10.0-windows/WebSocketTest.exe"