@echo off
chcp 65001 >nul
set PYTHONUTF8=1
set PYTHONIOENCODING=utf-8
setlocal enabledelayedexpansion
cd /d "%~dp0"
title GarraMania 3D Plushie Studio
echo ========================================================
echo     GarraMania 3D Plushie Studio (Multi-AI + Blender)
echo ========================================================

REM Se ja houver um processo anterior rodando na porta 7860, encerra para carregar o codigo atualizado
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":7860" ^| findstr "LISTENING"') do (
    echo Encerrando versao anterior (PID %%a) para carregar atualizacoes...
    taskkill /F /PID %%a >nul 2>&1
)
timeout /t 1 >nul 2>&1

REM Se o .venv nao existir, cria usando Python do sistema
if not exist ".venv\Scripts\python.exe" (
    echo [1/3] Criando ambiente virtual isolado - pasta .venv ...
    if exist "C:\Python314\python.exe" (
        "C:\Python314\python.exe" -m venv .venv
    ) else (
        python -m venv .venv
    )
)

REM Verifica se o Gradio esta instalado
.venv\Scripts\python.exe -c "import gradio" 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [2/3] Instalando dependencias do Gradio e ferramentas 3D...
    .venv\Scripts\pip.exe install -r requirements.txt
)

echo [3/3] Iniciando servidor do Plushie Studio...
echo.
echo ========================================================
echo   ATENCAO: MANTENHA ESTA JANELA ABERTA!
echo   Se fechar esta janela, o servidor desliga e o 
echo   navegador dara erro "Connection errored out".
echo   (Voce pode apenas minimizar esta janela)
echo ========================================================
echo.
echo Abrindo em http://127.0.0.1:7860 ...
start http://127.0.0.1:7860
.venv\Scripts\python.exe app.py

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Ocorreu um erro ao executar o Plushie Studio.
    pause
)
