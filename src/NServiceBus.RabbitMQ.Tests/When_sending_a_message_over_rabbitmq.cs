﻿namespace NServiceBus.Transports.RabbitMQ.Tests
{
    using System;
    using System.Text;
    using System.Transactions;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using NUnit.Framework;
    using Unicast;
    using Unicast.Queuing;

    [TestFixture]
    class When_sending_a_message_over_rabbitmq : RabbitMqContext
    {
        

        [Test]
        public void Should_populate_the_body()
        {
            var body = Encoding.UTF8.GetBytes("<TestMessage/>");

            Verify(new TransportMessageBuilder().WithBody(body),
                 received => Assert.AreEqual(body, received.Body));
        }


        [Test]
        public void Should_set_the_content_type()
        {
            VerifyRabbit(new TransportMessageBuilder().WithHeader(Headers.ContentType, "application/json"),
                received => Assert.AreEqual("application/json", received.BasicProperties.ContentType));

        }

        
        [Test]
        public void Should_default_the_content_type_to_octet_stream_when_no_content_type_is_specified()
        {
            VerifyRabbit(new TransportMessageBuilder(),
                received => Assert.AreEqual("application/octet-stream", received.BasicProperties.ContentType));

        }

        

        [Test]
        public void Should_set_the_message_type_based_on_the_encoded_message_types_header()
        {
            var messageType = typeof (MyMessage);

            VerifyRabbit(new TransportMessageBuilder().WithHeader(Headers.EnclosedMessageTypes, messageType.AssemblyQualifiedName),
                received => Assert.AreEqual(messageType.FullName, received.BasicProperties.Type));

        }

        [Test]
        public void Should_set_the_time_to_be_received()
        {

            var timeToBeReceived = TimeSpan.FromDays(1);


            VerifyRabbit(new TransportMessageBuilder().TimeToBeReceived(timeToBeReceived),
                received => Assert.AreEqual(timeToBeReceived.TotalMilliseconds.ToString(), received.BasicProperties.Expiration));
        }

        [Test]
        public void Should_set_the_reply_to_address()
        {
            var address = Address.Parse("myAddress");

            Verify(new TransportMessageBuilder().ReplyToAddress(address), 
                (t, r) =>
                {
                    Assert.AreEqual(address, t.ReplyToAddress);
                    Assert.AreEqual(address.Queue, r.BasicProperties.ReplyTo);
                });

        }

       
        [Test]
        public void Should_set_correlation_id_if_present()
        {
            var correlationId = Guid.NewGuid().ToString();

            Verify(new TransportMessageBuilder().CorrelationId(correlationId),
                result => Assert.AreEqual(correlationId, result.CorrelationId));

        }

        [Test]
        public void Should_transmit_all_transportMessage_headers()
        {

            Verify(new TransportMessageBuilder().WithHeader("h1", "v1").WithHeader("h2", "v2"),
                result =>
                {
                    Assert.AreEqual("v1", result.Headers["h1"]);
                    Assert.AreEqual("v2", result.Headers["h2"]);
                });

        }
        [Test]
        public void Should_defer_the_send_until_tx_commit_if_ambient_tx_exists()
        {
            var body = Encoding.UTF8.GetBytes("<TestMessage/>");
            var body2 = Encoding.UTF8.GetBytes("<TestMessage2/>");

            var message = new TransportMessageBuilder().WithBody(body).Build();
            var message2 = new TransportMessageBuilder().WithBody(body2).Build();

            using (var tx = new TransactionScope())
            {
                SendMessage(message);
                SendMessage(message2);
                Assert.Throws<InvalidOperationException>(()=>Consume(message.Id));
               
                tx.Complete();
            }

            Assert.AreEqual(body, Consume(message.Id).Body,"Message should be in the queue");
            Assert.AreEqual(body2, Consume(message2.Id).Body, "Message2 should be in the queue");
        }

        [Test]
        public void Should_not_send_message_if_ambient_tx_is_rolled_back()
        {
            var body = Encoding.UTF8.GetBytes("<TestMessage/>");

            var message = new TransportMessageBuilder().WithBody(body).Build();

            using (new TransactionScope())
            {
                SendMessage(message);
                Assert.Throws<InvalidOperationException>(() => Consume(message.Id));
            }

            Assert.Throws<InvalidOperationException>(() => Consume(message.Id));

        }



        [Test, Ignore("Not sure we should enforce this")]
        public void Should_throw_when_sending_to_a_non_existing_queue()
        {
            Assert.Throws<QueueNotFoundException>(() =>
                 sender.Send(new TransportMessage(), new SendOptions("NonExistingQueue@localhost")));
        }

        void Verify(TransportMessageBuilder builder, Action<TransportMessage, BasicDeliverEventArgs> assertion)
        {
            var message = builder.Build();

            SendMessage(message);

            var result = Consume(message.Id);

            assertion(RabbitMqTransportMessageExtensions.ToTransportMessage(result), result);
        }
        void Verify(TransportMessageBuilder builder, Action<TransportMessage> assertion)
        {
            Verify(builder, (t, r) => assertion(t));
        }

        void VerifyRabbit(TransportMessageBuilder builder, Action<BasicDeliverEventArgs> assertion)
        {
            Verify(builder, (t, r) => assertion(r));
        }



        void SendMessage(TransportMessage message)
        {
            MakeSureQueueAndExchangeExists("testEndPoint");

            sender.Send(message, new SendOptions("testEndPoint"));
        }

        BasicDeliverEventArgs Consume(string id)
        {

            using (var channel = connectionManager.GetConsumeConnection().CreateModel())
            {
                var consumer = new QueueingBasicConsumer(channel);

                channel.BasicConsume("testEndPoint", false, consumer);

                BasicDeliverEventArgs message;

                if (!consumer.Queue.Dequeue(1000, out message))
                    throw new InvalidOperationException("No message found in queue");

                var e = message;

                if (e.BasicProperties.MessageId != id)
                    throw new InvalidOperationException("Unexpected message found in queue");

                channel.BasicAck(e.DeliveryTag,false);

                return e;
            }
        }



        class MyMessage
        {
            
        }

    }
}