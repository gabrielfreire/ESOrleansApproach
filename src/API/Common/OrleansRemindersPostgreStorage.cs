using Dapper;
using Serilog;
using System.Data.Common;

namespace ESOrleansApproach.API.Common
{
    public static class OrleansRemindersPostgreStorage
    {
        private static string _logPrefix = "POSTGRESQL_ORLEANS_REMINDERS_STORAGE - ";
        private static string tableExistsSql = "SELECT EXISTS ( SELECT FROM pg_tables WHERE schemaname = @SchemaName AND tablename  = @TableName );";
        private static string databaseExistsSql = "SELECT EXISTS ( SELECT * from pg_database where datname=@DatabaseName);";

        public static async Task EnsureRemindersTablesExist(string connectionString)
        {
            Log.Information($"{_logPrefix}@ EnsureRemindersTablesExist()\nConnectionString: {connectionString}");

            var _OrleansRemindersTableExists = await TableExists("public", "OrleansRemindersTable", connectionString);

            if (_OrleansRemindersTableExists)
            {
                Log.Information($"{_logPrefix}@ EnsureRemindersTablesExist()\nTable already exists");
            }
            DoRequireOperations(!_OrleansRemindersTableExists, connectionString);
        }

        public static async Task<bool> TableExists(string dbName, string tableName, string connectionString)
        {
            using var conn = CreateConnection(connectionString);
            var _res = await conn.QueryAsync<object>(tableExistsSql, new { SchemaName = dbName, TableName = tableName.ToLower() });
            var _row = (IDictionary<string, object>)_res.First();
            return (bool)_row["exists"];
        }
        private static DbConnection CreateConnection(string connectionString)
        {
            var connection = Npgsql.NpgsqlFactory.Instance.CreateConnection();
            connection.ConnectionString = connectionString;
            return connection;
        }

