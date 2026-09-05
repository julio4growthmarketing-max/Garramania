@echo off
cd /d "%~dp0"
title GarraMania 3D Plushie Studio
echo ===================================================
echo     GarraMania 3D Plushie Studio (Gradio + Blender)
echo ===================================================

:: Verifica se o servidor ja esta rodando na porta 7860
netstat -ano | findstr ":7860" | findstr "LISTENING" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo O servidor do Plushie Studio ja esta ativo na porta 7860!
    echo Abrindo navegador em http://127.0.0.1:7860 ...
    start http://127.0.0.1:7860
    pause
    exit /b 0
)

:: Se o .venv nao existir, cria usando Python do sistema
if not exist ".venv\Scripts\python.exe" (
    echo [1/3] Criando ambiente virtual isolado (.venv)...
    if exist "C:\Python314\python.exe" (
        "C:\Python314\python.exe" -m venv .venv
    ) else (
        python -m venv .venv
    )
)

:: Verifica se o Gradio esta instalado
.venv\Scripts\python.exe -c "import gradio" 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [2/3] Instalando dependencias do Gradio e ferramentas 3D...
    .venv\Scripts\pip.exe install -r requirements.txt
)

echo [3/3] Iniciando servidor do Plushie Studio...
echo Abrindo em http://127.0.0.1:7860 ...
start http://127.0.0.1:7860
.venv\Scripts\python.exe app.py

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Ocorreu um erro ao executar o Plushie Studio.
    pause
)

