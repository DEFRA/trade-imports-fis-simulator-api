using System.Diagnostics;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace TradeImportsFisSimulatorApi.IntegrationTests;

/// <summary>
/// Reads from the queue subscribed to the IUU notification topic in compose/scripts/ready.d/10-init-floci.sh.
/// </summary>
public abstract class SqsTestBase : IntegrationTestBase
{
    private const string QueueUrl =
        "http://sqs.eu-west-2.127.0.0.1:4566/000000000000/trade_imports_iuunotification_sim_upserted_queue";

    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(30);

    private readonly AmazonSQSClient _sqsClient = new(
        new BasicAWSCredentials("test", "test"),
        new AmazonSQSConfig
        {
            AuthenticationRegion = "eu-west-2",
            ServiceURL = "http://localhost:4566",
            Timeout = TimeSpan.FromSeconds(5),
            MaxErrorRetry = 0,
        }
    );

    /// <summary>
    /// Waits for the message published for the given resource, deleting any messages read along the way
    /// so that the queue does not fill up across tests.
    /// </summary>
    protected async Task<Message> WaitForMessage(string resourceId, string operation)
    {
        var timer = Stopwatch.StartNew();

        while (timer.Elapsed < s_timeout)
        {
            var response = await _sqsClient.ReceiveMessageAsync(
                new ReceiveMessageRequest
                {
                    QueueUrl = QueueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 1,
                    MessageAttributeNames = ["All"],
                },
                TestContext.Current.CancellationToken
            );

            Message? match = null;
            foreach (var message in response.Messages ?? [])
            {
                await _sqsClient.DeleteMessageAsync(
                    QueueUrl,
                    message.ReceiptHandle,
                    TestContext.Current.CancellationToken
                );

                if (
                    message.MessageAttributes.TryGetValue("ResourceId", out var id)
                    && id.StringValue == resourceId
                    && message.Body.Contains($"\"operation\":\"{operation}\"", StringComparison.Ordinal)
                )
                {
                    match = message;
                }
            }

            if (match is not null)
            {
                return match;
            }
        }

        throw new TimeoutException($"No {operation} message for {resourceId} received within {s_timeout}");
    }
}
