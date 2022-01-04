using System;
using System.Collections.Generic;
using System.Text;
using TCX.Configuration;

namespace OMSamples.Samples
{
    [SampleCode("deletedn")]
    [SampleWarning("")]
    [SampleDescription("removing specifid DN")]
    class DeleteDN : ISample
    {
        public void Run(PhoneSystem ps, params string[] args)
        {
            ps.GetDNByNumber(args[1]).Delete();
        }
    }
}
