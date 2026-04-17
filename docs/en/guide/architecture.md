# Architecture

SyZero follows a modular architecture where each package maps to a dedicated capability.

## Recommended layers

1. Web: protocols, controllers, API gateway.
2. Application: use-case orchestration and transactional boundaries.
3. Domain/Core: aggregates, entities, and domain services.
4. Infrastructure: database, cache, queue, and external systems.

## Dependency injection conventions

1. `IScopedDependency`
2. `ITransientDependency`
3. `ISingletonDependency`

## Design principles

1. Keep dependencies minimal and explicit.
2. Start with lightweight built-in capabilities in dev environments.
3. Scale out with dedicated modules for production workloads.