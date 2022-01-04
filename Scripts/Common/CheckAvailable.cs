using CallFlow;
using TCX.Configuration;

/// <summary>
/// namespace is ignored by scripting host.
/// </summary>
namespace dummy
{
    /// <summary>
    /// This is the synthetic sample script which checks availability of the destination number specified by "public_destination" keys attached to the caller connection
    /// Is script is called using CallFlow.ICall.Call, the key "public_destination" can be overriden
    /// This script returns FALSE (not available) if:
    /// 1. destination is NOT internal
    /// 2. if destination is an Extension then:
    ///        - it should have at least one registered device
    ///        - it should not have any connections (calls)
    ///        - Extension.QueueStatus is set to QueueStatusType.LoggedIn
    /// </summary>
    public class CheckAvailable : ScriptBase<CheckAvailable>
    {
        public override void Start()
        {
            var number = MyCall.Caller["public_destination"];
            using (var dn = MyCall.PS.GetDNByNumber(number))
            {
                var retval = dn != null && !(dn is ExternalLine);
                if (dn is Extension ext)
                {
                    using (var connections = ext.GetActiveConnections().GetDisposer())
                    {
                        retval = (ext.QueueStatus == QueueStatusType.LoggedIn && ext.IsRegistered == true && connections.Length == 0);
                        if (!retval)
                            MyCall.Info($"Extension {number} is Busy/NotAvailable - Registered:{ext?.IsRegistered} - {ext?.QueueStatus} - {connections} active connsection(s)");
                    }
                }
                MyCall.Info($"{number} - available={retval}");
                MyCall.Return(retval);
            }
        }
    }
    
}
