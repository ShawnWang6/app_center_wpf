using CtrlCenter.DataModel;
using CtrlCenter.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CtrlCenter.Interfaces
{
    public interface IDbConnFactory
    {
        IDbConnection CreateConnection();
        IDbConnection CreateConnection(string connectionString);
    }
}
