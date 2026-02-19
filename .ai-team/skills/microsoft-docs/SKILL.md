---
name: microsoft-docs
description: Direct access to Microsoft documentation, API references, and official learning resources for .NET, Azure, and Microsoft technologies with MCP integration
---

# Microsoft Docs

This skill provides squad agents with direct access to Microsoft's official documentation ecosystem, enabling reference lookups, API verification, and solution discovery for .NET, Azure SDKs, and Microsoft technologies.

## Capabilities

### microsoft_docs_search
Search Microsoft's official documentation by keyword, API name, or topic.
- Lookup .NET classes, methods, namespaces
- Find Azure SDK references and API documentation
- Discover Microsoft Learn modules and training paths
- Search across official Microsoft docs repositories

**Examples:**
- "Search for HttpClient in Microsoft docs"
- "Find Azure App Service configuration references"
- "Look up Entity Framework Core DbContext methods"

### microsoft_docs_fetch
Retrieve full Microsoft documentation pages with complete details.
- Get full API method signatures and overload information
- Access parameter descriptions, return types, and exceptions
- Find related documentation and cross-references
- Review version-specific information and deprecations

**Examples:**
- "Fetch full documentation for System.Net.Http.HttpClient"
- "Get complete Azure.Storage.Blobs reference"
- "Retrieve AppSettings configuration page with all options"

### microsoft_learn_search
Discover learning paths and training modules from Microsoft Learn.
- Find curated learning paths by technology
- Locate hands-on modules and tutorials
- Identify certifications and skill paths
- Search for solution-specific guides

**Examples:**
- "Find Azure fundamentals learning path"
- ".NET Core async programming module"
- "ASP.NET Core security best practices training"

## When to Use

- **Verifying SDK signatures** — Catch hallucinated methods and parameter mismatches before implementation
- **Solving integration puzzles** — Find official patterns for Azure service integration, authentication, and configuration
- **Identifying deprecations** — Check if a method, NuGet package, or pattern is deprecated or superseded
- **Resolving version conflicts** — Look up version-specific API changes and migration guides
- **Learning unfamiliar APIs** — Discover official examples and working patterns from Microsoft's training resources
- **Self-service validation** — Verify authentication flows, RBAC permissions, and configuration requirements without human review

## Integration Notes

- Requires Microsoft Learn MCP Server connection for live documentation fetching
- Supports all Microsoft documentation domains: `docs.microsoft.com`, `learn.microsoft.com`, `msdn.microsoft.com`
- Works with .NET Framework, .NET Core/.NET 5+, Azure SDKs, and broader Microsoft ecosystem
- Cached results available for frequently accessed pages to reduce API calls

## Scope

This skill provides **documentation lookup and reference verification only**. It does not:
- Generate or execute code
- Modify infrastructure or services
- Change runtime behavior or deployment configuration
- Replace human architecture or security review

Use alongside domain logic and architecture design to ensure solutions fit project requirements and constraints.
