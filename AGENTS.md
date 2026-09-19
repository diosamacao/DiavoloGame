# ACTGame Codex guidance

## Project baseline

- This is a Unity `2022.3.62f3c1` project.
- Treat the repository root as the Unity project root.
- The primary development branch is `NetSync`.
- Preserve all pre-existing local changes. Never reset, discard, or overwrite user work.
- Do not commit, push, rebase, or switch branches unless the user explicitly requests it.

## Source and asset boundaries

- Code changes normally belong under `Assets/Scripts/**` and `Assets/Tests/**`.
- Do not edit generated folders: `Library/**`, `Temp/**`, `Logs/**`, `obj/**`, or generated `.csproj` / `.sln` files.
- Do not create, edit, move, or delete `Assets/Data/**`, prefab files, scenes, materials, Input Actions, imported art/audio/animation assets, or their `.meta` files unless the user explicitly authorizes that exact asset change.
- Shader source (`.shader`, `.shadergraph`, `.shadersubgraph`, `.hlsl`, `.cginc`) may be edited when requested. Material assignment and prefab wiring remain Unity Editor steps.
- When moving or deleting an allowed Unity asset, preserve the corresponding `.meta` operation so GUID references are not broken.

## Architecture and refactoring

- Inspect the relevant implementation and tests before proposing or making changes. Documentation is an index; code is the source of truth.
- Respect assembly-definition and layer boundaries. Do not introduce reverse dependencies to avoid a small amount of plumbing.
- For rewrites or framework refactors, make the new path the single source of truth and remove replaced entry points, branches, fields, registrations, and adapters.
- Do not retain `Legacy`, `Old`, `V1`, fallback, wrapper, or dual-run compatibility paths unless the user explicitly requests a migration window.
- Keep changes scoped to the requested behavior; do not perform unrelated cleanup.

## Comments and code quality

- Add concise responsibility comments for new or materially changed types.
- Document new or materially changed public/protected APIs and non-obvious state, timing, buffering, rollback, or lockstep behavior.
- Avoid comments that merely restate the code.
- Identifiers stay in English. Explanations and project documentation may be in Simplified Chinese.

## Verification

- Prefer the narrowest relevant existing tests under `Assets/Tests/**`.
- Use `tools/codex/Invoke-UnityTests.ps1` for command-line Unity tests when the same project directory is not already open in Unity Editor.
- Never start Unity batch mode against this project while the same project directory is open in another Unity Editor process. If that cannot be established safely, leave the Unity run to the user and provide exact Test Runner steps.
- Treat generated IDE diagnostics as supplemental. Unity compilation and Unity Test Framework results are authoritative for Unity code.
- Do not claim compilation or tests passed unless the command actually completed and its log/results were inspected.

## Architecture explanations

- For architecture, layering, module-boundary, or framework explanations, cite real repository files and line numbers for each key conclusion.
- Include a Mermaid flowchart or sequence diagram using real type and method names.
- If the requested mechanism is not implemented, say so explicitly before presenting pseudocode or a proposed diagram.
- For runtime execution-trace questions, use the `actgame-execution-trace` skill and present compact scenario-based indented call chains instead of a broad architecture essay.

## Completion report

After changing code, report:

1. The overall approach and why it was chosen.
2. One concise line for every added, modified, moved, or deleted source file.
3. Commands/tests actually run and their results.
4. Any remaining Unity Editor checks, including the relevant Play path, Test Runner assembly/class, and Inspector or prefab wiring.
5. Removed legacy paths or compatibility code, when applicable.

Do not say the task is complete while required verification is still running. Clearly distinguish automated verification from manual Editor checks.

