using System.Reflection;
using NetArchTest.Rules;

namespace Architecture;

/// <summary>
/// Enforces the dependency-direction rule documented in
/// docs/architecture/backend-architecture.md: Domain has zero outward
/// framework dependencies, and Application never reaches into
/// Infrastructure. Fails the build if either boundary erodes.
///
/// Loaded by assembly name rather than a marker type so these rules hold
/// even while Domain/Application are still empty projects (Phase 1 has not
/// added any types yet) — referencing the project via ProjectReference
/// guarantees the assembly is present in the test's output directory.
/// </summary>
public class DependencyDirectionTests
{
    private static Assembly DomainAssembly => Assembly.Load("Domain");
    private static Assembly ApplicationAssembly => Assembly.Load("Application");

    [Fact]
    public void Domain_Should_Not_DependOn_EntityFrameworkCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Domain_Should_Not_DependOn_AspNetCore()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_Should_Not_DependOn_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn("Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
