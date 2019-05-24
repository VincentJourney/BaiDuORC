using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace BaiDuOCR
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();

            #region MQ
            var factory = new ConnectionFactory
            {
                HostName = "http://10.0.8.10",
                UserName = "yogya",
                Password = "123123"
            };
            using (var connect = factory.CreateConnection())
            {
                using (var channel = connect.CreateModel())
                {
                    channel.ExchangeDeclare(exchange: "OCR_Point", type: "diret");
                    channel.QueueDeclare(queue: "OCR_queue", durable: true, exclusive: false, autoDelete: false);
                    channel.QueueBind(queue: "OCR_queue", exchange: "ApplyPoint", "OCR");

                    channel.QueueDeclare(queue: "ApplyPoint_queue", durable: true, exclusive: false, autoDelete: false);
                    channel.QueueBind(queue: "ApplyPoint_queue", exchange: "ApplyPoint", "ApplyPoint");

                    channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += (model, ea) =>
                    {
                        var body = ea.Body;
                        var message = Encoding.UTF8.GetString(body);
                        Log.Warn($"{ea.RoutingKey}:{message}", null);


                        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    };
                    channel.BasicConsume(queue: "OCR_queue",
                                         autoAck: false,
                                         consumer: consumer);

                }
            }
            #endregion
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build();
    }
}
