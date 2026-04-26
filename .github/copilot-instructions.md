# GitHub Copilot Instructions

## Pull Request Targeting

All code (non-deployment) pull requests must target the `develop` branch.
Deployment pull requests (e.g. releases to staging or production) should target `main`.

## Code Style

Prefer simpler code. Favour clarity and readability over cleverness. Avoid unnecessary abstractions, over-engineering, and premature optimisation. When two solutions solve the same problem equally well, choose the simpler one.

## Architecture

Follow the SOLID principles:

- **S**ingle Responsibility Principle – each class or module should have one reason to change.
- **O**pen/Closed Principle – classes should be open for extension but closed for modification.
- **L**iskov Substitution Principle – subtypes must be substitutable for their base types.
- **I**nterface Segregation Principle – prefer small, focused interfaces over large, general-purpose ones.
- **D**ependency Inversion Principle – depend on abstractions, not concretions.

## Testing

- **Unit tests** – always write unit tests for business logic. Tests should be fast, isolated, and cover both typical and edge-case behaviour.
- **Integration tests** – always include happy-path integration tests that verify the end-to-end flow of each feature works correctly in a real or near-real environment.
