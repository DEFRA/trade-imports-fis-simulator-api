using Defra.TradeImportsDataApi.Domain.Events;
using Defra.TradeImportsDataApi.Domain.Iuu;
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

    private static async Task<Results<Ok<IReadOnlyCollection<IuuNotificationEvent>>, ValidationProblem>> GetLatest(
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

        var notifications = await repository.GetByCreatedAsync(skip, limit, parsedSortOrder, cancellationToken);
        return TypedResults.Ok(notifications);
    }

    private static async Task<Results<Ok<IuuNotificationEvent>, NotFound>> Get(
        [FromRoute] string reference,
        [FromServices] IIuuNotificationRepository repository,
        CancellationToken cancellationToken
    )
    {
        var notification = await repository.GetAsync(reference, cancellationToken);
        return notification is null ? TypedResults.NotFound() : TypedResults.Ok(notification);
    }

    private static async Task<Results<Created<ResourceEvent<IuuNotificationEvent>>, Conflict<ProblemDetails>>> Create(
        [FromRoute] string reference,
        IuuNotificationRequest request,
        [FromServices] IIuuNotificationRepository repository,
        [FromServices] IIuuNotificationPublisher publisher,
        [FromServices] IIuuReferenceCounter referenceCounter,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.UtcNow;
        var notification = new IuuNotificationEvent
        {
            Id = reference,
            Etag = NewEtag(),
            Created = now,
            Updated = now,
            IuuNotification = new IuuNotification
            {
                Reference = reference,
                LastUpdated = now,
                IuuOption = request.IuuOption,
            },
        };

        try
        {
            await repository.CreateAsync(notification, cancellationToken);
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

        var resourceEvent = ToResourceEvent(notification, ResourceEventOperations.Created);
        await publisher.PublishAsync(resourceEvent, cancellationToken);

        return TypedResults.Created($"/iuunotification/{reference}", resourceEvent);
    }

    private static async Task<Results<Ok<ResourceEvent<IuuNotificationEvent>>, NotFound>> Update(
        [FromRoute] string reference,
        IuuNotificationRequest request,
        [FromServices] IIuuNotificationRepository repository,
        [FromServices] IIuuNotificationPublisher publisher,
        CancellationToken cancellationToken
    )
    {
        var notification = await repository.UpdateAsync(
            reference,
            request.IuuOption,
            NewEtag(),
            DateTime.UtcNow,
            cancellationToken
        );

        if (notification is null)
        {
            return TypedResults.NotFound();
        }

        var resourceEvent = ToResourceEvent(notification, ResourceEventOperations.Updated);
        await publisher.PublishAsync(resourceEvent, cancellationToken);

        return TypedResults.Ok(resourceEvent);
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

    private static ResourceEvent<IuuNotificationEvent> ToResourceEvent(
        IuuNotificationEvent notification,
        string operation
    )
    {
        return new ResourceEvent<IuuNotificationEvent>
        {
            ResourceId = notification.Id,
            ResourceType = ResourceEventResourceTypes.IuuNotification,
            Operation = operation,
            Resource = notification,
            Etag = notification.Etag,
            Timestamp = notification.Updated,
        };
    }

    private static string NewEtag() => Guid.NewGuid().ToString("N");
}
