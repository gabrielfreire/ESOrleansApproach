using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESOrleansApproach.Application.Common.Interfaces
{
    public interface IConnectionStringBuilder
    {
        string GetConnectionString();
        string GetConnectionString(string? user, string? password);
        string GetConnectionString(string? dbName);
        string GetTestConnectionString();
        string GetUser(bool testingEnvironment);

    }
}
