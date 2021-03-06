namespace SqlStreamStore.HAL.DevServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using Serilog;
    using SqlStreamStore.Infrastructure;

    internal static class SqlStreamStoreFactory
    {
        private delegate Task<IStreamStore> CreateStreamStore(
            string connectionString,
            string schema,
            CancellationToken cancellationToken);

        private const string SQLSTREAMSTORE_CONNECTION_STRING = nameof(SQLSTREAMSTORE_CONNECTION_STRING);
        private const string SQLSTREAMSTORE_SCHEMA = nameof(SQLSTREAMSTORE_SCHEMA);
        private const string SQLSTREAMSTORE_PROVIDER = nameof(SQLSTREAMSTORE_PROVIDER);

        private const string postgres = nameof(postgres);
        private const string mssql = nameof(mssql);
        private const string inmemory = nameof(inmemory);

        private static readonly IDictionary<string, CreateStreamStore> s_factories
            = new Dictionary<string, CreateStreamStore>
            {
                [inmemory] = CreateInMemoryStreamStore,
                [postgres] = CreatePostgresStreamStore,
                [mssql] = CreateMssqlStreamStore
            };

        public static Task<IStreamStore> Create(CancellationToken cancellationToken = default)
        {
            var provider = Environment.GetEnvironmentVariable(SQLSTREAMSTORE_PROVIDER)?.ToLowerInvariant()
                           ?? inmemory;

            Log.Information($"Creating stream store for provider '{provider}'");

            if(!s_factories.TryGetValue(provider, out var factory))
            {
                throw new InvalidOperationException($"No provider factory for provider '{provider}' found.");
            }

            var connectionString = Environment.GetEnvironmentVariable(SQLSTREAMSTORE_CONNECTION_STRING);
            var schema = Environment.GetEnvironmentVariable(SQLSTREAMSTORE_SCHEMA);

            return factory(connectionString, schema, cancellationToken);
        }

        private static Task<IStreamStore> CreateInMemoryStreamStore(
            string connectionString,
            string schema,
            CancellationToken cancellationToken)
            => Task.FromResult<IStreamStore>(new InMemoryStreamStore());

        private static async Task<IStreamStore> CreateMssqlStreamStore(
            string connectionString,
            string schema,
            CancellationToken cancellationToken)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            using(var connection = new SqlConnection(new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString))
            {
                await connection.OpenAsync(cancellationToken).NotOnCapturedContext();

                using(var command = new SqlCommand(
                    $@"
IF  NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{connectionStringBuilder.InitialCatalog}')
BEGIN
    CREATE DATABASE [{connectionStringBuilder.InitialCatalog}]
END;
",
                    connection))
                {
                    await command.ExecuteNonQueryAsync(cancellationToken).NotOnCapturedContext();
                }
            }

            var settings = new MsSqlStreamStoreV3Settings(connectionString);

            if(schema != null)
            {
                settings.Schema = schema;
            }

            var streamStore = new MsSqlStreamStoreV3(settings);

            await streamStore.CreateSchema(cancellationToken);

            return streamStore;
        }

        private static async Task<IStreamStore> CreatePostgresStreamStore(
            string connectionString,
            string schema,
            CancellationToken cancellationToken)
        {
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);

            using(var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = null
            }.ConnectionString))
            {
                bool exists;
                await connection.OpenAsync(cancellationToken).NotOnCapturedContext();

                using(var command = new NpgsqlCommand(
                    $"SELECT 1 FROM pg_database WHERE datname = '{connectionStringBuilder.Database}'",
                    connection))
                {
                    exists = await command.ExecuteScalarAsync(cancellationToken).NotOnCapturedContext()
                             != null;
                }

                if(!exists)
                {
                    using(var command = new NpgsqlCommand(
                        $"CREATE DATABASE {connectionStringBuilder.Database}",
                        connection))
                    {
                        await command.ExecuteNonQueryAsync(cancellationToken).NotOnCapturedContext();
                    }
                }

                var settings = new PostgresStreamStoreSettings(connectionString);

                if(schema != null)
                {
                    settings.Schema = schema;
                }

                var streamStore = new PostgresStreamStore(settings);

                await streamStore.CreateSchema(cancellationToken);

                return streamStore;
            }
        }
    }
}