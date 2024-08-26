using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VipDbChecker
{
    internal struct CfgParams
    {
        public CfgParamsConnection Connection { get; set; }
        public int ServerId { get; set; }
    }

    internal struct CfgParamsConnection
    {
        public string Host { get; set; }
        public string Database { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public int Port { get; set; }
    }



}
