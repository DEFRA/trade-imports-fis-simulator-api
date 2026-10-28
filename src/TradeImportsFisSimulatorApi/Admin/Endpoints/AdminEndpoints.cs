using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TradeImportsFisSimulatorApi.Admin.Services;
using TradeImportsFisSimulatorApi.IuuNotifications.Models;
using TradeImportsFisSimulatorApi.IuuNotifications.Services;

namespace TradeImportsFisSimulatorApi.Admin.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin").WithTags("Admin");

        group.MapPost("/reset", Reset);
        group.MapGet("/nextId", NextId);

        return group;
    }

    private static async Task<NoContent> Reset(
        [FromServices] IMongoResetService resetService,
        CancellationToken cancellationToken
    )
    {
        await resetService.ResetAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<NextIdResponse>> NextId(
        [FromServices] IIuuNotificationRepository repository,
        CancellationToken cancellationToken
    )
    {
        var references = await repository.GetAllReferencesAsync(cancellationToken);
        return TypedResults.Ok(new NextIdResponse(IuuReferenceGenerator.Next(references)));
    }
}
