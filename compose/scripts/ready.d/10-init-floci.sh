#!/bin/bash

set -e

TOPIC_NAME="trade_imports_iuunotification_sim_upserted"
QUEUE_NAME="trade_imports_iuunotification_sim_upserted_queue"

echo "Creating SNS topic $TOPIC_NAME..."
aws --endpoint-url="$AWS_ENDPOINT_URL" sns create-topic --name "$TOPIC_NAME"

# The queue is subscribed to the topic so that published messages can be read back,
# for example by the integration tests.
echo "Creating SQS queue $QUEUE_NAME..."
aws --endpoint-url="$AWS_ENDPOINT_URL" sqs create-queue --queue-name "$QUEUE_NAME"

echo "Subscribing $QUEUE_NAME to $TOPIC_NAME..."
aws --endpoint-url="$AWS_ENDPOINT_URL" sns subscribe \
    --topic-arn "arn:aws:sns:eu-west-2:000000000000:$TOPIC_NAME" \
    --protocol sqs \
    --notification-endpoint "arn:aws:sqs:eu-west-2:000000000000:$QUEUE_NAME" \
    --attributes '{"RawMessageDelivery": "true"}'
