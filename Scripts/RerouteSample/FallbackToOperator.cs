using System;
using CallFlow;
using TCX.Configuration;
using System.Threading.Tasks;

namespace dummy
{
    public class FallbackToOperator : ScriptBase<FallbackToOperator>
    {
        public override void Start()
        {
            Task.Run(async()=>
            {
                try
                {
            	    MyCall.SetDefaultRoute(((PhoneSystem)MyCall.PS).GetParameterValue("OPERATOR"), $"Fallback Route:{MyCall.Caller.CallerName}");
                    await Task.Delay(1000).ContinueWith(x=>MyCall.Return(true)); 
                }
                catch
                {
                    MyCall.Return(false);
                }
            });
        }
    }
}