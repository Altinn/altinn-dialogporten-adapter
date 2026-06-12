# Contributing to dialogporten-adapter

Thanks for contributing to dialogporten-adapter.

## Ways to contribute

- Report bugs
- Propose improvements or new features
- Improve documentation
- Submit pull requests
- Add a 👍 reaction to issues you want prioritized

## Before you start

Read the project setup and development documentation in [README.md](README.md), especially local development and testing.

## Report bugs and suggest changes

Use GitHub issues:

- Create new issue: <https://github.com/Altinn/altinn-dialogporten-adapter/issues/new/choose>
- Existing issues: <https://github.com/Altinn/altinn-dialogporten-adapter/issues>

A useful issue includes:

- A short summary
- Steps to reproduce
- Expected behavior
- Actual behavior
- Relevant logs, screenshots, or sample payloads

## Pull request process

1. Fork the repository and create a branch from `main`.
2. Implement your change.
3. Add or update tests when relevant.
4. Update documentation when behavior or API contracts change.
5. Run build and tests locally:

```bash
dotnet build Altinn.DialogportenAdapter.sln
dotnet test Altinn.DialogportenAdapter.sln
```

6. Open a pull request: <https://github.com/Altinn/altinn-dialogporten-adapter/pulls>

### PR requirements

- PR title must follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/):
  - `<type>(optional scope): <description>`
  - Example: `feat(webapi): some new endpoint`
- Obvious low-effort AI-generated contributions ("AI slop") will be closed and ignored.


## License

By contributing, you agree your contributions are licensed under the project’s [LICENSE](LICENSE).
