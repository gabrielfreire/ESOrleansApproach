using Dapper;
using Serilog;
using System.Data.Common;

namespace ESOrleansApproach.API.Common
{
    public static class OrleansClusteringInitializer
    {
        private static string _logPrefix = "POSTGRESQL_ORLEANS_CLUSTERING_STORAGE - ";
        private static string tableExistsSql = "SELECT EXISTS ( SELECT FROM pg_tables WHERE schemaname = @SchemaName AND tablename  = @TableName );";
        private static string databaseExistsSql = "SELECT EXISTS ( SELECT * from pg_database where datname=@DatabaseName);";

        public async static Task EnsureOrleansStorageCreated(string dbName, string connectionString, string clusteringConnectionString, string dbUser)
        {
            Log.Information($"{_logPrefix}@ EnsureCreated()\nConnectionString: {clusteringConnectionString}");

            if (await DatabaseExists(dbName, connectionString))
            {
                Log.Information($"{_logPrefix}Database {dbName} already exists!");
            }
            else
            {
                await CreateDatabase(dbName, connectionString, dbUser);
            }

            Log.Information($"{_logPrefix}Updated connection string!");

            var _OrleansQueryExists = await TableExists("public", "OrleansQuery", clusteringConnectionString);
            var _OrleansMembershipVersionTableExists = await TableExists("public", "OrleansMembershipVersionTable", clusteringConnectionString);

            if (!_OrleansQueryExists)
                CreateMainTable(clusteringConnectionString);
            if (!_OrleansMembershipVersionTableExists)
                //CreateMembershipAndMembershipVersionTables(clusteringConnectionString);

                DoRequiredOperations(!(_OrleansQueryExists && _OrleansMembershipVersionTableExists), clusteringConnectionString);

        }

        private static DbConnection CreateConnection(string conn)
        {
            var connection = Npgsql.NpgsqlFactory.Instance.CreateConnection();
            connection.ConnectionString = conn;
            return connection;
        }

        public async static Task<bool> TableExists(string dbName, string tableName, string connectionString)
        {
            using var conn = CreateConnection(connectionString);
            var _res = await conn.QueryAsync<object>(tableExistsSql, new { SchemaName = dbName, TableName = tableName.ToLower() });
            var _row = (IDictionary<string, object>)_res.First();
            return (bool)_row["exists"];

        }
        public async static Task<bool> DatabaseExists(string dbName, string connectionString)
        {
            using var conn = CreateConnection(connectionString);
            var _res = await conn.QueryAsync<object>(databaseExistsSql, new { DatabaseName = dbName });
            var _row = (IDictionary<string, object>)_res.First();
            return (bool)_row["exists"];

        }

        public async static Task<string> CreateDatabase(string databaseName, string connectionString, string dbUser)
        {
            var _sql = $@"CREATE DATABASE ""{databaseName}""
            WITH
            OWNER = {dbUser}
            ENCODING = UTF8
            LC_COLLATE = 'en_US.utf-8'
            LC_CTYPE = 'en_US.utf-8'
            TABLESPACE = pg_default
            CONNECTION LIMIT = -1;";

            Log.Information($"{_logPrefix} Database {databaseName} does not exist, creating it");

            using var connection = CreateConnection(connectionString);
            await connection.ExecuteAsync(_sql);

            Log.Information($"{_logPrefix} Database {databaseName} created and connection string updated");

            return connectionString;
        }

        public async static Task<string> DeleteDatabase(string databaseName, string connectionString)
        {
            try
            {
                // we need to terminate all sessions connected to this database in order to DROP it
                // otherwise it will throw an exception with the message: 'database "databaseName" is being accessed by other users'
                using var connection = CreateConnection(connectionString);

                // we first need to revoke connection
                var _revokeConn = $@"REVOKE CONNECT ON DATABASE ""{databaseName}"" FROM public;";
                await connection.ExecuteAsync(_revokeConn);

                // terminate backend process using database PID
                var _terminateActivity = $@"SELECT pg_terminate_backend(pg_stat_activity.pid)
FROM pg_stat_activity
WHERE pg_stat_activity.datname = '{databaseName}';";
                await connection.ExecuteAsync(_terminateActivity);

                // now we can drop
                var _sql = $@"DROP DATABASE ""{databaseName}""";

                await connection.ExecuteAsync(_sql);

                if (await DatabaseExists(databaseName, connectionString))
                {
                    throw new Exception($"{_logPrefix} Database {databaseName} was not deleted");
                }

                Log.Information($"{_logPrefix} Database {databaseName} deleted");

                return connectionString;
            }
            catch
            {
                throw;
            }
        }

