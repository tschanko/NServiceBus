﻿namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Transactions;
    using NServiceBus.Outbox;
    using NServiceBus.Pipeline;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Transport;

    class OutboxDeduplicationBehavior : PhysicalMessageProcessingStageBehavior
    {
        public OutboxDeduplicationBehavior(IOutboxStorage outboxStorage, DispatchMessageToTransportBehavior defaultDispatcher, DefaultMessageAuditer defaultAuditer, TransactionSettings transactionSettings)
        {
            this.outboxStorage = outboxStorage;
            this.defaultDispatcher = defaultDispatcher;
            this.defaultAuditer = defaultAuditer;
            this.transactionSettings = transactionSettings;
        }

        public override async Task Invoke(Context context, Func<Task> next)
        {
            var messageId = context.PhysicalMessage.Id;
            OutboxMessage outboxMessage;

            if (!outboxStorage.TryGet(messageId, out outboxMessage))
            {
                outboxMessage = new OutboxMessage(messageId);

                context.Set(outboxMessage);

                //we use this scope to make sure that we escalate to DTC if the user is talking to another resource by misstake
                // TODO: We need .NET 4.5.1 here!
                using (var checkForEscalationScope = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = transactionSettings.IsolationLevel, Timeout = transactionSettings.TransactionTimeout }))
                {
                    await next().ConfigureAwait(false);
                    checkForEscalationScope.Complete();
                }


                if (context.handleCurrentMessageLaterWasCalled)
                {
                    return;
                }
            }

            await DispatchOperationToTransport(outboxMessage.TransportOperations).ConfigureAwait(false);

            outboxStorage.SetAsDispatched(messageId);
        }

        async Task DispatchOperationToTransport(IEnumerable<TransportOperation> operations)
        {
            foreach (var transportOperation in operations)
            {
                var deliveryOptions = transportOperation.Options.ToDeliveryOptions();

                deliveryOptions.EnlistInReceiveTransaction = false;

                var message = new OutgoingMessage(transportOperation.MessageId, transportOperation.Headers, transportOperation.Body);

                var operationType = transportOperation.Options["Operation"];

                switch (operationType)
                {
                    case "Audit":
                        defaultAuditer.Audit(new TransportSendOptions(transportOperation.Options["Destination"],null,false,false), message);
                        break;
                    case "Send":
                        await defaultDispatcher.NativeSendOrDefer(deliveryOptions, message).ConfigureAwait(false);
                        break;
                    case "Publish":
                        
                        var options= new TransportPublishOptions(Type.GetType(transportOperation.Options["EventType"]));

                        await defaultDispatcher.NativePublish(options, message).ConfigureAwait(false);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown operation type: " + operationType);
                }
            }
        }

        readonly IOutboxStorage outboxStorage;
        readonly DispatchMessageToTransportBehavior defaultDispatcher;
        readonly DefaultMessageAuditer defaultAuditer;
        readonly TransactionSettings transactionSettings;

        public class OutboxDeduplicationRegistration : RegisterStep
        {
            public OutboxDeduplicationRegistration()
                : base("OutboxDeduplication", typeof(OutboxDeduplicationBehavior), "Deduplication for the outbox feature")
            {
                InsertBeforeIfExists(WellKnownStep.AuditProcessedMessage);
            }
        }
    }
}
