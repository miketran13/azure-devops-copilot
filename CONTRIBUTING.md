# Contributing to DevOps Copilot

Thank you for your interest in contributing! This project welcomes contributions and suggestions.

## Getting Started

1. **Fork** the repository
2. **Clone** your fork locally
3. **Create a branch** for your changes: `git checkout -b feature/my-feature`
4. **Make changes** and add tests
5. **Commit** with clear messages: `git commit -m "feat: add sprint planning tool"`
6. **Push** to your fork: `git push origin feature/my-feature`
7. **Open a Pull Request** against `main`

## Development Setup

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [tfx-cli](https://www.npmjs.com/package/tfx-cli): `npm install -g tfx-cli`

### Backend

```bash
cd backend
cp local.settings.example.json local.settings.json
# Edit local.settings.json with your Azure OpenAI and Azure DevOps settings
dotnet restore
dotnet build
func start
```

### Extension (Frontend)

```bash
cd extension
npm install
npm run build
# For development with hot reload:
npm run dev
```

## Code Style

- **C#**: Follow [.NET coding conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions). Use the `.editorconfig` provided.
- **TypeScript/React**: ESLint + Prettier configuration is included. Run `npm run lint` before submitting.
- Write XML doc comments for public APIs in C#.
- Write JSDoc comments for exported functions in TypeScript.

## Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` — new feature
- `fix:` — bug fix
- `docs:` — documentation changes
- `refactor:` — code refactoring
- `test:` — adding/updating tests
- `chore:` — tooling, CI, dependencies

## Adding New Agent Tools

To extend the copilot with new capabilities:

1. Create a new tool class in `backend/Tools/`
2. Add static methods with `[Description]` attributes
3. Register the tools with the appropriate specialist agent in `backend/Agents/`
4. Add unit tests in `backend/Tests/`
5. Update `docs/extending.md`

See [docs/extending.md](docs/extending.md) for detailed guidance.

## Pull Request Process

1. Ensure all tests pass (`dotnet test` and `npm run lint`)
2. Update documentation if you changed public APIs or behavior
3. Add a description of what changed and why
4. Link any related issues

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for details.
