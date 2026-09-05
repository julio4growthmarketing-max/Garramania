@echo off
cd /d "%~dp0"
title GarraMania 3D Plushie Studio
echo ===================================================
echo     GarraMania 3D Plushie Studio (Gradio + Blender)
echo ===================================================

if not exist ".venv" (
    echo Criando ambiente virtual dedicado (.venv)...
    python -m venv .venv
)

echo Verificando dependencias...
.venv\Scripts\python.exe -c "import gradio" 2>NUL
if %ERRORLEVEL% NEQ 0 (
    echo Instalando dependencias do Gradio no .venv...
    .venv\Scripts\pip.exe install -r requirements.txt
)

echo Iniciando servidor local do Gradio...
.venv\Scripts\python.exe app.py
pause

