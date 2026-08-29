$unityPath = "C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe"
$projectPath = "C:\Users\julio\.gemini\antigravity\scratch\ClawMachine"
$logPath = "C:\Users\julio\.gemini\antigravity\scratch\ClawMachine\Logs\BuildWebGL.log"

Write-Host "Starting Unity WebGL Batch Build with thermal throttling..."
$p = Start-Process -FilePath $unityPath -ArgumentList "-quit", "-batchmode", "-projectPath", $projectPath, "-executeMethod", "WebGLBuilder.BuildWebGLMobile", "-logFile", $logPath -PassThru
$p.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal
$p.ProcessorAffinity = [IntPtr]255
$p.WaitForExit()
Write-Host "Unity build finished with exit code $($p.ExitCode)"
exit $p.ExitCode
