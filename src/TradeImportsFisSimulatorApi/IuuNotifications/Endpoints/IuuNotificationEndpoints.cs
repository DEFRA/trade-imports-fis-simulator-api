using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TradeImportsFisSimulatorApi.IuuNotifications.Models;
using TradeImportsFisSimulatorApi.IuuNotifications.Services;

namespace TradeImportsFisSimulatorApi.IuuNotifications.Endpoints;

public static class IuuNotificationEndpoints
{
    public static RouteGroupBuilder MapIuuNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/iuunotification").WithTags("IuuNotification");

        group.MapGet("/", GetLatest);
        group.MapGet("/{reference}", Get);
        group.MapPost("/{reference}", Create);
        group.MapPut("/{reference}", Update);
        group.MapDelete("/{reference}", Delete);

        return group;
    }

    private static async Task<Results<Ok<IuuNotification[]>, ValidationProblem>> GetLatest(
        [FromServices] IIuuNotificationRepository repository,
        CancellationToken cancellationToken,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 10,
        [FromQuery] string sortOrder = nameof(SortOrder.Desc)
    )
    {
        var errors = new Dictionary<string, string[]>();
        // Parsed here rather than bound as an enum, as enum binding is case sensitive.
        if (!Enum.TryParse<SortOrder>(sortOrder, ignoreCase: true, out var parsedSortOrder))
        {
            errors[nameof(sortOrder)] = ["sortOrder must be asc or desc"];
        }

        if (skip < 0)
        {
            errors[nameof(skip)] = ["skip must be 0 or more"];
        }

        if (limit < 1)
        {
            errors[nameof(limit)] = ["limit must be 1 or more"];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var documents = await repository.GetByCreatedAsync(skip, limit, parsedSortOrder, cancellationToken);
        return TypedResults.Ok(documents.Select(d => d.IuuNotification).ToArray());
    }

    private static async Task<Results<Ok<IuuNotification>, NotFound>> Get(
        [FromRoute] string reference,
        [FromServices] IIuuNotificationRepository repository,
        CancellationToken cancellationToken
    )
    {
        var document = await repository.GetAsync(reference, cancellationToken);
        return document is null ? TypedResults.NotFound() : TypedResults.Ok(document.IuuNotification);
    }

    private static async Task<Results<Created<IuuNotification>, Conflict<ProblemDetails>, ValidationProblem>> Create(
        [FromRoute] string reference,
        IuuNotificationRequest request,
        [FromServices] IIuuNotificationRepository repository,
        [FromServices] IIuuNotificationPublisher publisher,
        [FromServices] IIuuReferenceCounter referenceCounter,
        CancellationToken cancellationToken
    )
    {
        if (IsBlank(request.IuuCheckOutcome))
        {
            return IuuCheckOutcomeBlank();
        }

        var now = UtcNowToMilliseconds();
        var document = new IuuNotificationDocument
        {
            Id = reference,
            Created = now,
            IuuNotification = new IuuNotification
            {
                Reference = reference,
                LastUpdated = now,
                IuuCheckOutcome = request.IuuCheckOutcome,
            },
        };

        try
        {
            await repository.CreateAsync(document, cancellationToken);
        }
        catch (IuuNotificationConflictException ex)
        {
            return TypedResults.Conflict(
                new ProblemDetails
                {
                    Title = "IUU notification already exists",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }

        await referenceCounter.RecordAsync(reference, cancellationToken);
        await publisher.PublishAsync(document.IuuNotification, cancellationToken);

        return TypedResults.Created($"/iuunotification/{reference}", document.IuuNotification);
    }

    private static async Task<Results<Ok<IuuNotification>, NotFound, ValidationProblem>> Update(
        [FromRoute] string reference,
        IuuNotificationRequest request,
        [FromServices] IIuuNotificationRepository repository,
        [FromServices] IIuuNotificationPublisher publisher,
        CancellationToken cancellationToken
    )
    {
        if (IsBlank(request.IuuCheckOutcome))
        {
            return IuuCheckOutcomeBlank();
        }

        var document = await repository.UpdateAsync(
            reference,
            request.IuuCheckOutcome,
            UtcNowToMilliseconds(),
            cancellationToken
        );

        if (document is null)
        {
            return TypedResults.NotFound();
        }

        await publisher.PublishAsync(document.IuuNotification, cancellationToken);

        return TypedResults.Ok(document.IuuNotification);
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        [FromRoute] string reference,
        [FromServices] IIuuNotificationRepository repository,
        CancellationToken cancellationToken
    )
    {
        var deleted = await repository.DeleteAsync(reference, cancellationToken);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    // MongoDB stores dates to the millisecond, so round to that before storing and publishing
    // so the published lastUpdated always matches the stored one.
    private static DateTime UtcNowToMilliseconds()
    {
        var now = DateTime.UtcNow;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
    }

    // Any value is accepted, so consumers can be tested with values they do not expect. Null is no outcome yet,
    // but a blank is refused, as it is more likely a mistake than a value to test with.
    private static bool IsBlank(string? iuuCheckOutcome) =>
        iuuCheckOutcome is not null && string.IsNullOrWhiteSpace(iuuCheckOutcome);

    private static ValidationProblem IuuCheckOutcomeBlank() =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]> { ["iuuCheckOutcome"] = ["iuuCheckOutcome must be null or not blank"] }
        );
}
