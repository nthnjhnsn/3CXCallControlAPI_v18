using System;
using System.Threading;
using TCX.Configuration;
using CallFlow;

/// <summary>
/// namespace is substituted by scripting for own needs.
/// </summary>
namespace dummy
{
    /// <summary>
    /// This script does following:
    /// 1. acccepts incoming call.
    /// 2. run timer for 30 seconds and try to return call to the destination using RouteTo method. Destination is selected as:
    ///    if Caller connection is delivered to this script with "public_return_to" parameter (either as attached data, or as the data provided with ICall.Call method from main flow.
    ///    else if call was transferred to the RoutePoint by the extension call will be returned to that extension.
    ///    else call will stay on RoutePoint forewer.
    /// </summary>
    public class CallReminder : ScriptBase<CallReminder>
    {
        /// <summary>
        /// timer to repeat route
        /// </summary>
        Timer MyTimer = null;
        string overrideReturnTo = null;

        /// <summary>
        /// makes nameaddr which prepends requested string to the caller name
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns></returns>
        string MakeSourceAddress(string prefix)
        {
            return $"\"{prefix}:{MyCall.Caller.CallerName}\"<sip:_{MyCall.Caller.CallerID}@{MyCall.PS.GetParameterValue("SIPDOMAIN")}>";
        }
        string GetReturnTo()
        {
            try
            {
                var retval = MyCall.Caller["public_return_to"];
                if (string.IsNullOrEmpty(retval))
                {
                    MyCall.Info("no public_return_to specified");
                    if (overrideReturnTo != null)
                    {
                        MyCall.Info("Return destination is overriden to {0}", overrideReturnTo);
                        retval = overrideReturnTo;
                    }
                    else
                    {
                        //onbehlfof is lowlevel parameter which is currently provided during blind transfer procedure
                        //this is the trick, how to catch the dn which has transferred call to this CallFlow script.
                        retval = (MyCall.PS.GetDNByNumber(MyCall["onbehlfof"]) as Extension)?.Number;
                        if (string.IsNullOrEmpty(retval))
                        {
                            MyCall.Info($"Return destination is not defined. Stay on call.");
                        }
                    }
                }
                return retval;
            }
            catch (Exception ex)
            {
                MyCall.Error("Cannot obtain return number");
                MyCall.Exception(ex);
                return null;
            }
        }
        /// <summary>
        /// script entry point.
        /// Here we are subscribe for necessary event and initiate process which will repeatedly deliver call to the returnTo with "CallReminder:" prefix.
        /// </summary>
        public override void Start()
        {
            //when call will leave RoutePoint we just dispose timer and put message into 3CXCallFlow service log.
            MyCall.OnTerminated += () => 
            {
                MyTimer.Dispose();
                MyCall.Info("Call from '{0}'({1}) with requested reminder to '{2}' was disconnected form the {3}", MyCall?.Caller?.CallerName, MyCall?.Caller?.CallerID, GetReturnTo(), MyCall?.DN?.Number);
            };

            //we are reactin on * and each time when caller presses it try to deliver call to operator extension with display name started with "Operator requested by CallReminder:"
            //if operator will not answer script will continue to reach returnTo destination.
            MyCall.OnDTMFInput += (x) =>
            {
                if (x == '*')
                {
                    MyTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    MyCall.Info("Go to operator Extension");
                    try
                    {
                        MyCall.RouteTo((string)MyCall.PS.GetParameterValue("OPERATOR"), MakeSourceAddress("Operator requested by CallReminder:"), 30, (y) => { MyCall.Info("Operator unreachable"); RepeatRoute(); });
                    }
                    catch(Exception ex)
                    {
                        MyCall.Info("Failed to route to Operator");
                        MyCall.Exception(ex);
                    }
                }
            };
            MyTimer = new Timer(x =>
            {
                var returnTo = GetReturnTo();
                MyCall.Info($"Trying to reach {returnTo}");
                if (!string.IsNullOrEmpty(returnTo))
                    MyCall.RouteTo(returnTo, MakeSourceAddress("CallReminder:"), 30, (y) => RepeatRoute());
                else
                    RepeatRoute();
            }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            //Call can be delivered using "attended transfer".
            //Attended transfer is the procedure which joins two calls. So either, script connection will be replicated in other call (current script instance will be terminated)
            //or other party will replace Caller connection.
            //Also it is possible that the waiting party may be replaced with other caonnection (Caller makes blind transfer)
            //second case of Attended transfer and blind transfer made by caller is handled here. we cancel timer and current reroute and then start next round 
            //which tell PLSHOLD to the new party and continue to deliver the call back to the returnTo destination.
            MyCall.OnPartyChanged += () =>
            {
                MyTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                MyCall.CancelReroute();
                RepeatRoute();
            };
            //Call flow route point which executes this script can be provisioned with "overriden destination for call reminder"
            overrideReturnTo = MyCall.DN.GetPropertyValue("CALLREMINDER_DESTINATION_OVERRIDE");
            if (string.IsNullOrEmpty(overrideReturnTo) && string.IsNullOrEmpty(GetReturnTo()))
            {
                if (MyCall.Caller.DN is Extension ext)
                    overrideReturnTo = ext.Number;
            }
            RepeatRoute();
        }
        void RepeatRoute()
        {
            MyCall.PlayPrompt(null, new[] { "PLSHOLD" }, PlayPromptOptions.Blocked|PlayPromptOptions.UnblockWhenFinished, (x) => MyTimer.Change(TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan));
        }
    }
}
