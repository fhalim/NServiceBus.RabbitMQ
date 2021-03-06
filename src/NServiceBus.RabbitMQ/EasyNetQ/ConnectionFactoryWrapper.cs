﻿namespace EasyNetQ
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Transports.RabbitMQ.Config;
    using RabbitMQ.Client;

    public class ConnectionFactoryWrapper : IConnectionFactory
    {
        public virtual IConnectionConfiguration Configuration { get; private set; }
        private readonly IClusterHostSelectionStrategy<ConnectionFactoryInfo> clusterHostSelectionStrategy;

        public ConnectionFactoryWrapper(IConnectionConfiguration connectionConfiguration, IClusterHostSelectionStrategy<ConnectionFactoryInfo> clusterHostSelectionStrategy)
        {
            this.clusterHostSelectionStrategy = clusterHostSelectionStrategy;
            if(connectionConfiguration == null)
            {
                throw new ArgumentNullException("connectionConfiguration");
            }
            if (!connectionConfiguration.Hosts.Any())
            {
                throw new EasyNetQException("At least one host must be defined in connectionConfiguration");
            }

            Configuration = connectionConfiguration;

            foreach (var hostConfiguration in Configuration.Hosts)
            {
                clusterHostSelectionStrategy.Add(new ConnectionFactoryInfo(new ConnectionFactory
                    {
                        HostName = hostConfiguration.Host,
                        Port = hostConfiguration.Port,
                        VirtualHost = Configuration.VirtualHost,
                        UserName = Configuration.UserName,
                        Password = Configuration.Password,
                        RequestedHeartbeat = Configuration.RequestedHeartbeat,
                        ClientProperties = ConvertToHashtable(Configuration.ClientProperties)
                    }, hostConfiguration));
            }
        }

        private static IDictionary<string, object> ConvertToHashtable(IDictionary<string, object> clientProperties)
        {
            var dictionary = new Dictionary<string, object>();
            foreach (var clientProperty in clientProperties)
            {
                dictionary.Add(clientProperty.Key, clientProperty.Value);
            }
            return dictionary;
        }

        public virtual IConnection CreateConnection() {
            var connectionFactoryInfo = clusterHostSelectionStrategy.Current();
            var connectionFactory = connectionFactoryInfo.ConnectionFactory;
            return connectionFactory.CreateConnection();
        }

        public virtual IHostConfiguration CurrentHost
        {
            get { return clusterHostSelectionStrategy.Current().HostConfiguration; }
        }

        public virtual bool Next()
        {
            return clusterHostSelectionStrategy.Next();
        }

        public virtual void Reset()
        {
            clusterHostSelectionStrategy.Reset();
        }

        public virtual void Success()
        {
            clusterHostSelectionStrategy.Success();
        }

        public virtual bool Succeeded
        {
            get { return clusterHostSelectionStrategy.Succeeded; }
        }
    }
}