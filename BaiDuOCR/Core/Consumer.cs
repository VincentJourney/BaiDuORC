using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace BaiDuOCR.Core
{
    public static class Consumer
    {
        public static void Action()
        {
            var factory = new ConnectionFactory
            {
                HostName = "10.0.8.10",
                UserName = "yogya",
                Password = "123123"
            };

            using (var connection = factory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.ExchangeDeclare(exchange: "BaiduOCR", type: "direct");

                    channel.QueueDeclare(queue: "OCR_QueueA", durable: true, exclusive: false, autoDelete: false, arguments: null);

                    channel.QueueBind(queue: "OCR_QueueA", exchange: "BaiduOCR", routingKey: "OCR_routingA");

                    channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);//同时处理一个

                    var consumer = new EventingBasicConsumer(channel);

                    consumer.Received += (model, ea) =>
                    {
                        var message = Encoding.UTF8.GetString(ea.Body);
                        Log.Warn($"Consumer-Message:{message}", null);



                        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    };
                    channel.BasicConsume(queue: "queueA",
                                         autoAck: false,
                                         consumer: consumer);

                    Console.ReadLine();
                }
            }

        }
    }
}