        private static void CreateMainTable(string connectionString)
        {
            Log.Information($"{_logPrefix} Creating main table OrleansQuery");
            var _sql = @"CREATE TABLE OrleansQuery
(
    QueryKey varchar(64) NOT NULL,
    QueryText varchar(8000) NOT NULL,

    CONSTRAINT OrleansQuery_Key PRIMARY KEY(QueryKey)
);";
            using var connection = CreateConnection(connectionString);
            connection.Execute(_sql);
        }

        /// <summary>
        /// Every silo instance has a row in the membership table.
        /// </summary>
        private static void CreateMembershipAndMembershipVersionTables(string connectionString)
        {
            Log.Information($"{_logPrefix}Creating table OrleansMembershipVersionTable");
            var _sql = @"CREATE TABLE if not exists OrleansMembershipVersionTable
            (
                DeploymentId varchar(150) NOT NULL,
                Timestamp timestamp(3) NOT NULL DEFAULT (now() at time zone 'utc'),
                Version integer NOT NULL DEFAULT 0,

                CONSTRAINT PK_OrleansMembershipVersionTable_DeploymentId PRIMARY KEY(DeploymentId)
            );

            CREATE TABLE if not exists OrleansMembershipTable
            (
                DeploymentId varchar(150) NOT NULL,
                Address varchar(45) NOT NULL,
                Port integer NOT NULL,
                Generation integer NOT NULL,
                SiloName varchar(150) NOT NULL,
                HostName varchar(150) NOT NULL,
                Status integer NOT NULL,
                ProxyPort integer NULL,
                SuspectTimes varchar(8000) NULL,
                StartTime timestamp(3) NOT NULL,
                IAmAliveTime timestamp(3) NOT NULL,

                CONSTRAINT PK_MembershipTable_DeploymentId PRIMARY KEY(DeploymentId, Address, Port, Generation),
                CONSTRAINT FK_MembershipTable_MembershipVersionTable_DeploymentId FOREIGN KEY (DeploymentId) REFERENCES OrleansMembershipVersionTable (DeploymentId)
            );";
            using var connection = CreateConnection(connectionString);
            connection.Execute(_sql);
        }

