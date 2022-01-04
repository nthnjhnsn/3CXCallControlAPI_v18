using CallFlow;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// namespace is ignored by scripting engine
/// </summary>
namespace dummy
{
    /// <summary>
    /// This sample is using "callbacks" to build sequence of the actions
    /// 1. Check/Set caller language by calling 'Common.ChooseLanguage" call flow script
    /// 2. Plays "Record your name after the sygnal..."
    /// 3. plays recorded audio.
    /// 4. then Calls "Common.GoodByeAndTerminate" and try to deliver call to the number "1002"
    /// 5. says BUSY or CALLTRAN_FAILED depending on availability of the extension and result of RouteTo.
    /// </summary>
    public class StartRecordSample : ScriptBase<StartRecordSample>
    {
        public override void Start()
        {
            MyCall.OnTerminated += () => MyCall.Info($"{GetType()} - terminated");
            MyCall.Call("Common.ChooseLanguage",
                x => {
                     if(!MyCall.RunWithMedia(() =>
                    MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { "#" + MyCall.Caller.CallerID, "CONF_NAME" }, PlayPromptOptions.Blocked,
                           y => MyCall.Call("Common.RecordIncoming",
                               z => MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { MyCall.Caller["public_lastrecord"] }, PlayPromptOptions.Blocked,
                                   a => MyCall.Call("Common.GoodByeAndTerminate",
                                       b =>
                                       {
                                            MyCall.Info($"{MyCall.Caller["public_lastrecord"]}");
                                           File.Delete(MyCall.Caller["public_lastrecord"]);
                                           MyCall.Call("Common.CheckAvailable",
                                               g =>
                                               {
                                                   if (g)
                                                   {
                                                       MyCall.RouteTo("1011", MyCall.Caller.CallerID, 10,
                                                           pp => MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { "CALLTRAN_FAILED" }, PlayPromptOptions.Blocked, t => MyCall.Terminate())
                                                           );
                                                   }
                                                   else
                                                   {
                                                       MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { "BUSY" }, PlayPromptOptions.Blocked, t => MyCall.Terminate());
                                                   }
                                               }, new Dictionary<string, string> { { "public_destination", "1002" } });
                                       })
                               )
                           , new Dictionary<string, string> { { "public_record_timeout", "5" } })
                    )))
                    {
                        MyCall.Info("Answering call");
                        MyCall.Answer();
                    }
                }
            );

        }
    }
}
