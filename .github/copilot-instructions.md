- always show plan, don't apply without permission
- put hardcoded values to constants in the same class or if required in shared Constants class. 
string.Empty, true, false is not a constant. Don't move endpoints to constants.
Use MediaTypeNames.Application.* constants.
- Use C# 12 features when possible.
- Prefer immutability.
- Follow Clean Architecture principles.
- Avoid static mutable state.
- All async methods must use CancellationToken and suffix Async.