        private static void DoRequiredOperations(bool actuallyDoIt, string connectionString)
        {
            if (actuallyDoIt)
            {

                Log.Information($"{_logPrefix}Performing operations");
                var _sql = @"
-- For each deployment, there will be only one (active) membership version table version column which will be updated periodically.
CREATE TABLE OrleansMembershipVersionTable
(
    DeploymentId varchar(150) NOT NULL,
    Timestamp timestamptz(3) NOT NULL DEFAULT now(),
    Version integer NOT NULL DEFAULT 0,

    CONSTRAINT PK_OrleansMembershipVersionTable_DeploymentId PRIMARY KEY(DeploymentId)
);

-- Every silo instance has a row in the membership table.
CREATE TABLE OrleansMembershipTable
(
    DeploymentId varchar(150) NOT NULL,
    Address varchar(45) NOT NULL,
    Port integer NOT NULL,
    Generation integer NOT NULL,
    SiloName varchar(150) NOT NULL,
    HostName varchar(150) NOT NULL,
    Status integer NOT NULL,
    ProxyPort integer NULL,
    SuspectTimes varchar(8000) NULL,
    StartTime timestamptz(3) NOT NULL,
    IAmAliveTime timestamptz(3) NOT NULL,

    CONSTRAINT PK_MembershipTable_DeploymentId PRIMARY KEY(DeploymentId, Address, Port, Generation),
    CONSTRAINT FK_MembershipTable_MembershipVersionTable_DeploymentId FOREIGN KEY (DeploymentId) REFERENCES OrleansMembershipVersionTable (DeploymentId)
);

CREATE FUNCTION update_i_am_alive_time(
    deployment_id OrleansMembershipTable.DeploymentId%TYPE,
    address_arg OrleansMembershipTable.Address%TYPE,
    port_arg OrleansMembershipTable.Port%TYPE,
    generation_arg OrleansMembershipTable.Generation%TYPE,
    i_am_alive_time OrleansMembershipTable.IAmAliveTime%TYPE)
  RETURNS void AS
$func$
BEGIN
    -- This is expected to never fail by Orleans, so return value
    -- is not needed nor is it checked.
    UPDATE OrleansMembershipTable as d
    SET
        IAmAliveTime = i_am_alive_time
    WHERE
        d.DeploymentId = deployment_id AND deployment_id IS NOT NULL
        AND d.Address = address_arg AND address_arg IS NOT NULL
        AND d.Port = port_arg AND port_arg IS NOT NULL
        AND d.Generation = generation_arg AND generation_arg IS NOT NULL;
END
$func$ LANGUAGE plpgsql;

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'UpdateIAmAlivetimeKey','
    -- This is expected to never fail by Orleans, so return value
    -- is not needed nor is it checked.
    SELECT * from update_i_am_alive_time(
        @DeploymentId,
        @Address,
        @Port,
        @Generation,
        @IAmAliveTime
    );
');

CREATE FUNCTION insert_membership_version(
    DeploymentIdArg OrleansMembershipTable.DeploymentId%TYPE
)
  RETURNS TABLE(row_count integer) AS
$func$
DECLARE
    RowCountVar int := 0;
BEGIN

    BEGIN

        INSERT INTO OrleansMembershipVersionTable
        (
            DeploymentId
        )
        SELECT DeploymentIdArg
        ON CONFLICT (DeploymentId) DO NOTHING;

        GET DIAGNOSTICS RowCountVar = ROW_COUNT;

        ASSERT RowCountVar <> 0, 'no rows affected, rollback';

        RETURN QUERY SELECT RowCountVar;
    EXCEPTION
    WHEN assert_failure THEN
        RETURN QUERY SELECT RowCountVar;
    END;

END
$func$ LANGUAGE plpgsql;

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'InsertMembershipVersionKey','
    SELECT * FROM insert_membership_version(
        @DeploymentId
    );
');
INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'CleanupDefunctSiloEntriesKey','
    DELETE FROM OrleansMembershipTable
    WHERE DeploymentId = @DeploymentId
        AND @DeploymentId IS NOT NULL
        AND IAmAliveTime < @IAmAliveTime
        AND Status != 3;
');
CREATE FUNCTION insert_membership(
    DeploymentIdArg OrleansMembershipTable.DeploymentId%TYPE,
    AddressArg      OrleansMembershipTable.Address%TYPE,
    PortArg         OrleansMembershipTable.Port%TYPE,
    GenerationArg   OrleansMembershipTable.Generation%TYPE,
    SiloNameArg     OrleansMembershipTable.SiloName%TYPE,
    HostNameArg     OrleansMembershipTable.HostName%TYPE,
    StatusArg       OrleansMembershipTable.Status%TYPE,
    ProxyPortArg    OrleansMembershipTable.ProxyPort%TYPE,
    StartTimeArg    OrleansMembershipTable.StartTime%TYPE,
    IAmAliveTimeArg OrleansMembershipTable.IAmAliveTime%TYPE,
    VersionArg      OrleansMembershipVersionTable.Version%TYPE)
  RETURNS TABLE(row_count integer) AS
$func$
DECLARE
    RowCountVar int := 0;
BEGIN

    BEGIN
        INSERT INTO OrleansMembershipTable
        (
            DeploymentId,
            Address,
            Port,
            Generation,
            SiloName,
            HostName,
            Status,
            ProxyPort,
            StartTime,
            IAmAliveTime
        )
        SELECT
            DeploymentIdArg,
            AddressArg,
            PortArg,
            GenerationArg,
            SiloNameArg,
            HostNameArg,
            StatusArg,
            ProxyPortArg,
            StartTimeArg,
            IAmAliveTimeArg
        ON CONFLICT (DeploymentId, Address, Port, Generation) DO
            NOTHING;


        GET DIAGNOSTICS RowCountVar = ROW_COUNT;

        UPDATE OrleansMembershipVersionTable
        SET
            Timestamp = now(),
            Version = Version + 1
        WHERE
            DeploymentId = DeploymentIdArg AND DeploymentIdArg IS NOT NULL
            AND Version = VersionArg AND VersionArg IS NOT NULL
            AND RowCountVar > 0;

        GET DIAGNOSTICS RowCountVar = ROW_COUNT;

        ASSERT RowCountVar <> 0, 'no rows affected, rollback';


        RETURN QUERY SELECT RowCountVar;
    EXCEPTION
    WHEN assert_failure THEN
        RETURN QUERY SELECT RowCountVar;
    END;

END
$func$ LANGUAGE plpgsql;

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'InsertMembershipKey','
    SELECT * FROM insert_membership(
        @DeploymentId,
        @Address,
        @Port,
        @Generation,
        @SiloName,
        @HostName,
        @Status,
        @ProxyPort,
        @StartTime,
        @IAmAliveTime,
        @Version
    );
');

CREATE FUNCTION update_membership(
    DeploymentIdArg OrleansMembershipTable.DeploymentId%TYPE,
    AddressArg      OrleansMembershipTable.Address%TYPE,
    PortArg         OrleansMembershipTable.Port%TYPE,
    GenerationArg   OrleansMembershipTable.Generation%TYPE,
    StatusArg       OrleansMembershipTable.Status%TYPE,
    SuspectTimesArg OrleansMembershipTable.SuspectTimes%TYPE,
    IAmAliveTimeArg OrleansMembershipTable.IAmAliveTime%TYPE,
    VersionArg      OrleansMembershipVersionTable.Version%TYPE
  )
  RETURNS TABLE(row_count integer) AS
$func$
DECLARE
    RowCountVar int := 0;
BEGIN

    BEGIN

    UPDATE OrleansMembershipVersionTable
    SET
        Timestamp = now(),
        Version = Version + 1
    WHERE
        DeploymentId = DeploymentIdArg AND DeploymentIdArg IS NOT NULL
        AND Version = VersionArg AND VersionArg IS NOT NULL;


    GET DIAGNOSTICS RowCountVar = ROW_COUNT;

    UPDATE OrleansMembershipTable
    SET
        Status = StatusArg,
        SuspectTimes = SuspectTimesArg,
        IAmAliveTime = IAmAliveTimeArg
    WHERE
        DeploymentId = DeploymentIdArg AND DeploymentIdArg IS NOT NULL
        AND Address = AddressArg AND AddressArg IS NOT NULL
        AND Port = PortArg AND PortArg IS NOT NULL
        AND Generation = GenerationArg AND GenerationArg IS NOT NULL
        AND RowCountVar > 0;


        GET DIAGNOSTICS RowCountVar = ROW_COUNT;

        ASSERT RowCountVar <> 0, 'no rows affected, rollback';


        RETURN QUERY SELECT RowCountVar;
    EXCEPTION
    WHEN assert_failure THEN
        RETURN QUERY SELECT RowCountVar;
    END;

END
$func$ LANGUAGE plpgsql;

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'UpdateMembershipKey','
    SELECT * FROM update_membership(
        @DeploymentId,
        @Address,
        @Port,
        @Generation,
        @Status,
        @SuspectTimes,
        @IAmAliveTime,
        @Version
    );
');

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'MembershipReadRowKey','
    SELECT
        v.DeploymentId,
        m.Address,
        m.Port,
        m.Generation,
        m.SiloName,
        m.HostName,
        m.Status,
        m.ProxyPort,
        m.SuspectTimes,
        m.StartTime,
        m.IAmAliveTime,
        v.Version
    FROM
        OrleansMembershipVersionTable v
        -- This ensures the version table will returned even if there is no matching membership row.
        LEFT OUTER JOIN OrleansMembershipTable m ON v.DeploymentId = m.DeploymentId
        AND Address = @Address AND @Address IS NOT NULL
        AND Port = @Port AND @Port IS NOT NULL
        AND Generation = @Generation AND @Generation IS NOT NULL
    WHERE
        v.DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;
');

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'MembershipReadAllKey','
    SELECT
        v.DeploymentId,
        m.Address,
        m.Port,
        m.Generation,
        m.SiloName,
        m.HostName,
        m.Status,
        m.ProxyPort,
        m.SuspectTimes,
        m.StartTime,
        m.IAmAliveTime,
        v.Version
    FROM
        OrleansMembershipVersionTable v LEFT OUTER JOIN OrleansMembershipTable m
        ON v.DeploymentId = m.DeploymentId
    WHERE
        v.DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;
');

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'DeleteMembershipTableEntriesKey','
    DELETE FROM OrleansMembershipTable
    WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;
    DELETE FROM OrleansMembershipVersionTable
    WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;
');

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'GatewaysQueryKey','
    SELECT
        Address,
        ProxyPort,
        Generation
    FROM
        OrleansMembershipTable
    WHERE
        DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL
        AND Status = @Status AND @Status IS NOT NULL
        AND ProxyPort > 0;
');";
                using var connection = CreateConnection(connectionString);
                connection.Execute(_sql);
            }

            //            var _migration = @"-- Run this migration for upgrading the PostgreSQL clustering table and routines for deployments created before 3.6.0

            //BEGIN;

            //-- Change date type

            //ALTER TABLE OrleansMembershipVersionTable
            //ALTER COLUMN Timestamp TYPE TIMESTAMPTZ(3) USING Timestamp AT TIME ZONE 'UTC';

            //ALTER TABLE OrleansMembershipTable
            //ALTER COLUMN StartTime TYPE TIMESTAMPTZ(3) USING StartTime AT TIME ZONE 'UTC',
            //ALTER COLUMN IAmAliveTime TYPE TIMESTAMPTZ(3) USING IAmAliveTime AT TIME ZONE 'UTC';

            //-- Recreate routines

            //CREATE OR REPLACE FUNCTION update_i_am_alive_time(
            //    deployment_id OrleansMembershipTable.DeploymentId%TYPE,
            //    address_arg OrleansMembershipTable.Address%TYPE,
            //    port_arg OrleansMembershipTable.Port%TYPE,
            //    generation_arg OrleansMembershipTable.Generation%TYPE,
            //    i_am_alive_time OrleansMembershipTable.IAmAliveTime%TYPE)
            //  RETURNS void AS
            //$func$
            //BEGIN
            //    -- This is expected to never fail by Orleans, so return value
            //    -- is not needed nor is it checked.
            //    UPDATE OrleansMembershipTable as d
            //    SET
            //        IAmAliveTime = i_am_alive_time
            //    WHERE
            //        d.DeploymentId = deployment_id AND deployment_id IS NOT NULL
            //        AND d.Address = address_arg AND address_arg IS NOT NULL
            //        AND d.Port = port_arg AND port_arg IS NOT NULL
            //        AND d.Generation = generation_arg AND generation_arg IS NOT NULL;
            //END
            //$func$ LANGUAGE plpgsql;


            //CREATE OR REPLACE FUNCTION insert_membership(
            //    DeploymentIdArg OrleansMembershipTable.DeploymentId%TYPE,
            //    AddressArg      OrleansMembershipTable.Address%TYPE,
            //    PortArg         OrleansMembershipTable.Port%TYPE,
            //    GenerationArg   OrleansMembershipTable.Generation%TYPE,
            //    SiloNameArg     OrleansMembershipTable.SiloName%TYPE,
            //    HostNameArg     OrleansMembershipTable.HostName%TYPE,
            //    StatusArg       OrleansMembershipTable.Status%TYPE,
            //    ProxyPortArg    OrleansMembershipTable.ProxyPort%TYPE,
            //    StartTimeArg    OrleansMembershipTable.StartTime%TYPE,
            //    IAmAliveTimeArg OrleansMembershipTable.IAmAliveTime%TYPE,
            //    VersionArg      OrleansMembershipVersionTable.Version%TYPE)
            //  RETURNS TABLE(row_count integer) AS
            //$func$
            //DECLARE
            //    RowCountVar int := 0;
            //BEGIN

            //    BEGIN
            //        INSERT INTO OrleansMembershipTable
            //        (
            //            DeploymentId,
            //            Address,
            //            Port,
            //            Generation,
            //            SiloName,
            //            HostName,
            //            Status,
            //            ProxyPort,
            //            StartTime,
            //            IAmAliveTime
            //        )
            //        SELECT
            //            DeploymentIdArg,
            //            AddressArg,
            //            PortArg,
            //            GenerationArg,
            //            SiloNameArg,
            //            HostNameArg,
            //            StatusArg,
            //            ProxyPortArg,
            //            StartTimeArg,
            //            IAmAliveTimeArg
            //        ON CONFLICT (DeploymentId, Address, Port, Generation) DO
            //            NOTHING;


            //        GET DIAGNOSTICS RowCountVar = ROW_COUNT;

            //        UPDATE OrleansMembershipVersionTable
            //        SET
            //            Timestamp = now(),
            //            Version = Version + 1
            //        WHERE
            //            DeploymentId = DeploymentIdArg AND DeploymentIdArg IS NOT NULL
            //            AND Version = VersionArg AND VersionArg IS NOT NULL
            //            AND RowCountVar > 0;

            //        GET DIAGNOSTICS RowCountVar = ROW_COUNT;

            //        ASSERT RowCountVar <> 0, 'no rows affected, rollback';


            //        RETURN QUERY SELECT RowCountVar;
            //    EXCEPTION
            //    WHEN assert_failure THEN
            //        RETURN QUERY SELECT RowCountVar;
            //    END;

            //END
            //$func$ LANGUAGE plpgsql;


            //CREATE OR REPLACE FUNCTION update_membership(
            //    DeploymentIdArg OrleansMembershipTable.DeploymentId%TYPE,
            //    AddressArg      OrleansMembershipTable.Address%TYPE,
            //    PortArg         OrleansMembershipTable.Port%TYPE,
            //    GenerationArg   OrleansMembershipTable.Generation%TYPE,
            //    StatusArg       OrleansMembershipTable.Status%TYPE,
            //    SuspectTimesArg OrleansMembershipTable.SuspectTimes%TYPE,
            //    IAmAliveTimeArg OrleansMembershipTable.IAmAliveTime%TYPE,
            //    VersionArg      OrleansMembershipVersionTable.Version%TYPE
            //  )
            //  RETURNS TABLE(row_count integer) AS
            //$func$
            //DECLARE
            //    RowCountVar int := 0;
            //BEGIN

            //    BEGIN

            //    UPDATE OrleansMembershipVersionTable
            //    SET
            //        Timestamp = now(),
            //        Version = Version + 1
            //    WHERE
            //        DeploymentId = DeploymentIdArg AND DeploymentIdArg IS NOT NULL
            //        AND Version = VersionArg AND VersionArg IS NOT NULL;


            //    GET DIAGNOSTICS RowCountVar = ROW_COUNT;

            //    UPDATE OrleansMembershipTable
            //    SET
            //        Status = StatusArg,
            //        SuspectTimes = SuspectTimesArg,
            //        IAmAliveTime = IAmAliveTimeArg
            //    WHERE
            //        DeploymentId = DeploymentIdArg AND DeploymentIdArg IS NOT NULL
            //        AND Address = AddressArg AND AddressArg IS NOT NULL
            //        AND Port = PortArg AND PortArg IS NOT NULL
            //        AND Generation = GenerationArg AND GenerationArg IS NOT NULL
            //        AND RowCountVar > 0;


            //        GET DIAGNOSTICS RowCountVar = ROW_COUNT;

            //        ASSERT RowCountVar <> 0, 'no rows affected, rollback';


            //        RETURN QUERY SELECT RowCountVar;
            //    EXCEPTION
            //    WHEN assert_failure THEN
            //        RETURN QUERY SELECT RowCountVar;
            //    END;

            //END
            //$func$ LANGUAGE plpgsql;

            //COMMIT;";
            //            using var _connection = CreateConnection(connectionString);
            //            _connection.Execute(_migration);
        }
    }
}

