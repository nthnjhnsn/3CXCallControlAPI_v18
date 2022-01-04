using CallFlow;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// namespace is ignored by scripting engine
/// </summary>
namespace dummy
{
    /// <summary>
    /// This sample is using asynchronous Task approach. Behavior is similar to "RecordSample/Startscript"
    /// 0. NEW: specifies defaultdestination (see strin FallBackDestination)
    /// 1. Check/Set caller language by calling 'Common.ChooseLanguage" call flow script
    /// 2. Plays "Record your name after the signal..."
    /// 3. plays recorded audio.
    /// 4. Tries to deliver calls to the destinations specified by 
    /// string[] Destinations
    /// 5. says BUSY or CALLTRAN_FAILED depending on availability of the extension and result of RouteTo.
    /// 6. NEW: Scripting engine will try to deliver call to the default destination in case if script will fail, or destinations will not be available 
    /// </summary>
    public class StartRecordSample : ScriptBase<StartRecordSample>
    {
        //list of destinations
        string[] Destinations = { "1002", "1011" };
        //fallback destination (when script will fail or 
        string FallBackDestination = "1003";

        public override void OnExecutionModeChanged(ScriptExecutionMode prevMode)
        {
            //once 
        }

        public override void Start()
        {
            MyCall.OnTerminated += () => MyCall.Info($"{GetType()} - received OnTerminated event");
            MyCall.SetDefaultRoute(FallBackDestination, $"\"{MyCall.Caller.CallerName} did not reach expected destinations [{string.Join(",", Destinations)}]\"");

            Task.Run(
                async () =>
                {
                    try
                    {
                        await MyCall.Call("Common.ChooseLanguage");
                        MyCall.Info("Language is set to {0}", MyCall.Caller["public_language"]);
                        if (await MyCall.AssureMedia())
                        {
                            await MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { "##" + MyCall.Caller.CallerID, "CONF_NAME" }, PlayPromptOptions.Blocked);
                            MyCall.Info("Prompt for name has been played");
                            await MyCall.Call("Common.RecordIncoming", new Dictionary<string, string> { { "public_record_timeout", "5" } });
                            MyCall.Info("Common.RecordIncoming has finished with {0}", MyCall.Caller["public_lastrecord"]);
                            await MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { MyCall.Caller["public_lastrecord"] }, PlayPromptOptions.Blocked);
                            MyCall.Info("{0} has playback finished", MyCall.Caller["public_lastrecord"]);
                            File.Delete(MyCall.Caller["public_lastrecord"]);
                            MyCall.Info("file {0} removed");
                            ///Gooodbuy and terminate may return true or false.
                            //var goodbyeresult = await MyCall.Call("Common.GoodByeAndTerminate");
                            //MyCall.Info("Common.GoodByeAndTerminate returned with '{0}'", goodbyeresult);
                            foreach (var destination in Destinations)
                            {
                                var message = "BUSY";
                                //check availability of the destination script will not return here.
                                if (await MyCall.Call("Common.CheckAvailable", new Dictionary<string, string> { { "public_destination", destination } }))
                                {
                                    MyCall.Info("Calling {0}", destination);
                                    await MyCall.RouteTo(destination, null, 10);
                                    message = "BUSY";
                                }
                                else
                                {
                                    message = "CALLTRAN_FAILED";
                                }
                                MyCall.Info("{0} not reachable", destination);
                                await MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { $"##{destination}", message }, PlayPromptOptions.Blocked);
                            }
                            //finally say goodbye and return to the "calling context" (either calling script object or route call to the default destination
                            //SwitchTo guarantee no exceptions
                            MyCall.SwitchTo("Common.GoodByeAndTerminate"); 
                       }
                    }
                    catch(TaskCanceledException)
                    {
                        System.Diagnostics.Debug.Assert(ExecutionMode==ScriptExecutionMode.Wrapup);
                        MyCall.Warn("Script was cancelled");
                        //the only one reason of the cancel is expected in this script - call termination.
                        //here can be code which do some "post-termination" actions
                        MyCall.Return(false); //we return control to the previous script to allow it to wrapup call
                    }
                    catch (Exception ex)
                    {
                        MyCall.Exception(ex);
                        MyCall.Return(false); //return control to calling script if any
                    }
                    finally
                    {
                        MyCall.Info("script final message");
                    }
                }, MyCall.MediaCancellation
                );
        }
    }
}
