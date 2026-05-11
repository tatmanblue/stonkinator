# Goal

Please read the [design doc](docs/technical/DESIGN.md).

# Technology

This list of technology is imcomplete and will evolve as the project is defined and created.

- C# / .NET 10
- `DotNetEnv` 3.1.1 — `.env` file loading

# Coding Conventions

- Do not use `_` as a prefix for class-level fields. Use plain camelCase (e.g., `registryFilePath`).
- When a constructor parameter name collides with a field name, disambiguate with `this.` (e.g., `this.registry = registry`).
- Constants should be ALL_CAPS_WITH_UNDERSCORES.

# Instructions for Claude
1. All changes must be approved before creating these changes. Please prepare a plan of proposed changes and get confirmation before proceeding.
2. Unless directed otherwise, documentation should be saved in the briefcase in the "STONKS" project.
