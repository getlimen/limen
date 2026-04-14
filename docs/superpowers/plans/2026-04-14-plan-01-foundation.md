# Plan 01 — Foundation: `limen` manager + admin OIDC + Angular shell

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scaffold the `limen` central manager repo with clean architecture layers, Postgres, admin OIDC login, a minimal Angular shell, and a CI pipeline that builds a Docker image.

**Architecture:** ASP.NET Core 10 Web API serving a Mediator-based Application layer + EF Core against Postgres. Angular 21 frontend built into static assets served by the API. Authentication via OIDC (Pocket ID as default example). Deployed via `compose.yml`. No agent, proxy, or WG wiring yet — those come in Plans 2-4.

**Tech Stack:** .NET 10, ASP.NET Core (minimal APIs), EF Core (PostgreSQL provider), Mediator (source-generated), Serilog, OpenIddict.Client for OIDC, Quartz.NET (scaffolded, not yet scheduling work), Angular 21 + spartan.ng + Tailwind 4, xUnit + FluentAssertions + NSubstitute, Testcontainers for Postgres.

**Prerequisites:**
- `getlimen/limen` repo exists (empty), initialized with `git init` locally
- Docker + Docker Compose installed
- .NET 10 SDK installed
- Node 20+ installed
- `gh` CLI authenticated as PianoNic

**Parent spec:** `docs/superpowers/specs/2026-04-14-limen-design.md`

---

## File structure created by this plan

```
limen/
├── .github/workflows/
│   └── ci.yml                               # build + test + docker push on main
├── .editorconfig
├── .gitignore
├── .dockerignore
├── CLAUDE.md                                # (already written in scaffolding)
├── LICENSE                                  # Apache 2.0
├── README.md                                # (already written)
├── Directory.Build.props                    # common C# props (nullable, warnings, etc.)
├── Limen.slnx
├── compose.yml                              # Postgres + Limen
├── compose.dev.yml                          # with dev overrides (local DB port, hot reload)
├── template.env                             # env template
├── contracts/
│   └── Limen.Contracts/
│       ├── Limen.Contracts.csproj
│       ├── Common/
│       │   ├── ConfigVersion.cs
│       │   └── Envelope.cs
│       └── README.md
├── src/
│   ├── Limen.Domain/
│   │   ├── Limen.Domain.csproj
│   │   └── Auth/
│   │       └── AdminSession.cs              # persisted session row
│   ├── Limen.Application/
│   │   ├── Limen.Application.csproj
│   │   ├── Common/
│   │   │   ├── Behaviors/
│   │   │   │   ├── ValidationBehavior.cs
│   │   │   │   └── LoggingBehavior.cs
│   │   │   ├── Interfaces/
│   │   │   │   └── IClock.cs
│   │   │   └── DependencyInjection.cs
│   │   ├── Services/
│   │   │   └── (empty in plan 1)
│   │   ├── Commands/
│   │   │   └── Auth/
│   │   │       ├── SignOutCommand.cs
│   │   │       └── HandleOidcCallbackCommand.cs
│   │   └── Queries/
│   │       └── Auth/
│   │           └── GetCurrentAdminQuery.cs
│   ├── Limen.Infrastructure/
│   │   ├── Limen.Infrastructure.csproj
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/
│   │   │   │   └── AdminSessionConfiguration.cs
│   │   │   ├── Migrations/
│   │   │   │   └── (generated)
│   │   │   └── Repositories/
│   │   │       └── (empty in plan 1)
│   │   ├── Auth/
│   │   │   └── OidcHandler.cs
│   │   ├── Clock/
│   │   │   └── SystemClock.cs
│   │   └── DependencyInjection.cs
│   ├── Limen.API/
│   │   ├── Limen.API.csproj
│   │   ├── Program.cs
│   │   ├── Endpoints/
│   │   │   ├── AuthEndpoints.cs
│   │   │   └── HealthEndpoints.cs
│   │   ├── Middleware/
│   │   │   └── GlobalExceptionMiddleware.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── Dockerfile
│   ├── Limen.Frontend/
│   │   ├── package.json
│   │   ├── angular.json
│   │   ├── tailwind.config.ts
│   │   ├── tsconfig.json
│   │   └── src/
│   │       ├── main.ts
│   │       ├── index.html
│   │       ├── styles.css
│   │       ├── app/
│   │       │   ├── app.component.ts
│   │       │   ├── app.routes.ts
│   │       │   ├── core/
│   │       │   │   ├── auth/
│   │       │   │   │   ├── auth.service.ts
│   │       │   │   │   └── auth.guard.ts
│   │       │   │   └── api/
│   │       │   │       └── api.service.ts
│   │       │   ├── layout/
│   │       │   │   └── shell.component.ts
│   │       │   └── features/
│   │       │       ├── login/
│   │       │       │   └── login.component.ts
│   │       │       └── dashboard/
│   │       │           └── dashboard.component.ts
│   │       └── environments/
│   │           ├── environment.ts
│   │           └── environment.development.ts
│   └── Limen.Tests/
│       ├── Limen.Tests.csproj
│       ├── Application/
│       │   └── Auth/
│       │       └── HandleOidcCallbackCommandTests.cs
│       ├── Infrastructure/
│       │   └── Persistence/
│       │       └── AppDbContextTests.cs
│       └── Integration/
│           └── HealthEndpointTests.cs
```

---

## Task 1: Repo boilerplate (LICENSE, gitignore, editorconfig, Directory.Build.props)

