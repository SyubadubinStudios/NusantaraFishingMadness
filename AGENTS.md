# Repository Guidelines

## Project Structure & Module Organization

This project is currently in the design stage. `solution-design.md` describes the Indonesian open-world fishing game, gameplay systems, and planned architecture. `NusantaraFishingMadness.txt` records delivery requirements. Read both before implementing features.

The planned stack is .NET 10+, Avalonia UI, ReactiveUI, CommunityToolkit, and Vibe_Graphics ThreeNet. No solution, source tree, tests, or game assets exist yet. When scaffolding, organize application code under `src/`, automated tests under `tests/`, and models, textures, and audio under `assets/`. Keep editable asset sources separate from runtime exports.

## Build, Test, and Development Commands

There are currently no runnable build or test commands. After adding a .NET solution, document exact project paths and use:

- `dotnet restore`: restore solution dependencies.
- `dotnet build`: compile the solution.
- `dotnet test`: run automated tests.
- `dotnet run --project src/<AppProject>`: launch the Avalonia application.

These commands require project scaffolding and the appropriate .NET SDK first.

## Coding Style & Naming Conventions

For new C# code, use four-space indentation, PascalCase for types and public members, and camelCase for parameters and local variables. Match filenames to their primary types. Keep gameplay logic separate from UI and rendering integrations. Prioritize memory efficiency and measure performance-sensitive rendering or simulation changes.

No formatter or linter configuration exists yet. Establish shared rules in `.editorconfig` when introducing code.

## Testing Guidelines

No testing framework or coverage threshold is configured. Select and document a framework when creating the first test project. Use descriptive names such as `CatchResolverTests.cs` and `Resolve_WhenLineBreaks_ReturnsEscapedCatch`. Cover deterministic gameplay rules, inventory changes, and progression. Include manual verification steps for rendering, animation, and UI changes.

## Commit & Pull Request Guidelines

No Git history is available to establish existing conventions. Use concise, imperative commit subjects, such as `Add fishing progression design`. PRs should describe the change, link relevant issues, state validation performed, and include screenshots for visual changes.

## Documentation & Asset Workflow

Provide documentation in English and Bahasa Indonesia. Maintain `PLAN.md` for the roadmap and `Progress.md` for development tracking when those files are introduced. Include screenshots in README and documentation, and preserve the required attribution: Ariana Mischa Fadhila, Syubadubin Studios.

`.mcp.json` defines Blender and Rodin integrations. Use Blender for sculpting, rigging, and animation, and Rodin for generated 3D assets and 2D art. Keep credentials and machine-specific secrets out of committed configuration.
