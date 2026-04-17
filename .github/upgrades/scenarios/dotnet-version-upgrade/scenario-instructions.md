# .NET Version Upgrade to .NET 8.0

## Strategy
All-At-Once — Single project upgraded in one atomic operation

## Preferences
- **Flow Mode**: Automatic
- **Commit Strategy**: Single Commit at End
- **Target Framework**: .NET 8.0 (LTS)
- **Source Branch**: main
- **Working Branch**: upgrade-to-NET8

## Execution Constraints
- Single atomic upgrade — project updated in one operation
- Validate full solution build after upgrade
- All packages are compatible, no version updates needed

## Decisions

## Custom Instructions
<!-- Task-specific overrides: "For {taskId}: {instruction}" -->
