@echo off
title Client App Launcher
cls

echo ======================================================
echo    KHOI CHAY CLIENT APP (CAU TRUC: ROOT -> SOURCE)
echo ======================================================

:: 1. Đảm bảo CMD luôn đứng ở thư mục gốc (RATPROJECT) chứa file .bat này
cd /d "%~dp0"

:: 2. Kiểm tra Python
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [LOI] Khong tim thay Python! Hay cai dat Python va chon "Add to PATH".
    pause
    exit
)

:: 3. Kiểm tra/Tạo môi trường ảo (venv nằm ngay tại root)
if not exist "venv" (
    echo [INFO] Phat hien chua co venv. Dang tao moi...
    python -m venv venv
    if %errorlevel% neq 0 (
        echo [LOI] Khong the tao venv.
        pause
        exit
    )
)

:: 4. Kích hoạt venv
call venv\Scripts\activate

:: 5. Cài đặt thư viện (File requirements.txt nằm ngay tại root)
if exist "requirements.txt" (
    echo [INFO] Dang kiem tra thu vien...
    pip install -r requirements.txt --disable-pip-version-check
) else (
    echo [CANH BAO] Khong tim thay file requirements.txt!
)

:: 6. CHẠY APP 
:: Quan trọng: Trỏ đúng đường dẫn vào trong folder source
echo.
echo [INFO] Dang khoi dong Web Server...
echo ------------------------------------------------------
python source\ClientApp\app.py

if %errorlevel% neq 0 (
    echo.
    echo [LOI] Chuong trinh gap loi hoac bi dong dot ngot.
    pause
)