        private static void DoRequireOperations(bool actuallyDoIt, string connectionString)
        {
            if (actuallyDoIt)
            {

                Log.Information($"{_logPrefix}Performing operations");
                var _sql = @"
-- Orleans Reminders table - https://learn.microsoft.com/dotnet/orleans/grains/timers-and-reminders
CREATE TABLE OrleansRemindersTable
(
    ServiceId varchar(150) NOT NULL,
    GrainId varchar(150) NOT NULL,
    ReminderName varchar(150) NOT NULL,
    StartTime timestamptz(3) NOT NULL,
    Period bigint NOT NULL,
    GrainHash integer NOT NULL,
    Version integer NOT NULL,

    CONSTRAINT PK_RemindersTable_ServiceId_GrainId_ReminderName PRIMARY KEY(ServiceId, GrainId, ReminderName)
);

CREATE FUNCTION upsert_reminder_row(
    ServiceIdArg    OrleansRemindersTable.ServiceId%TYPE,
    GrainIdArg      OrleansRemindersTable.GrainId%TYPE,
    ReminderNameArg OrleansRemindersTable.ReminderName%TYPE,
    StartTimeArg    OrleansRemindersTable.StartTime%TYPE,
    PeriodArg       OrleansRemindersTable.Period%TYPE,
    GrainHashArg    OrleansRemindersTable.GrainHash%TYPE
  )
  RETURNS TABLE(version integer) AS
$func$
DECLARE
    VersionVar int := 0;
BEGIN

    INSERT INTO OrleansRemindersTable
    (
        ServiceId,
        GrainId,
        ReminderName,
        StartTime,
        Period,
        GrainHash,
        Version
    )
    SELECT
        ServiceIdArg,
        GrainIdArg,
        ReminderNameArg,
        StartTimeArg,
        PeriodArg,
        GrainHashArg,
        0
    ON CONFLICT (ServiceId, GrainId, ReminderName)
        DO UPDATE SET
            StartTime = excluded.StartTime,
            Period = excluded.Period,
            GrainHash = excluded.GrainHash,
            Version = OrleansRemindersTable.Version + 1
    RETURNING
        OrleansRemindersTable.Version INTO STRICT VersionVar;

    RETURN QUERY SELECT VersionVar AS versionr;

END
$func$ LANGUAGE plpgsql;

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'UpsertReminderRowKey','
    SELECT * FROM upsert_reminder_row(
        @ServiceId,
        @GrainId,
        @ReminderName,
        @StartTime,
        @Period,
        @GrainHash
    );
');

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'ReadReminderRowsKey','
    SELECT
        GrainId,
        ReminderName,
        StartTime,
        Period,
        Version
    FROM OrleansRemindersTable
    WHERE
        ServiceId = @ServiceId AND @ServiceId IS NOT NULL
        AND GrainId = @GrainId AND @GrainId IS NOT NULL;
');

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'ReadReminderRowKey','
    SELECT
        GrainId,
        ReminderName,
        StartTime,
        Period,
        Version
    FROM OrleansRemindersTable
    WHERE
        ServiceId = @ServiceId AND @ServiceId IS NOT NULL
        AND GrainId = @GrainId AND @GrainId IS NOT NULL
        AND ReminderName = @ReminderName AND @ReminderName IS NOT NULL;
');

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'ReadRangeRows1Key','
    SELECT
        GrainId,
        ReminderName,
        StartTime,
        Period,
        Version
    FROM OrleansRemindersTable
    WHERE
        ServiceId = @ServiceId AND @ServiceId IS NOT NULL
        AND GrainHash > @BeginHash AND @BeginHash IS NOT NULL
        AND GrainHash <= @EndHash AND @EndHash IS NOT NULL;
');

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'ReadRangeRows2Key','
    SELECT
        GrainId,
        ReminderName,
        StartTime,
        Period,
        Version
    FROM OrleansRemindersTable
    WHERE
        ServiceId = @ServiceId AND @ServiceId IS NOT NULL
        AND ((GrainHash > @BeginHash AND @BeginHash IS NOT NULL)
        OR (GrainHash <= @EndHash AND @EndHash IS NOT NULL));
');

CREATE FUNCTION delete_reminder_row(
    ServiceIdArg    OrleansRemindersTable.ServiceId%TYPE,
    GrainIdArg      OrleansRemindersTable.GrainId%TYPE,
    ReminderNameArg OrleansRemindersTable.ReminderName%TYPE,
    VersionArg      OrleansRemindersTable.Version%TYPE
)
  RETURNS TABLE(row_count integer) AS
$func$
DECLARE
    RowCountVar int := 0;
BEGIN


    DELETE FROM OrleansRemindersTable
    WHERE
        ServiceId = ServiceIdArg AND ServiceIdArg IS NOT NULL
        AND GrainId = GrainIdArg AND GrainIdArg IS NOT NULL
        AND ReminderName = ReminderNameArg AND ReminderNameArg IS NOT NULL
        AND Version = VersionArg AND VersionArg IS NOT NULL;

    GET DIAGNOSTICS RowCountVar = ROW_COUNT;

    RETURN QUERY SELECT RowCountVar;

END
$func$ LANGUAGE plpgsql;

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'DeleteReminderRowKey','
    SELECT * FROM delete_reminder_row(
        @ServiceId,
        @GrainId,
        @ReminderName,
        @Version
    );
');

INSERT INTO OrleansQuery(QueryKey, QueryText)
VALUES
(
    'DeleteReminderRowsKey','
    DELETE FROM OrleansRemindersTable
    WHERE
        ServiceId = @ServiceId AND @ServiceId IS NOT NULL;
');";

                using var connection = CreateConnection(connectionString);
                connection.Execute(_sql);
            }

            //            var _migration = @"-- Run this migration for upgrading the PostgreSQL reminder table and routines for deployments created before 3.6.0

            //BEGIN;

            //-- Change date type

            //ALTER TABLE OrleansRemindersTable
            //ALTER COLUMN StartTime TYPE TIMESTAMPTZ(3) USING StartTime AT TIME ZONE 'UTC';

            //-- Recreate routines

            //CREATE OR REPLACE FUNCTION upsert_reminder_row(
            //    ServiceIdArg    OrleansRemindersTable.ServiceId%TYPE,
            //    GrainIdArg      OrleansRemindersTable.GrainId%TYPE,
            //    ReminderNameArg OrleansRemindersTable.ReminderName%TYPE,
            //    StartTimeArg    OrleansRemindersTable.StartTime%TYPE,
            //    PeriodArg       OrleansRemindersTable.Period%TYPE,
            //    GrainHashArg    OrleansRemindersTable.GrainHash%TYPE
            //  )
            //  RETURNS TABLE(version integer) AS
            //$func$
            //DECLARE
            //    VersionVar int := 0;
            //BEGIN

            //    INSERT INTO OrleansRemindersTable
            //    (
            //        ServiceId,
            //        GrainId,
            //        ReminderName,
            //        StartTime,
            //        Period,
            //        GrainHash,
            //        Version
            //    )
            //    SELECT
            //        ServiceIdArg,
            //        GrainIdArg,
            //        ReminderNameArg,
            //        StartTimeArg,
            //        PeriodArg,
            //        GrainHashArg,
            //        0
            //    ON CONFLICT (ServiceId, GrainId, ReminderName)
            //        DO UPDATE SET
            //            StartTime = excluded.StartTime,
            //            Period = excluded.Period,
            //            GrainHash = excluded.GrainHash,
            //            Version = OrleansRemindersTable.Version + 1
            //    RETURNING
            //        OrleansRemindersTable.Version INTO STRICT VersionVar;

            //    RETURN QUERY SELECT VersionVar AS versionr;

            //END
            //$func$ LANGUAGE plpgsql;

            //COMMIT;";
            //            using var _connection = CreateConnection(connectionString);
            //            _connection.Execute(_migration);
        }
    }
}
