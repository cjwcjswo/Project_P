## Contextual Skill Routing
Before modifying or creating any code, you MUST read the corresponding rule file:
- If working in `/Client` (Unity): Read `.cursor/rules/unity-client.md`
- If working in `/Server` (.NET 8): Read `.cursor/rules/dotnet-server.md`

## Absolute Constraints
1. **No file deletion** without explicit user permission.
2. **Do not pivot or guess.** Ask questions if the task is ambiguous.
3. **No hallucination of architecture.** Stick to the existing PRD and constraints.

## Workflow
1. Read the relevant `Docs/Tasklist/*.json` to understand the current step.
2. Execute the task by applying the routed skills (rules).
3. Log the completed work in `Docs/Logs/YYYY-MM-DD.md`.
4. Update the tasklist JSON: set `"status": "completed"` for every finished task.