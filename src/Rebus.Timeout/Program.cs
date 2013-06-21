﻿using System;
using System.IO;
using Rebus.Log4Net;
using Rebus.Logging;
using Rebus.MongoDb;
using Rebus.Persistence.InMemory;
using Rebus.Persistence.SqlServer;
using Rebus.Timeout.Configuration;
using Topshelf;
using log4net.Config;

namespace Rebus.Timeout
{
    class Program
    {
        static ILog log;

        static Program()
        {
            RebusLoggerFactory.Changed += f => log = f.GetCurrentClassLogger();
        }

        static void Main()
        {
            XmlConfigurator.ConfigureAndWatch(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config")));

            RebusLoggerFactory.Current = new Log4NetLoggerFactory();

            HostFactory
                .Run(s =>
                         {
                             const string text = "Rebus Timeout Service";

                             s.SetDescription("Rebus Timeout Service - Install named instance by adding '/instance:\"myInstance\"' when installing.");
                             s.SetDisplayName(text);
                             s.SetInstanceName("default");
                             s.SetServiceName("rebus_timeout_service");

                             s.Service<TimeoutService>(c =>
                                                           {
                                                               c.ConstructUsing(CreateTimeoutService);
                                                               c.WhenStarted(t => t.Start());
                                                               c.WhenStopped(t => t.Stop());
                                                           });

                             s.DependsOnMsmq();
                         });
        }

        static TimeoutService CreateTimeoutService()
        {
            try
            {
                var configuration = TimeoutConfigurationSection.GetSection();

                if (configuration == null)
                {
                    log.Warn(@"The timeout manager will use the MSMQ queue '{0}' as its input queue because the input queue name has not been explicitly configured.

The timeout manager will use the in-memory timeout storage, which is NOT suitable for production use. For production use, you should use a SQL Server (e.g. a locally installed SQL Express) or another durable database.

If you want to configure the timeout manager for production use, you can use one the following examples to get started:

    <timeout inputQueue=""rebus.timeout.input"" errorQueue=""rebus.timeout.error"" storageType=""SQL"" 
             connectionString=""server=.;initial catalog=RebusTimeoutManager;integrated security=sspi""
             tableName=""timeouts"" />

to use the 'timeouts' table in the RebusTimeoutManager database to store timeouts. If the specified table does not exist, the timeout manager will try to create it automatically.

You can also configure the timeout manager to store timeouts in MongoDB like this:

    <timeout inputQueue=""rebus.timeout.input"" errorQueue=""rebus.timeout.error"" storageType=""mongodb"" 
             connectionString=""mongodb://localhost/SomeDatabase""
             tableName=""timeouts"" />

to use the 'timeouts' collection in the SomeDatabase database to store timeouts. Please don't use the collection to store anything besides Rebus' timeouts, because otherwise it might lead to unexpected behavior.",
                        TimeoutService.DefaultInputQueueName);

                    var storage = new InMemoryTimeoutStorage();
                    var timeoutService = new TimeoutService(storage);

                    return timeoutService;
                }

                EnsureIsSet(configuration.InputQueue);
                EnsureIsSet(configuration.ErrorQueue);
                EnsureIsSet(configuration.StorageType);
                EnsureIsSet(configuration.TableName);

                switch (configuration.StorageType.ToLowerInvariant())
                {
                    case "sql":
                        log.Info("Using the SQL timeout storage - the table name '{0}' will be used", configuration.TableName);
                        return
                            new TimeoutService(
                                new SqlServerTimeoutStorage(configuration.ConnectionString, configuration.TableName)
                                    .EnsureTableIsCreated(), configuration.InputQueue, configuration.ErrorQueue);

                    case "mongodb":
                        log.Info("Using the MongoDB timeout storage - the collection name '{0}' will be used",
                                 configuration.TableName);
                        return new TimeoutService(
                            new MongoDbTimeoutStorage(configuration.ConnectionString, configuration.TableName),
                            configuration.InputQueue, configuration.ErrorQueue);

                    default:
                        throw new ArgumentException(
                            string.Format("Cannot use the value '{0}' as the storage type... sorry!",
                                          configuration.StorageType));
                }
            }
            catch (Exception e)
            {
                log.Error(e, "An error occurred while attempting to configure the timeout manager");
                throw;
            }
        }

        static void EnsureIsSet(string setting)
        {
            if (!string.IsNullOrWhiteSpace(setting)) return;

            throw new ArgumentException(string.Format(@"When you include the TimeoutConfigurationSection, you must specify input queue name, error queue name and a way to store the timeouts.

Take a look at this example configuration snippet

  <configSections>
    <section name=""timeout"" type=""Rebus.Timeout.Configuration.TimeoutConfigurationSection, Rebus.Timeout""/>
  </configSections>

  <timeout inputQueue=""rebus.timeout.input"" errorQueue=""rebus.timeout.error"" storageType=""SQL"" 
           connectionString=""server=.;initial catalog=RebusTimeoutManager;integrated security=sspi""
           tableName=""timeouts"" />

for inspiration on how it can be done.
"));
        }
    }
}
