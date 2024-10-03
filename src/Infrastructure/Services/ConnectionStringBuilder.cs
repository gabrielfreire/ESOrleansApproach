using Microsoft.Extensions.Configuration;
using ESOrleansApproach.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Infrastructure.Services
{
    public class ConnectionStringBuilder : IConnectionStringBuilder
    {
        private IConfiguration _configuration;

        public ConnectionStringBuilder(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GetConnectionString()
        {
            var host = _configuration["Database:Host"];
            var port = _configuration["Database:Port"];
            var dbName = _configuration["Database:DbName"];
            var user = _configuration["Database:User"];
            var password = _configuration["Database:Password"];

            ThrowIfInvalid(host, port, user, password, dbName);

            return $"Server={host};Port={port};Database={dbName};User ID={user};Password='{password}';CommandTimeout=120;MaxPoolSize=600;Pooling=True";
        }

        public string GetConnectionString(string user, string password)
        {
            var host = _configuration["DatabaseTest:Host"];
            var port = _configuration["DatabaseTest:Port"];
            var dbName = _configuration["DatabaseTest:DbName"];
            ThrowIfInvalid(host, port, user, password, dbName);

            return $"Server={host};Port={port};Database={dbName};User ID={user};Password='{password}';CommandTimeout=120;MaxPoolSize=600;Pooling=True";
        }

        public string GetConnectionString(string dbName)
        {
            var host = _configuration["DatabaseTest:Host"];
            var port = _configuration["DatabaseTest:Port"];
            var user = _configuration["DatabaseTest:User"];
            var password = _configuration["DatabaseTest:Password"];

            ThrowIfInvalid(host, port, user, password, dbName);

            return $"Server={host};Port={port};Database={dbName};User ID={user};Password='{password}';CommandTimeout=120;MaxPoolSize=600;Pooling=True";
        }

        public string GetTestConnectionString()
        {
            var host = _configuration["DatabaseTest:Host"];
            var port = _configuration["DatabaseTest:Port"];
            var dbName = _configuration["DatabaseTest:DbName"];
            var user = _configuration["DatabaseTest:User"];
            var password = _configuration["DatabaseTest:Password"];

            ThrowIfInvalid(host, port, user, password, dbName);

            return $"Server={host};Port={port};Database={dbName};User ID={user};Password='{password}';CommandTimeout=120;MaxPoolSize=600;Pooling=True";
        }

        public string GetUser(bool testingEnvironment)
        {
            return testingEnvironment ? _configuration["DatabaseTest:User"] : _configuration["Database:User"];
        }

        private void ThrowIfInvalid(string? host, string? port, string? user, string? password, string? dbName)
        {
            if (string.IsNullOrEmpty(host) ||
                string.IsNullOrEmpty(port) ||
                string.IsNullOrEmpty(user) ||
                string.IsNullOrEmpty(dbName) ||
                string.IsNullOrEmpty(password)
                 )
            {
                throw new Exception("Connection string is invalid");
            }

            if (!int.TryParse(port, out var value))
            {
                throw new Exception($"Port {port} is not a valid port");
            }
        }
    }
}
