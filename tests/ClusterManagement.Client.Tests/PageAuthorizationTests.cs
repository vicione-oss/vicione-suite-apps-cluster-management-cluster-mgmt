using System.Reflection;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Authorization;

namespace ClusterManagement.Client.Tests;

public class PageAuthorizationTests
{
    public static TheoryData<Type> Pages => [
        .. typeof(ClusterManagementClientModule).Assembly.GetTypes().Where(t => t.GetCustomAttributes<RouteAttribute>().Any()),
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Refuses_a_user_without_full_access_to_the_module(Type page)
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.AddAuthorization().SetAuthorized("operator");

        // Act
        var sut = RenderRoute(ctx, page);

        // Assert
        sut.Markup.Should().Be("refused");
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Shows_the_page_to_a_user_with_full_access_to_the_module(Type page)
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.AddAuthorization().SetAuthorized("admin")
            .SetPolicies(ModulePolicyProvider.GetPolicy<ClusterManagementClientModule>(AccessLevel.Full)!);

        // Act
        var sut = RenderRoute(ctx, page);

        // Assert
        sut.Markup.Should().Be("page");
    }

    // The page itself is a stub, so the result depends on its authorization attributes only and not on its services.
    private static IRenderedComponent<AuthorizeRouteView> RenderRoute(BunitContext ctx, Type page)
    {
        ctx.ComponentFactories.AddStub(type => type == page, "page");
        return ctx.Render<AuthorizeRouteView>(p => p
            .Add(c => c.RouteData, new RouteData(page, new Dictionary<string, object?>()))
            .Add(c => c.NotAuthorized, _ => builder => builder.AddContent(0, "refused")));
    }
}
