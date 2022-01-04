using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCX.Configuration;

namespace OMSamples.Samples
{
    [SampleCode("create_shared_parking")]
    [SampleParam("arg1", "name of shared parking place")]
    [SampleDescription("This sample adds Shared parking place. The name MUST start with 'SP'.")]
    class CreateSharedParkingSample : ISample
    {
        public void Run(PhoneSystem ps, params string[] args)
        {
            ps.GetTenant().CreateParkExtension(args[1]).Save();
        }
    }
}