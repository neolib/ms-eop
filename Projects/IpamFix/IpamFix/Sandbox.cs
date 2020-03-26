using Microsoft.Azure.Ipam.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IpamFix
{
    using static Console;
    using static IpamHelper;

    class Sandbox
    {
        internal void Run(string[] args)
        {
            DoWork().Wait();
        }

        private async Task DoWork()
        {
            var prefix = "100.127.144.0/23";
            var id = "b16c49fb-adf5-4d92-8c5d-b37f11543f9d";

            await UpdateTitle(
                "Default",
                prefix,
                id,
                "EOP: EUR-VI1EUR03 - IPv4_CAP - IPv4_CAP");
        }
    }
}
