param([int]$TargetPid)

$signature = @"
[DllImport("kernel32.dll")]
public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
[DllImport("kernel32.dll")]
public static extern bool AttachConsole(uint dwProcessId);
[DllImport("kernel32.dll")]
public static extern bool FreeConsole();
[DllImport("kernel32.dll")]
public static extern bool SetConsoleCtrlHandler(IntPtr HandlerRoutine, bool Add);
"@

$api = Add-Type -MemberDefinition $signature -Name Win32 -Namespace P -PassThru -ErrorAction SilentlyContinue
if (-not $api) { $api = [P.Win32] }

# Detach current console, attach to target, ignore Ctrl+C in this script, send event, then detach
[void]$api::FreeConsole()
$attached = $api::AttachConsole([uint32]$TargetPid)
if (-not $attached) { Write-Host "AttachConsole failed for PID $TargetPid"; exit 1 }
[void]$api::SetConsoleCtrlHandler([IntPtr]::Zero, $true)
$sent = $api::GenerateConsoleCtrlEvent(0, 0)  # 0 = CTRL_C_EVENT, 0 = all attached processes
Start-Sleep -Milliseconds 1500
[void]$api::FreeConsole()
[void]$api::SetConsoleCtrlHandler([IntPtr]::Zero, $false)
if ($sent) { Write-Host "Ctrl+C sent to PID $TargetPid" } else { Write-Host "Send failed" }
