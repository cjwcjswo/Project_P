# Unity Client Development Rules (Project P)

## 🎯 Core Philosophy
The Unity client is strictly a **Viewer**. It handles rendering, UI, animations, and user input. It does NOT handle authoritative game state or persistent data validation.

## 🏗️ Architecture & Logic
- **Decouple Logic from View:** Pure game logic (like the 7x6 Match-3 board array, deadlock detection, combo calculation) MUST be written in pure C# classes, independent of `MonoBehaviour`.
- **Event-Driven:** Use `System.Action` or Observer pattern to bridge Logic and View. Do NOT tightly couple GameObjects to core logic.
- **No `Update()` Polling:** Avoid using `Update()` for game loops or delays. Use event listeners.

## ⚡ Libraries & Concurrency
- **Asynchronous Operations:** Strictly use `UniTask` (Cysharp.Threading.Tasks). Do NOT use Unity's native `Coroutine` (`IEnumerator`).
- **Animations:** Use `DOTween` for all UI transitions, block dropping, and combat visual effects. `DOTween.ToUniTask()` requires the `UNITASK_DOTWEEN_SUPPORT` scripting define symbol in Player Settings.

## 🎮 Input
- **New Input System only:** Use `UnityEngine.InputSystem`. Do NOT use `UnityEngine.Input` (legacy) — it will throw `InvalidOperationException` when Active Input Handling is set to "Input System Package".
- Use `InputAction` fields (binding: `<Pointer>/press`, `<Pointer>/position`) for pointer input. `<Pointer>` abstracts both mouse and touchscreen.
- Create and `Enable()` `InputAction` instances in `OnEnable`, then `Disable()` and `Dispose()` in `OnDisable`.
- Use `action.WasPressedThisFrame()` / `action.WasReleasedThisFrame()` inside `UniTask.WaitUntil` for frame-accurate press detection.

## 🎨 UI & 2D URP
- **Canvas Setup:** All UI must support a 9:16 portrait aspect ratio. Use `Canvas Scaler` -> `Scale With Screen Size` (Reference: 1080x1920, Match: Width=0).
- **Pixel Perfect:** Ensure all sprites use point filtering (no filter) and compression is set to 'None' to maintain crisp 2D pixel art. Use the URP 2D Renderer features.