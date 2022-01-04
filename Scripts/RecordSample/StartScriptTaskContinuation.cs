using CallFlow;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// namespace is ignored by scripting host
/// </summary>
namespace dummy
{
    /// <summary>
    /// This script is providing the same functionality as the RecordSample/StartScriptTast.cs
    /// but uses "task continuation" approach.
    /// Also it shows how to use "LocalRecordingStream" to manage recorded content.
    /// 
    /// Prerequisites - 
    /// Scripts form Scripts/Common folder should be added as CallFlows.
    /// it can be done using 
    /// 1. Scripts/Common/ChooseLanguage.cs should be added as Common.ChooseLanguage CallFlow
    /// 2. Scripts/Common/RecordIncomingAsync.cs should be installed as Common.RecordIncomingAsync CallFlow
    /// This script executes following logic:
    /// 0. sets default destination as specified by FallBackDestination
    /// 1. Calls Common.ChooseLanguage script to attach user language information (see Script/Common/ChooseLanguage.cs)
    /// 2. Says prompt to record name using choosen language.
    /// 3. Calls "Common.RecordIncomingAsync" to record incoming audio.
    /// 4. Store recorded content (LocalRecordingStream) to the temporary file.
    /// 5. Plays recorded content and deletes temporary file
    /// 6. Pools all Destinations and provide report on failures
    /// 7. if call is not delivered to any of the destinations - Returns control to the calling script. in case if the script instance is the rool of current call flow(called by scripting host) - scripting engine will try to deliver call as specified by 
    ///    Default Route (see SetDefaultRoute)
    /// </summary>
    public class TheMain : ScriptBase<TheMain>
    {
        string[] Destinations = { "1002", "1011" };
        string FallBackDestination = "1003";

        public async override void Start()
        {
            CancellationTokenSource mySource = new CancellationTokenSource();
            MyCall.OnTerminated += () => { MyCall.Info($"{GetType()} - terminated"); mySource.Cancel(); };
            MyCall.SetDefaultRoute(FallBackDestination, "\"" + $"Failed to deliver call - {MyCall.Caller.CallerName}");

            try
            {
                await MyCall.Call("Common.ChooseLanguage")
                .ContinueWith(_ => MyCall.Info("Language is set to {0}", MyCall.Caller["public_language"]), TaskContinuationOptions.OnlyOnRanToCompletion)
                .ContinueWith(_ => MyCall.AssureMedia(), TaskContinuationOptions.OnlyOnRanToCompletion)
                .Unwrap() //Assure media
                .ContinueWith(_ => MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { "##" + MyCall.Caller.CallerID, "CONF_NAME" }, PlayPromptOptions.Blocked).Result, TaskContinuationOptions.OnlyOnRanToCompletion)
                .ContinueWith(_ => MyCall.Info("Prompt for name has been played"), TaskContinuationOptions.OnlyOnRanToCompletion)
                .ContinueWith(_ => MyCall.Call("Common.RecordIncomingAsync", new Dictionary<string, string> { { "public_record_timeout", "5" } }), TaskContinuationOptions.OnlyOnRanToCompletion)
                .Unwrap()
                .ContinueWith(_ => 
                {
                    MyCall.Info("Common.RecordIncoming has finished with {0}", _.Result);
                    var i = 0;
                    for (; MyCall.LocalRecordingState == LocalRecordingState.RECORDING; i++)
                    {
                        Task.Delay(10).Wait();
                    }
                    MyCall.Info($"{i} of 10ms iterations spend on waiting for recording content");
                }
                , TaskContinuationOptions.OnlyOnRanToCompletion)
                .ContinueWith(_ => Path.Combine(Path.GetTempPath(), MyCall.DN.Number))
                .ContinueWith(_ => 
                { 
                    Directory.CreateDirectory(_.Result); 
                    return Path.Combine(_.Result, $"{Path.GetRandomFileName()}.wav"); 
                }, TaskContinuationOptions.OnlyOnRanToCompletion)
                .ContinueWith(async _ => 
                {
                    MyCall.Info("await MyCall.LocalRecordingStream.CopyToAsync");
                    var stream = new FileStream(_.Result, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    await MyCall.LocalRecordingStream.CopyToAsync(stream);
                    stream.Dispose();
                    return _.Result;
                }, TaskContinuationOptions.OnlyOnRanToCompletion).Unwrap()
                .ContinueWith(async _ =>  
                {
                    MyCall.Info("await MyCall.PlayPrompt");
                    await MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { _.Result }, PlayPromptOptions.Blocked);
                    return _.Result;
                }, TaskContinuationOptions.OnlyOnRanToCompletion)
                .Unwrap()
                .ContinueWith(_ =>
                   {
                       MyCall.Info("{0} has playback finished", _.Result);
                       File.Copy(_.Result, @"D:\Temp\lastscriptplayed.wav");
                       File.Delete(_.Result);
                       MyCall.Info("file {0} removed", _.Result);
                   }
                   ,
                   TaskContinuationOptions.OnlyOnRanToCompletion
                )
                .ContinueWith(_ =>
                {
                    Task<bool> retval = null;
                    foreach (var destination in Destinations)
                    {
                        retval = (retval?.ContinueWith(
                                __ => MyCall.Call("Common.CheckAvailable", new Dictionary<string, string> { { "public_destination", destination } })
                        ).Unwrap() ?? MyCall.Call("Common.CheckAvailable", new Dictionary<string, string> { { "public_destination", destination } }))
                        .ContinueWith(async __ =>
                        {
                            if (__.Result)
                            {
                                MyCall.Info("Calling {0}", destination);
                                await MyCall.RouteTo(destination, MyCall.Caller.CallerID, 10);
                                return "CALLTRAN_FAILED";
                            }
                            else
                                return "BUSY";
                        }
                        , TaskContinuationOptions.OnlyOnRanToCompletion)
                        .Unwrap()
                        .ContinueWith(__ =>
                                    MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { $"##{destination}", __.Result }, PlayPromptOptions.Blocked)
                                    , TaskContinuationOptions.OnlyOnRanToCompletion
                        ).Unwrap();
                    }
                    return retval;
                }
                , TaskContinuationOptions.OnlyOnRanToCompletion).Unwrap()
                 .ContinueWith(__ =>
                {
                    MyCall.Info("No available destinations. Return with {0}", false);
                    MyCall.Info("Script finished");
                    MyCall.SwitchTo("Common.GoodByeAndTerminate");
                }
                , TaskContinuationOptions.OnlyOnRanToCompletion);
            }
            catch(TaskCanceledException )
            {
                //here is wrapup
            }
            catch (Exception ex)
            {
                MyCall.Exception(ex);
            }
            finally
            {
                MyCall.Return(false); //return to calling script
            }
        }
    }
}