**Files:**
- Create: `limen/LICENSE`
- Create: `limen/.gitignore`
- Create: `limen/.editorconfig`
- Create: `limen/.dockerignore`
- Create: `limen/Directory.Build.props`

- [ ] **Step 1: Create LICENSE (Apache 2.0 — full text)**

Download from `https://www.apache.org/licenses/LICENSE-2.0.txt` or copy from any existing Apache-2.0 repo. File must be exactly the standard text; do not modify.

- [ ] **Step 2: Create `.gitignore`**

```gitignore
## .NET
bin/
obj/
*.user
*.suo
.vs/
.idea/
Limen.slnx.user

## EF Core
*.db
*.db-*

## Node / Angular
node_modules/
dist/
.angular/
coverage/
*.tsbuildinfo

## Environment
.env
.env.local
*.env
!template.env

## OS
.DS_Store
Thumbs.db

## Logs
*.log
logs/

## IDE
.vscode/
*.swp
*~

## Certificates (never commit)
*.pfx
*.key
*.crt
*.pem
!test/**/*.pem
```

- [ ] **Step 3: Create `.editorconfig` (matches Kursa pattern)**

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
indent_style = space
indent_size = 4
trim_trailing_whitespace = true

[*.{cs,csproj}]
indent_size = 4

[*.{json,yml,yaml,ts,js,html,scss,css}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false

# C# code style
[*.cs]
dotnet_sort_system_directives_first = true
dotnet_style_namespace_match_folder = true
csharp_style_namespace_declarations = file_scoped:error
csharp_prefer_braces = true:warning
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_new_line_before_open_brace = all
```

- [ ] **Step 4: Create `.dockerignore`**

```dockerignore
**/bin
**/obj
**/node_modules
**/dist
**/.angular
**/.vs
**/*.user
.git
.github
docs
contracts
tests
*.md
!README.md
```

- [ ] **Step 5: Create `Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
    <AnalysisLevel>latest</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Commit**

```bash
cd limen
git add LICENSE .gitignore .editorconfig .dockerignore Directory.Build.props
git commit -m "chore: repo boilerplate (license, gitignore, editorconfig)"
```

---

## Task 2: Solution + Limen.Contracts project

**Files:**
- Create: `limen/Limen.slnx`
- Create: `limen/contracts/Limen.Contracts/Limen.Contracts.csproj`
- Create: `limen/contracts/Limen.Contracts/Common/Envelope.cs`
- Create: `limen/contracts/Limen.Contracts/Common/ConfigVersion.cs`
- Create: `limen/contracts/Limen.Contracts/README.md`

- [ ] **Step 1: Create solution**

Run:
```bash
cd limen
dotnet new slnx -n Limen
```

- [ ] **Step 2: Create Limen.Contracts classlib**

```bash
dotnet new classlib -n Limen.Contracts -o contracts/Limen.Contracts --framework net10.0
dotnet sln Limen.slnx add contracts/Limen.Contracts/Limen.Contracts.csproj
```

- [ ] **Step 3: Edit `contracts/Limen.Contracts/Limen.Contracts.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Limen.Contracts</PackageId>
    <Version>0.1.0-alpha</Version>
    <Authors>PianoNic</Authors>
    <Description>Shared DTOs + contracts for Limen components (limen, ostiarius, forculus, limentinus).</Description>
    <PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>
    <RepositoryUrl>https://github.com/getlimen/limen</RepositoryUrl>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Write the `Envelope` type**

Create `contracts/Limen.Contracts/Common/Envelope.cs`:

```csharp
namespace Limen.Contracts.Common;

/// <summary>
/// Universal wrapper around every control-plane message. Carries the message
/// type discriminator, an optional config version for idempotency/dedup, and
/// the serialized payload.
/// </summary>
public sealed record Envelope<T>(
    string Type,
    ConfigVersion Version,
    T Payload);
```

- [ ] **Step 5: Write the `ConfigVersion` type**

Create `contracts/Limen.Contracts/Common/ConfigVersion.cs`:

```csharp
namespace Limen.Contracts.Common;

/// <summary>
/// Monotonic u64 used for deduplication. Limen increments this on every state
/// change that produces an outbound message. Agents/proxies track the last
/// version they applied and reject older ones.
/// </summary>
public readonly record struct ConfigVersion(ulong Value) : IComparable<ConfigVersion>
{
    public static ConfigVersion Zero => new(0);
    public ConfigVersion Next() => new(Value + 1);
    public int CompareTo(ConfigVersion other) => Value.CompareTo(other.Value);
    public static bool operator <(ConfigVersion a, ConfigVersion b) => a.Value < b.Value;
    public static bool operator >(ConfigVersion a, ConfigVersion b) => a.Value > b.Value;
    public static bool operator <=(ConfigVersion a, ConfigVersion b) => a.Value <= b.Value;
    public static bool operator >=(ConfigVersion a, ConfigVersion b) => a.Value >= b.Value;
    public override string ToString() => Value.ToString();
}
```

- [ ] **Step 6: Build to verify**

```bash
dotnet build contracts/Limen.Contracts/Limen.Contracts.csproj
```
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add Limen.slnx contracts/
git commit -m "feat(contracts): initial Limen.Contracts project with Envelope and ConfigVersion"
```

---

## Task 3: Domain project (empty scaffold + AdminSession entity)

**Files:**
- Create: `limen/src/Limen.Domain/Limen.Domain.csproj`
- Create: `limen/src/Limen.Domain/Auth/AdminSession.cs`

- [ ] **Step 1: Create Domain project**

```bash
dotnet new classlib -n Limen.Domain -o src/Limen.Domain
dotnet sln Limen.slnx add src/Limen.Domain/Limen.Domain.csproj
```

- [ ] **Step 2: Edit csproj (remove default Class1.cs)**

Delete `src/Limen.Domain/Class1.cs`.

Ensure `src/Limen.Domain/Limen.Domain.csproj` contains:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Create AdminSession entity**

Create `src/Limen.Domain/Auth/AdminSession.cs`:

```csharp
namespace Limen.Domain.Auth;

/// <summary>
/// Persisted admin session. One row per active login.
/// </summary>
public class AdminSession
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty; // OIDC subject claim
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build src/Limen.Domain/Limen.Domain.csproj
```
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add src/Limen.Domain/
git commit -m "feat(domain): AdminSession entity"
```

---

## Task 4: Application project with Mediator + validation behavior

**Files:**
- Create: `limen/src/Limen.Application/Limen.Application.csproj`
- Create: `limen/src/Limen.Application/Common/Interfaces/IClock.cs`
- Create: `limen/src/Limen.Application/Common/Behaviors/ValidationBehavior.cs`
- Create: `limen/src/Limen.Application/Common/Behaviors/LoggingBehavior.cs`
- Create: `limen/src/Limen.Application/Common/DependencyInjection.cs`

- [ ] **Step 1: Create Application project**

```bash
dotnet new classlib -n Limen.Application -o src/Limen.Application
dotnet sln Limen.slnx add src/Limen.Application/Limen.Application.csproj
```

Delete the default `Class1.cs`.

- [ ] **Step 2: Configure csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsAotCompatible>false</IsAotCompatible> <!-- Mediator source-gen needs reflection metadata -->
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Limen.Domain\Limen.Domain.csproj" />
    <ProjectReference Include="..\..\contracts\Limen.Contracts\Limen.Contracts.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Mediator.Abstractions" Version="3.0.0" />
    <PackageReference Include="Mediator.SourceGenerator" Version="3.0.0">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentValidation" Version="11.10.0" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.10.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `IClock` interface**

Create `src/Limen.Application/Common/Interfaces/IClock.cs`:

```csharp
namespace Limen.Application.Common.Interfaces;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
```

- [ ] **Step 4: Create `ValidationBehavior`**

Create `src/Limen.Application/Common/Behaviors/ValidationBehavior.cs`:

```csharp
using FluentValidation;
using Mediator;

namespace Limen.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async ValueTask<TResponse> Handle(TRequest message, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next(message, ct);

        var context = new ValidationContext<TRequest>(message);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
        if (failures.Count > 0) throw new ValidationException(failures);

        return await next(message, ct);
    }
}
```

- [ ] **Step 5: Create `LoggingBehavior`**

Create `src/Limen.Application/Common/Behaviors/LoggingBehavior.cs`:

```csharp
using Mediator;
using Microsoft.Extensions.Logging;

namespace Limen.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async ValueTask<TResponse> Handle(TRequest message, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", name);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await next(message, ct);
            _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms", name, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {RequestName}", name);
            throw;
        }
    }
}
```

- [ ] **Step 6: Create `DependencyInjection` root for Application**

Create `src/Limen.Application/Common/DependencyInjection.cs`:

```csharp
using System.Reflection;
using FluentValidation;
using Limen.Application.Common.Behaviors;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Limen.Application.Common;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(opt => opt.ServiceLifetime = ServiceLifetime.Scoped);
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        return services;
    }
}
```

- [ ] **Step 7: Build to verify Mediator source generators run cleanly**

```bash
dotnet build src/Limen.Application/Limen.Application.csproj
```
Expected: `Build succeeded`.

- [ ] **Step 8: Commit**

```bash
git add src/Limen.Application/
git commit -m "feat(application): Mediator setup with Validation + Logging behaviors"
```

---

## Task 5: Infrastructure project — DbContext, IClock implementation

**Files:**
- Create: `limen/src/Limen.Infrastructure/Limen.Infrastructure.csproj`
- Create: `limen/src/Limen.Infrastructure/Persistence/AppDbContext.cs`
- Create: `limen/src/Limen.Infrastructure/Persistence/Configurations/AdminSessionConfiguration.cs`
- Create: `limen/src/Limen.Infrastructure/Clock/SystemClock.cs`
- Create: `limen/src/Limen.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create Infrastructure project**

```bash
dotnet new classlib -n Limen.Infrastructure -o src/Limen.Infrastructure
dotnet sln Limen.slnx add src/Limen.Infrastructure/Limen.Infrastructure.csproj
```
Delete the default `Class1.cs`.

- [ ] **Step 2: Configure csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\Limen.Domain\Limen.Domain.csproj" />
    <ProjectReference Include="..\Limen.Application\Limen.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `AppDbContext`**

```csharp
// src/Limen.Infrastructure/Persistence/AppDbContext.cs
using Limen.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace Limen.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AdminSession> AdminSessions => Set<AdminSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

- [ ] **Step 4: Create `AdminSessionConfiguration`**

```csharp
// src/Limen.Infrastructure/Persistence/Configurations/AdminSessionConfiguration.cs
using Limen.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class AdminSessionConfiguration : IEntityTypeConfiguration<AdminSession>
{
    public void Configure(EntityTypeBuilder<AdminSession> builder)
    {
        builder.ToTable("admin_sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.HasIndex(x => x.Subject);
        builder.HasIndex(x => x.ExpiresAt);
    }
}
```

- [ ] **Step 5: Create `SystemClock`**

```csharp
// src/Limen.Infrastructure/Clock/SystemClock.cs
using Limen.Application.Common.Interfaces;

namespace Limen.Infrastructure.Clock;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
```

- [ ] **Step 6: Create `DependencyInjection`**

```csharp
// src/Limen.Infrastructure/DependencyInjection.cs
using Limen.Application.Common.Interfaces;
using Limen.Infrastructure.Clock;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Limen.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres");

        services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(cs));
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
```

- [ ] **Step 7: Build**

```bash
dotnet build src/Limen.Infrastructure/Limen.Infrastructure.csproj
```
Expected: `Build succeeded`.

- [ ] **Step 8: Commit**

```bash
git add src/Limen.Infrastructure/
git commit -m "feat(infra): AppDbContext, AdminSession config, SystemClock, DI root"
```

---

## Task 6: Tests project + first integration test (DbContext)

**Files:**
- Create: `limen/src/Limen.Tests/Limen.Tests.csproj`
- Create: `limen/src/Limen.Tests/Infrastructure/Persistence/AppDbContextTests.cs`

- [ ] **Step 1: Create tests project**

```bash
dotnet new xunit -n Limen.Tests -o src/Limen.Tests
dotnet sln Limen.slnx add src/Limen.Tests/Limen.Tests.csproj
```

- [ ] **Step 2: Configure csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Limen.Domain\Limen.Domain.csproj" />
    <ProjectReference Include="..\Limen.Application\Limen.Application.csproj" />
    <ProjectReference Include="..\Limen.Infrastructure\Limen.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="FluentAssertions" Version="8.0.0" />
    <PackageReference Include="NSubstitute" Version="5.1.0" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Write the failing test**

```csharp
// src/Limen.Tests/Infrastructure/Persistence/AppDbContextTests.cs
using FluentAssertions;
using Limen.Domain.Auth;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Limen.Tests.Infrastructure.Persistence;

public sealed class AppDbContextTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync() => await _pg.StartAsync();
    public async Task DisposeAsync() => await _pg.StopAsync();

    [Fact]
    public async Task Can_persist_and_read_AdminSession()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .Options;

        await using (var ctx = new AppDbContext(opts))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.AdminSessions.Add(new AdminSession
            {
                Id = Guid.NewGuid(),
                Subject = "test-subject",
                Email = "admin@example.com",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new AppDbContext(opts))
        {
            var session = await ctx.AdminSessions.FirstAsync();
            session.Email.Should().Be("admin@example.com");
        }
    }
}
```

- [ ] **Step 4: Run test (expect PASS — we're not following strict TDD on the initial scaffolding test because schema is trivial)**

```bash
dotnet test src/Limen.Tests/Limen.Tests.csproj --filter "FullyQualifiedName~AppDbContextTests"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Limen.Tests/
git commit -m "test(infra): Testcontainers-backed AppDbContext integration test"
```

---

## Task 7: API project — Program.cs, health endpoint

**Files:**
- Create: `limen/src/Limen.API/Limen.API.csproj`
- Create: `limen/src/Limen.API/Program.cs`
- Create: `limen/src/Limen.API/Endpoints/HealthEndpoints.cs`
- Create: `limen/src/Limen.API/Middleware/GlobalExceptionMiddleware.cs`
- Create: `limen/src/Limen.API/appsettings.json`
- Create: `limen/src/Limen.API/appsettings.Development.json`

- [ ] **Step 1: Create API project**

```bash
dotnet new web -n Limen.API -o src/Limen.API
dotnet sln Limen.slnx add src/Limen.API/Limen.API.csproj
```

- [ ] **Step 2: Configure csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <UserSecretsId>limen-api-dev</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Limen.Application\Limen.Application.csproj" />
    <ProjectReference Include="..\Limen.Infrastructure\Limen.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageReference Include="Serilog.Expressions" Version="5.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.OpenIdConnect" Version="10.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.Cookies" Version="10.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Write the failing integration test for the health endpoint**

```csharp
// src/Limen.Tests/Integration/HealthEndpointTests.cs
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Limen.Tests.Integration;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_200()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/healthz");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

Add the `Microsoft.AspNetCore.Mvc.Testing` package reference to `Limen.Tests.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
```

And add a reference to the API project:

```xml
<ProjectReference Include="..\Limen.API\Limen.API.csproj" />
```

- [ ] **Step 4: Run test (expect FAIL — Program.cs doesn't wire `/healthz` yet)**

```bash
dotnet test src/Limen.Tests/Limen.Tests.csproj --filter "FullyQualifiedName~HealthEndpointTests"
```
Expected: FAIL (`System.Net.HttpRequestException` or 404).

- [ ] **Step 5: Create `HealthEndpoints.cs`**

```csharp
// src/Limen.API/Endpoints/HealthEndpoints.cs
namespace Limen.API.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
        return app;
    }
}
```

- [ ] **Step 6: Create `GlobalExceptionMiddleware.cs`**

```csharp
// src/Limen.API/Middleware/GlobalExceptionMiddleware.cs
using FluentValidation;

namespace Limen.API.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ValidationException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { error = "internal_error" });
        }
    }
}
```

- [ ] **Step 7: Write `Program.cs`**

```csharp
// src/Limen.API/Program.cs
using Limen.API.Endpoints;
using Limen.API.Middleware;
using Limen.Application.Common;
using Limen.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging();

    app.MapHealthEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Limen.API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Allow WebApplicationFactory<Program> in tests
public partial class Program;
```

- [ ] **Step 8: Create `appsettings.json`**

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=limen;Username=limen;Password=limen"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information"
      }
    }
  },
  "Oidc": {
    "Authority": "https://example.pocket-id.org",
    "ClientId": "limen",
    "ClientSecret": "",
    "CallbackPath": "/auth/oidc/callback",
    "Scopes": ["openid", "profile", "email"]
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 9: Create `appsettings.Development.json`**

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=limen_dev;Username=limen;Password=limen"
  }
}
```

- [ ] **Step 10: Run test again (expect PASS)**

```bash
dotnet test src/Limen.Tests/Limen.Tests.csproj --filter "FullyQualifiedName~HealthEndpointTests"
```
Expected: PASS.

- [ ] **Step 11: Commit**

```bash
git add src/Limen.API/ src/Limen.Tests/
git commit -m "feat(api): /healthz endpoint + Program.cs + Serilog + global exception middleware"
```

---

## Task 8: Initial EF migration

**Files:**
- Create: `limen/src/Limen.Infrastructure/Persistence/Migrations/...` (auto-generated)

- [ ] **Step 1: Install dotnet-ef tool if not present**

```bash
dotnet tool install --global dotnet-ef --version 10.0.0
```
(If already installed, skip.)

- [ ] **Step 2: Generate the initial migration**

```bash
cd limen
dotnet ef migrations add Initial --project src/Limen.Infrastructure --startup-project src/Limen.API --output-dir Persistence/Migrations
```

- [ ] **Step 3: Verify migration files generated**

```bash
ls src/Limen.Infrastructure/Persistence/Migrations/
```
Expect: `AppDbContextModelSnapshot.cs`, `<timestamp>_Initial.cs`, `<timestamp>_Initial.Designer.cs`.

- [ ] **Step 4: Commit**

```bash
git add src/Limen.Infrastructure/Persistence/Migrations/
git commit -m "feat(infra): initial EF migration for AdminSessions table"
```

---

## Task 9: OIDC authentication wiring

**Files:**
- Create: `limen/src/Limen.Application/Commands/Auth/HandleOidcCallbackCommand.cs`
- Create: `limen/src/Limen.Application/Commands/Auth/SignOutCommand.cs`
- Create: `limen/src/Limen.Application/Queries/Auth/GetCurrentAdminQuery.cs`
- Create: `limen/src/Limen.API/Endpoints/AuthEndpoints.cs`
- Modify: `limen/src/Limen.API/Program.cs`

- [ ] **Step 1: Write the failing test for `HandleOidcCallbackCommand`**

```csharp
// src/Limen.Tests/Application/Auth/HandleOidcCallbackCommandTests.cs
using FluentAssertions;
using Limen.Application.Commands.Auth;
using Limen.Application.Common.Interfaces;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Limen.Tests.Application.Auth;

public sealed class HandleOidcCallbackCommandTests
{
    [Fact]
    public async Task Creates_AdminSession_and_returns_session_id()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var fixedNow = new DateTimeOffset(2026, 04, 14, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(fixedNow);

        var handler = new HandleOidcCallbackCommandHandler(db, clock);
        var result = await handler.Handle(
            new HandleOidcCallbackCommand("sub-123", "admin@example.com", "10.0.0.1", "Mozilla"),
            CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
        var session = await db.AdminSessions.FindAsync(result);
        session.Should().NotBeNull();
        session!.Email.Should().Be("admin@example.com");
        session.ExpiresAt.Should().Be(fixedNow.AddHours(12));
    }
}
```

- [ ] **Step 2: Run test (expect FAIL — command doesn't exist)**

```bash
dotnet test --filter "FullyQualifiedName~HandleOidcCallbackCommandTests"
```
Expected: FAIL.

- [ ] **Step 3: Write `HandleOidcCallbackCommand.cs` (command + handler in ONE file per user's clean-arch rules)**

```csharp
// src/Limen.Application/Commands/Auth/HandleOidcCallbackCommand.cs
using Limen.Application.Common.Interfaces;
using Limen.Domain.Auth;
using Limen.Infrastructure.Persistence;
using Mediator;

namespace Limen.Application.Commands.Auth;

public sealed record HandleOidcCallbackCommand(string Subject, string Email, string? IpAddress, string? UserAgent)
    : ICommand<Guid>;

internal sealed class HandleOidcCallbackCommandHandler : ICommandHandler<HandleOidcCallbackCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public HandleOidcCallbackCommandHandler(AppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async ValueTask<Guid> Handle(HandleOidcCallbackCommand cmd, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var session = new AdminSession
        {
            Id = Guid.NewGuid(),
            Subject = cmd.Subject,
            Email = cmd.Email,
            IpAddress = cmd.IpAddress,
            UserAgent = cmd.UserAgent,
            CreatedAt = now,
            ExpiresAt = now.AddHours(12),
        };
        _db.AdminSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session.Id;
    }
}
```

**Note:** the Application project can't currently reference `AppDbContext` directly because `AppDbContext` lives in Infrastructure. This violates the clean arch rule. The fix: define `IAppDbContext` in Application and implement it in Infrastructure. Do that next.

- [ ] **Step 4: Create `IAppDbContext` interface in Application**

```csharp
// src/Limen.Application/Common/Interfaces/IAppDbContext.cs
using Limen.Domain.Auth;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<AdminSession> AdminSessions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
}
```

Add `PackageReference Include="Microsoft.EntityFrameworkCore"` to `Limen.Application.csproj`.

- [ ] **Step 5: Make `AppDbContext` implement `IAppDbContext`**

Edit `src/Limen.Infrastructure/Persistence/AppDbContext.cs`:

```csharp
using Limen.Application.Common.Interfaces;
// ...
public sealed class AppDbContext : DbContext, IAppDbContext
{
    // existing body unchanged
}
```

Register in `Limen.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
```

- [ ] **Step 6: Update handler to use `IAppDbContext`**

```csharp
// HandleOidcCallbackCommand.cs — replace AppDbContext with IAppDbContext
using Limen.Application.Common.Interfaces;
// ...
private readonly IAppDbContext _db;
public HandleOidcCallbackCommandHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }
```

- [ ] **Step 7: Run the test (expect PASS)**

```bash
dotnet test --filter "FullyQualifiedName~HandleOidcCallbackCommandTests"
```
Expected: PASS.

- [ ] **Step 8: Create `SignOutCommand.cs`**

```csharp
// src/Limen.Application/Commands/Auth/SignOutCommand.cs
using Limen.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Commands.Auth;

public sealed record SignOutCommand(Guid SessionId) : ICommand<Unit>;

internal sealed class SignOutCommandHandler : ICommandHandler<SignOutCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public SignOutCommandHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async ValueTask<Unit> Handle(SignOutCommand cmd, CancellationToken ct)
    {
        var session = await _db.AdminSessions.FirstOrDefaultAsync(s => s.Id == cmd.SessionId, ct);
        if (session is null) return Unit.Value;
        session.RevokedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
```

- [ ] **Step 9: Create `GetCurrentAdminQuery.cs`**

```csharp
// src/Limen.Application/Queries/Auth/GetCurrentAdminQuery.cs
using Limen.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Queries.Auth;

public sealed record AdminInfoDto(string Subject, string Email, DateTimeOffset ExpiresAt);

public sealed record GetCurrentAdminQuery(Guid SessionId) : IQuery<AdminInfoDto?>;

internal sealed class GetCurrentAdminQueryHandler : IQueryHandler<GetCurrentAdminQuery, AdminInfoDto?>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public GetCurrentAdminQueryHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async ValueTask<AdminInfoDto?> Handle(GetCurrentAdminQuery q, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var session = await _db.AdminSessions
            .Where(s => s.Id == q.SessionId && s.RevokedAt == null && s.ExpiresAt > now)
            .FirstOrDefaultAsync(ct);
        return session is null ? null : new AdminInfoDto(session.Subject, session.Email, session.ExpiresAt);
    }
}
```

- [ ] **Step 10: Create `AuthEndpoints.cs`**

```csharp
// src/Limen.API/Endpoints/AuthEndpoints.cs
using System.Security.Claims;
using Limen.Application.Commands.Auth;
using Limen.Application.Queries.Auth;
using Mediator;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Limen.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/auth");

        grp.MapGet("/login", (HttpContext ctx) =>
            Results.Challenge(new AuthenticationProperties { RedirectUri = "/auth/complete" }, new[] { "oidc" }));

        grp.MapGet("/complete", async (HttpContext ctx, IMediator mediator) =>
        {
            var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirstValue("sub") ?? "";
            var email = ctx.User.FindFirstValue(ClaimTypes.Email) ?? "";
            var ip = ctx.Connection.RemoteIpAddress?.ToString();
            var ua = ctx.Request.Headers.UserAgent.ToString();

            var sessionId = await mediator.Send(new HandleOidcCallbackCommand(sub, email, ip, ua));
            ctx.Response.Cookies.Append("limen_admin", sessionId.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(12)
            });
            return Results.Redirect("/");
        }).RequireAuthorization();

        grp.MapPost("/signout", async (HttpContext ctx, IMediator mediator) =>
        {
            if (Guid.TryParse(ctx.Request.Cookies["limen_admin"], out var id))
                await mediator.Send(new SignOutCommand(id));
            ctx.Response.Cookies.Delete("limen_admin");
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok();
        });

        grp.MapGet("/me", async (HttpContext ctx, IMediator mediator) =>
        {
            if (!Guid.TryParse(ctx.Request.Cookies["limen_admin"], out var id)) return Results.Unauthorized();
            var admin = await mediator.Send(new GetCurrentAdminQuery(id));
            return admin is null ? Results.Unauthorized() : Results.Ok(admin);
        });

        return app;
    }
}
```

- [ ] **Step 11: Wire OIDC into `Program.cs`**

Update `Program.cs` after `builder.Services.AddInfrastructure(...)`:

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "oidc";
})
.AddCookie()
.AddOpenIdConnect("oidc", options =>
{
    var cfg = builder.Configuration.GetSection("Oidc");
    options.Authority = cfg["Authority"];
    options.ClientId = cfg["ClientId"];
    options.ClientSecret = cfg["ClientSecret"];
    options.CallbackPath = cfg["CallbackPath"] ?? "/auth/oidc/callback";
    options.ResponseType = "code";
    options.UsePkce = true;
    options.SaveTokens = true;
    foreach (var s in cfg.GetSection("Scopes").Get<string[]>() ?? Array.Empty<string>())
        options.Scope.Add(s);
});
builder.Services.AddAuthorization();
```

And before `app.Run()`:
```csharp
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
```

Add `using Microsoft.AspNetCore.Authentication.Cookies;` at top of Program.cs.

- [ ] **Step 12: Run all tests**

```bash
dotnet test
```
Expected: all PASS.

- [ ] **Step 13: Commit**

```bash
git add src/ contracts/
git commit -m "feat(auth): OIDC login flow — HandleOidcCallbackCommand, SignOutCommand, GetCurrentAdminQuery, /auth/* endpoints"
```

---

## Task 10: Angular 21 scaffold with Tailwind 4 + spartan.ng

**Files:**
- Create: `limen/src/Limen.Frontend/` (full Angular app)

- [ ] **Step 1: Scaffold Angular app**

```bash
cd limen/src
npx -y @angular/cli@21 new Limen.Frontend --defaults --skip-git --style=css --ssr=false --routing --standalone
cd Limen.Frontend
```

- [ ] **Step 2: Install Tailwind 4**

```bash
npm install -D tailwindcss@^4 @tailwindcss/postcss postcss
```

Create `postcss.config.js`:
```js
module.exports = { plugins: { '@tailwindcss/postcss': {} } };
```

Edit `src/styles.css`:
```css
@import "tailwindcss";
```

- [ ] **Step 3: Install spartan.ng**

```bash
ng add @spartan-ng/cli
```
Answer prompts: accept defaults. This adds the `hlm`/`brain` libraries.

- [ ] **Step 4: Configure proxy to API backend**

Create `proxy.conf.json`:
```json
{
  "/api": { "target": "http://localhost:5000", "secure": false },
  "/auth": { "target": "http://localhost:5000", "secure": false },
  "/healthz": { "target": "http://localhost:5000", "secure": false }
}
```

Edit `angular.json`, under `projects.Limen.Frontend.architect.serve.options` add:
```json
"proxyConfig": "proxy.conf.json"
```

- [ ] **Step 5: Create auth service**

```ts
// src/app/core/auth/auth.service.ts
import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface AdminInfo { subject: string; email: string; expiresAt: string; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private _admin = signal<AdminInfo | null>(null);
  readonly admin = this._admin.asReadonly();

  constructor(private http: HttpClient) {}

  async refresh(): Promise<AdminInfo | null> {
    try {
      const me = await firstValueFrom(this.http.get<AdminInfo>('/auth/me'));
      this._admin.set(me);
      return me;
    } catch {
      this._admin.set(null);
      return null;
    }
  }

  login() { window.location.href = '/auth/login'; }

  async signOut() {
    await firstValueFrom(this.http.post('/auth/signout', {}));
    this._admin.set(null);
  }
}
```

- [ ] **Step 6: Create auth guard**

```ts
// src/app/core/auth/auth.guard.ts
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const me = await auth.refresh();
  if (!me) { router.navigate(['/login']); return false; }
  return true;
};
```

- [ ] **Step 7: Create login component**

```ts
// src/app/features/login/login.component.ts
import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'limen-login',
  standalone: true,
  template: `
    <div class="min-h-screen flex items-center justify-center bg-slate-50">
      <div class="p-8 bg-white rounded-lg shadow-md max-w-sm w-full text-center">
        <h1 class="text-2xl font-bold mb-4">Limen</h1>
        <p class="text-slate-600 mb-6">Sign in to manage your infrastructure.</p>
        <button (click)="auth.login()" class="w-full px-4 py-2 bg-slate-900 text-white rounded hover:bg-slate-800">
          Sign in with OIDC
        </button>
      </div>
    </div>`,
})
export class LoginComponent { auth = inject(AuthService); }
```

- [ ] **Step 8: Create dashboard component**

```ts
// src/app/features/dashboard/dashboard.component.ts
import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'limen-dashboard',
  standalone: true,
  template: `
    <div class="p-8">
      <header class="flex justify-between items-center mb-8">
        <h1 class="text-3xl font-bold">Limen</h1>
        <div class="flex items-center gap-4">
          <span class="text-sm text-slate-600">{{ auth.admin()?.email }}</span>
          <button (click)="auth.signOut()" class="px-3 py-1 text-sm border rounded">Sign out</button>
        </div>
      </header>
      <section class="text-slate-500">
        No nodes enrolled yet.
      </section>
    </div>`,
})
export class DashboardComponent { auth = inject(AuthService); }
```

- [ ] **Step 9: Wire routes**

```ts
// src/app/app.routes.ts
import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent) },
  { path: '', loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent), canActivate: [authGuard] },
];
```

- [ ] **Step 10: Build the Angular app**

```bash
npm run build
```
Expected: `dist/` folder produced.

- [ ] **Step 11: Serve static files from Limen.API**

Add this to `Limen.API/Program.cs` before `app.Run()`:

```csharp
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
```

Edit `src/Limen.API/Limen.API.csproj` to copy built Angular assets:

```xml
<Target Name="PublishAngular" BeforeTargets="PrepareForPublish">
  <Exec Command="npm ci" WorkingDirectory="../Limen.Frontend" />
  <Exec Command="npm run build -- --output-path=../Limen.API/wwwroot --base-href=/" WorkingDirectory="../Limen.Frontend" />
</Target>
```

- [ ] **Step 12: Commit**

```bash
cd ../..
git add src/Limen.Frontend/ src/Limen.API/
git commit -m "feat(frontend): Angular 21 shell with Tailwind 4, spartan.ng, auth service + guard, login + dashboard"
```

---

## Task 11: Docker image + compose

**Files:**
- Create: `limen/src/Limen.API/Dockerfile`
- Create: `limen/compose.yml`
- Create: `limen/compose.dev.yml`
- Create: `limen/template.env`

- [ ] **Step 1: Create `Dockerfile`**

```dockerfile
# src/Limen.API/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Node for Angular build
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && apt-get install -y nodejs

COPY Directory.Build.props Limen.slnx ./
COPY contracts/ ./contracts/
COPY src/ ./src/
RUN dotnet publish src/Limen.API/Limen.API.csproj -c Release -o /app --no-restore=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Limen.API.dll"]
```

- [ ] **Step 2: Create `compose.yml`**

```yaml
name: limen
services:
  postgres:
    image: postgres:16-alpine
    restart: unless-stopped
    environment:
      POSTGRES_DB: limen
      POSTGRES_USER: limen
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?set POSTGRES_PASSWORD in .env}
    volumes:
      - limen_pg:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U limen"]
      interval: 10s
      retries: 5

  limen:
    image: ghcr.io/getlimen/limen:latest
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ConnectionStrings__Postgres: "Host=postgres;Database=limen;Username=limen;Password=${POSTGRES_PASSWORD}"
      Oidc__Authority: ${OIDC_AUTHORITY}
      Oidc__ClientId: ${OIDC_CLIENT_ID}
      Oidc__ClientSecret: ${OIDC_CLIENT_SECRET}
    ports:
      - "8080:8080"

volumes:
  limen_pg:
```

- [ ] **Step 3: Create `compose.dev.yml`**

```yaml
name: limen-dev
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: limen_dev
      POSTGRES_USER: limen
      POSTGRES_PASSWORD: limen
    ports:
      - "5433:5432"
```

- [ ] **Step 4: Create `template.env`**

```env
# Copy to .env and fill in values before running `docker compose up`
POSTGRES_PASSWORD=change-me-strong-password
OIDC_AUTHORITY=https://your-pocket-id.example.com
OIDC_CLIENT_ID=limen
OIDC_CLIENT_SECRET=change-me
```

- [ ] **Step 5: Verify local dev compose works**

```bash
docker compose -f compose.dev.yml up -d
sleep 2
docker compose -f compose.dev.yml ps
```
Expected: `postgres` in `running` state.

- [ ] **Step 6: Apply migration against dev DB**

```bash
dotnet ef database update --project src/Limen.Infrastructure --startup-project src/Limen.API
```
Expected: `Applying migration '<timestamp>_Initial'. Done.`

- [ ] **Step 7: Commit**

```bash
docker compose -f compose.dev.yml down
git add src/Limen.API/Dockerfile compose.yml compose.dev.yml template.env
git commit -m "feat(deploy): Dockerfile + compose.yml + dev overrides"
```

---

## Task 12: CI pipeline

**Files:**
- Create: `limen/.github/workflows/ci.yml`

- [ ] **Step 1: Create CI workflow**

```yaml
# .github/workflows/ci.yml
name: CI
on:
  push: { branches: [main] }
  pull_request: { branches: [main] }

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - uses: actions/setup-node@v4
        with: { node-version: '20' }

      - run: dotnet restore
      - run: dotnet build --configuration Release --no-restore

      - name: Run tests
        run: dotnet test --configuration Release --no-build --verbosity normal

      - name: Build frontend
        working-directory: src/Limen.Frontend
        run: |
          npm ci
          npm run build

  docker:
    needs: build-test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    permissions: { contents: read, packages: write }
    steps:
      - uses: actions/checkout@v4
      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      - uses: docker/build-push-action@v6
        with:
          context: .
          file: src/Limen.API/Dockerfile
          push: true
          tags: |
            ghcr.io/${{ github.repository_owner }}/limen:latest
            ghcr.io/${{ github.repository_owner }}/limen:sha-${{ github.sha }}
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: build, test, and push Docker image to ghcr.io on main"
```

---

## Task 13: Final verification

- [ ] **Step 1: `dotnet build` from repo root**

```bash
dotnet build
```
Expected: all 5 projects build with 0 errors, 0 warnings.

- [ ] **Step 2: `dotnet test`**

```bash
dotnet test
```
Expected: all tests pass.

- [ ] **Step 3: Start dev DB, apply migrations, run the app**

```bash
docker compose -f compose.dev.yml up -d
dotnet ef database update --project src/Limen.Infrastructure --startup-project src/Limen.API
cd src/Limen.API && dotnet run
```

In another terminal, from `src/Limen.Frontend`:
```bash
npm run start
```

Open http://localhost:4200 — should redirect to `/login` showing the Limen login page. Clicking sign-in should redirect to your OIDC provider.

- [ ] **Step 4: Stop everything + commit any doc updates**

```bash
docker compose -f compose.dev.yml down
```

- [ ] **Step 5: Push and confirm CI goes green**

```bash
git push origin main
```
Verify the CI run on GitHub completes successfully and an image appears at `ghcr.io/getlimen/limen:latest`.

---

## Exit criteria for Plan 1

✅ Repo scaffolded under `getlimen/limen`
✅ Clean architecture with strict layer rules (Domain/Application/Infrastructure/API/Frontend)
✅ Commands/Queries in separate folders, one file per command/query
✅ `Limen.Contracts` NuGet source in the repo
✅ OIDC login flow end-to-end (sign in → cookie → `/auth/me` works)
✅ Angular shell with login + empty dashboard
✅ Postgres via compose.yml
✅ CI builds, tests, pushes Docker image
✅ All tests pass

**Plan 2 unlocks next:** agent control channel via WebSocket + provisioning key enrollment.
