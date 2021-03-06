﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Consumer.EventConsumer;
using EasyNetQ;
using EasyNetQ.AutoSubscribe;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Consumer.EasyNetQ
{
    public static class EasyNetQExtension
    {
        private static void InternalInitEasyNetQ(IServiceCollection service, string rabbitMqConnection)
        {
            var bus = RabbitHutch.CreateBus(rabbitMqConnection);
            service.AddSingleton<IBus>(bus);
            service.AddSingleton<IAutoSubscriberMessageDispatcher, ConsumerMessageDispatcher>(serviceProvider => new ConsumerMessageDispatcher(serviceProvider, serviceProvider.GetRequiredService<ILogger<ConsumerMessageDispatcher>>()));

            var consumerTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(x => x.IsClass && !x.IsAbstract && !x.IsInterface)
                .Where(x => x.BaseType.Name == typeof(EasyNetQConsumerBase<>).Name ||
                            x.GetInterfaces().Any(t => t.Name == typeof(IConsume<>).Name));

            foreach (var consumerType in consumerTypes)
            {
                service.AddTransient(consumerType);
                //service.AddTransient(typeof(IConsume<>), consumerType);
            }

            var consumerAsyncTypes = typeof(OrderCreatedEventConsumer).Assembly.GetTypes().Where(x => x.IsClass && !x.IsAbstract && !x.IsInterface)
                .Where(x => x.GetInterfaces().Any(t => t.Name == typeof(IConsumeAsync<>).Name));

            foreach (var consumerAsyncType in consumerAsyncTypes)
            {
                service.AddTransient(consumerAsyncType);
                //service.AddTransient(typeof(IConsumeAsync<>), consumerAsyncType);
            }
        }

        public static void AddEasyNetQ(this IServiceCollection service, Func<string> getRabbitMqConneciton)
        {
            InternalInitEasyNetQ(service, getRabbitMqConneciton());
        }

        public static void AddEasyNetQ(this IServiceCollection service, string rabbitMqConnectionString)
        {
            InternalInitEasyNetQ(service, rabbitMqConnectionString);
        }

        public static void UseEasyNetQ(this IApplicationBuilder app)
        {
            var bus = app.ApplicationServices.GetRequiredService<IBus>();
            var autoSubscriber = new AutoSubscriber(bus, "consumer")
            {
                AutoSubscriberMessageDispatcher = app.ApplicationServices.GetRequiredService<IAutoSubscriberMessageDispatcher>(),
                GenerateSubscriptionId = x => AppDomain.CurrentDomain.FriendlyName + x.ConcreteType.Name,
                ConfigureSubscriptionConfiguration = x => x.WithAutoDelete(true).WithDurable(true)
            };
            autoSubscriber.Subscribe(Assembly.GetExecutingAssembly());
            autoSubscriber.SubscribeAsync(Assembly.GetExecutingAssembly());
        }
    }
}
