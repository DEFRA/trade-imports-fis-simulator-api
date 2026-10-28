# trade-imports-fis-simulator-api

A simulator for the Fish Import Service (FIS). It lets other Trade Imports services be developed and tested
against IUU (Illegal, Unreported and Unregulated fishing) notifications without the real FIS.

When an IUU notification is created or updated, the simulator:

1. Stores it in MongoDB, in the `iuunotifications` collection.
2. Publishes a `ResourceEvent<IuuNotificationEvent>` to the `trade_imports_iuunotification_sim_upserted` SNS topic.

The event and notification types come from the
[`Defra.TradeImportsDataApi.Domain`](https://github.com/DEFRA/trade-imports-data-api/tree/main/src/Domain) package, so
messages have the same shape as those published by the Trade Imports Data API.

* [Endpoints](#endpoints)
* [Published messages](#published-messages)
* [Solution layout](#solution-layout)
* [Configuration](#configuration)
* [Running locally](#running-locally)
* [Testing](#testing)
* [Build and deployment](#build-and-deployment)

## Endpoints

| Method   | Path                            | Description                                                                                                                         | Responses                                                            |
|----------|---------------------------------|-------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------|
| `GET`    | `/iuunotification`              | Returns stored notifications ordered by creation time. See [query parameters](#listing-notifications).                            | `200` with an array of notifications, `400` if a parameter is invalid |
| `POST`   | `/iuunotification/{reference}`  | Creates a notification, stores it and publishes a `Created` event.                                                                 | `201` with the event, `409` if the reference already exists          |
| `PUT`    | `/iuunotification/{reference}`  | Updates a notification, stores the change and publishes an `Updated` event.                                                         | `200` with the event, `404` if the reference does not exist          |
| `DELETE` | `/iuunotification/{reference}`  | Deletes a notification from MongoDB. No event is published.                                                                         | `204`, `404` if the reference does not exist                         |
| `POST`   | `/admin/reset`                  | Deletes every document in every collection in the simulator's database. Indexes are kept.                                          | `204`                                                                |
| `GET`    | `/admin/nextId`                 | Returns the next reference to use: the stored reference with the largest trailing number, plus one, keeping its prefix and padding. | `200` with `{ "nextId": "IUU.GB.2026.0000042" }`, or `"1"` if empty |
| `GET`    | `/health`                       | Health check.                                                                                                                       | `200`                                                                |

`POST` and `PUT` take the IUU check outcome as the request body:

```json
{ "iuuOption": "IUUOK" }
```

The values the domain package lists are `IUUOK`, `IUUNA` and `IUUNotCompliant`. The simulator does not validate the
value, so other values can be sent to test how consumers handle them.

Example:

```bash
curl -X POST http://localhost:8085/iuunotification/IUU.GB.2026.0000001 \
  -H "Content-Type: application/json" \
  -H "x-cdp-request-id: my-trace-id" \
  -d '{ "iuuOption": "IUUOK" }'
```

### Listing notifications

`GET /iuunotification` returns the stored `IuuNotificationEvent` documents, ordered by their `created` time. With no
query string it returns the 10 most recently created. Notifications created at the same time are ordered by reference.

| Parameter   | Description                                        | Default |
|-------------|----------------------------------------------------|---------|
| `skip`      | Number of notifications to skip. 0 or more.        | `0`     |
| `limit`     | Maximum number of notifications to return. 1 or more. | `10`  |
| `sortOrder` | `asc` (oldest first) or `desc` (newest first). Not case sensitive. | `desc` |

```bash
curl "http://localhost:8085/iuunotification?skip=10&limit=5&sortOrder=desc"
```

## Published messages

Each message body is a camelCase JSON `ResourceEvent<IuuNotificationEvent>`:

```json
{
  "resourceId": "IUU.GB.2026.0000001",
  "resourceType": "IuuNotification",
  "subResourceType": null,
  "operation": "Created",
  "resource": {
    "id": "IUU.GB.2026.0000001",
    "etag": "5e4361d606134150a2ee778b162cb756",
    "created": "2026-10-28T06:49:21.6869234Z",
    "updated": "2026-10-28T06:49:21.6869234Z",
    "iuuNotification": {
      "reference": "IUU.GB.2026.0000001",
      "lastUpdated": "2026-10-28T06:49:21.6869234Z",
      "iuuOption": "IUUOK"
    }
  },
  "etag": "5e4361d606134150a2ee778b162cb756",
  "timestamp": "2026-10-28T06:49:21.6869234Z",
  "changeSet": []
}
```

Each message has these SNS message attributes, matching the Trade Imports Data API:

| Attribute          | Value                                                                                 |
|--------------------|---------------------------------------------------------------------------------------|
| `MessageType`      | The `ResourceEvent<IuuNotificationEvent>` type name, without version or key tokens    |
| `ResourceType`     | `IuuNotification`                                                                     |
| `ResourceId`       | The notification reference                                                            |
| `x-cdp-request-id` | The trace id from the incoming request's `x-cdp-request-id` header, when it was sent  |

## Solution layout

```
src/TradeImportsFisSimulatorApi                   The API
  Admin/                                          /admin endpoints and the MongoDB reset service
  IuuNotifications/
    Endpoints/                                    /iuunotification endpoints
    Models/                                       Request and response models
    Services/                                     MongoDB repository, SNS publisher and reference generator
  Config/                                         Strongly typed configuration
  Utils/                                          CDP template utilities: logging, tracing, MongoDB, HTTP, trust store
tests/TradeImportsFisSimulatorApi.Test            Unit tests
tests/TradeImportsFisSimulatorApi.IntegrationTests Integration tests, run against Docker Compose
compose/                                          floci setup script and AWS environment for Docker Compose
```

## Configuration

| Setting                            | Description                                                                 | Default                                                                                 |
|------------------------------------|-----------------------------------------------------------------------------|-----------------------------------------------------------------------------------------|
| `Mongo:DatabaseUri`                | MongoDB connection string                                                   | Set automatically in CDP                                                                |
| `Mongo:DatabaseName`               | MongoDB database                                                            | `trade-imports-fis-simulator-api`                                                       |
| `IuuNotificationTopic:TopicArn`    | ARN of the topic events are published to                                    | `arn:aws:sns:eu-west-2:000000000000:trade_imports_iuunotification_sim_upserted` (local) |
| `TraceHeader`                      | Header holding the trace id, which is copied onto published messages        | `x-cdp-request-id`                                                                      |
| `AWS:ServiceUrl`                   | AWS endpoint override, used to point at floci                               | `http://localhost:4566` in Development                                                  |
| `AWS:AuthenticationRegion`         | Region used to sign requests to floci                                       | `eu-west-2` in Development                                                              |

The topic ARN must be set to the real ARN in each CDP environment.

## Running locally

### Docker Compose

[compose.yml](compose.yml) starts a local environment with:

- [floci](https://floci.io), a LocalStack replacement, for SNS and SQS.
  [compose/scripts/ready.d/10-init-floci.sh](compose/scripts/ready.d/10-init-floci.sh) creates the
  `trade_imports_iuunotification_sim_upserted` topic and subscribes the
  `trade_imports_iuunotification_sim_upserted_queue` queue to it, so published messages can be read back.
- MongoDB, as a single node replica set.
- This service, on port 8085.

The image restores packages from the DEFRA GitHub NuGet feed, so set `DEFRA_NUGET_PAT` to a GitHub token with
`read:packages` first:

```bash
docker compose up --build -d --wait
```

To read the messages the simulator has published:

```bash
aws --endpoint-url=http://localhost:4566 sqs receive-message \
  --queue-url http://localhost:4566/000000000000/trade_imports_iuunotification_sim_upserted_queue \
  --message-attribute-names All
```

### From your IDE or the command line

Start MongoDB and floci, then run the API with the `Development` launch profile. It listens on
`http://localhost:8085`:

```bash
docker compose up -d --wait mongodb floci
dotnet run --project src/TradeImportsFisSimulatorApi
```

### Inspect MongoDB

```bash
mongosh "mongodb://127.0.0.1:27017/trade-imports-fis-simulator-api?directConnection=true"
```

You can use the CDP Terminal to access MongoDB in CDP environments.

## Testing

Unit tests run the API in memory with `WebApplicationFactory`, with MongoDB and SNS replaced by substitutes. They cover
the endpoints, the SNS publisher (message body, attributes and trace id) and the reference generator:

```bash
dotnet test --filter "Category!=IntegrationTest"
```

Integration tests run against the Docker Compose environment. They call the API on port 8085, check MongoDB directly
and read published messages from the SQS queue in floci:

```bash
docker compose up --build -d --wait
dotnet test --filter "Category=IntegrationTest"
```

Code is formatted with [CSharpier](https://csharpier.com/), and the Docker build fails if it is not formatted:

```bash
dotnet tool restore
dotnet csharpier format .
```

## Build and deployment

- The [Dockerfile](Dockerfile) checks formatting, runs the unit tests and publishes the API.
- [check-pull-request.yml](.github/workflows/check-pull-request.yml) runs the unit and integration tests, builds the
  image and scans it with Trivy.
- [publish.yml](.github/workflows/publish.yml) runs the tests and builds and publishes the image to CDP on every push to
  `main`. [publish-hotfix.yml](.github/workflows/publish-hotfix.yml) publishes a hotfix on demand.
- [sonarcloud.yml](.github/workflows/sonarcloud.yml) runs the SonarCloud scan. It is commented out in
  `check-pull-request.yml` and `publish.yml` for now. To enable it, set up the `DEFRA_trade-imports-fis-simulator-api`
  project in SonarCloud, add the `SONAR_TOKEN` repository secret and uncomment the `sonarcloud-scan` jobs.
- Dependabot is configured in [.github/dependabot.yml](.github/dependabot.yml).

## About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable
information providers in the public sector to license the use and re-use of their information under a common open
licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
