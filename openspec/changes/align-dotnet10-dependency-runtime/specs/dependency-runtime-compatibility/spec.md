## ADDED Requirements

### Requirement: Development server runtime compatibility
The WebAssembly development server SHALL run on the repository-configured .NET 10 runtime without requiring .NET 11.

#### Scenario: CI starts the actual development server
- **WHEN** the Release WebAssembly project is launched on .NET 10
- **THEN** its home page and Blazor framework bootstrap respond successfully
- **AND** CI fails if the server exits or does not become ready
