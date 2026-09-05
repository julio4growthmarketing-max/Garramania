# GarraMania 3D Plushie Studio Launcher
Set-Location $PSScriptRoot

Write-Host "===================================================" -ForegroundColor Magenta
Write-Host "    GarraMania 3D Plushie Studio (Gradio + Blender)" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Magenta

if (-not (Test-Path ".venv")) {
    Write-Host "Criando ambiente virtual dedicado (.venv)..." -ForegroundColor Yellow
    python -m venv .venv
}

Write-Host "Verificando dependências do Gradio..." -ForegroundColor Yellow
$gradioInstalled = & .venv\Scripts\python.exe -c "import gradio; print('OK')" 2>$null
if ($gradioInstalled -ne "OK") {
    Write-Host "Instalando dependências no .venv..." -ForegroundColor Yellow
    & .venv\Scripts\pip.exe install -r requirements.txt
}

Write-Host "Iniciando servidor local do Gradio em http://127.0.0.1:7860..." -ForegroundColor Green
& .venv\Scripts\python.exe app.py
