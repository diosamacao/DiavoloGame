# Codex + Unity workflow

## Open the project in Codex

In the ChatGPT/Codex desktop app, choose **Add project** (or press `Ctrl+O`) and select:

```text
D:\Projects\ACTGame-code
```

Start a new Codex task after opening the project so Codex loads the repository `AGENTS.md` and `.agents/skills/**` files.

## Connect the Unity Editor executable

The project requires Unity `2022.3.62f3c1`. On this workstation it is installed at:

```text
D:\UnityEngine\Engine\2022.3.62f3c1\Editor\Unity.exe
```

The test script detects this layout and the usual Unity Hub locations automatically. If Unity is moved elsewhere, use either option below.

Pass the executable for one run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\codex\Invoke-UnityTests.ps1 `
  -UnityEditorPath 'X:\Path\To\2022.3.62f3c1\Editor\Unity.exe' `
  -TestPlatform EditMode
```

Or set it for the current terminal session:

```powershell
$env:UNITY_EDITOR_PATH = 'X:\Path\To\2022.3.62f3c1\Editor\Unity.exe'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\codex\Invoke-UnityTests.ps1 -TestPlatform EditMode
```

The execution-policy flag applies only to that PowerShell process; it does not change the machine policy.

## Useful test commands

All EditMode tests:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\codex\Invoke-UnityTests.ps1 -TestPlatform EditMode
```

A specific test or namespace:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\codex\Invoke-UnityTests.ps1 `
  -TestPlatform EditMode `
  -TestFilter 'ACTGame.Tests.SomeTest'
```

A specific test assembly:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\codex\Invoke-UnityTests.ps1 `
  -TestPlatform EditMode `
  -AssemblyNames 'ACTGame.Combat.EditModeTests'
```

Results and logs are written under `Logs/Codex/`, which is already ignored by Git.

Do not run batch-mode tests against this directory while the same project is open in Unity Editor. Close Unity first, or run tests from a separate Git worktree.
