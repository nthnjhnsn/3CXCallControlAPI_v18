using System;
using System.Threading;
using CallFlow;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// namespace is ignored by scripting engine
/// </summary>
namespace dummy
{
    /// <summary>
    /// This script plays beep and records incoming audio:
    /// "public_record_timeout" key specifies maximal length of the audio
    /// is caller presses any digit - recording is stopped.
    /// </summary>
    public class RecordIncoming : ScriptBase<RecordIncoming>
    {
        Timer MyTimer = null;
        public override void Start()
        {
            MyCall.Info($"{this.GetType()}");
            try
            {
                MyCall.OnTerminated += () =>
                {
                    MyCall.Info($"{GetType()} OnTerminated {MyCall.Caller.CallerID}");
                    MyTimer?.Dispose();
                };

                MyCall.OnDTMFInput += x =>
                {
                    EndRecording(true);
                };

                MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { "BEEP" }, PlayPromptOptions.Blocked | PlayPromptOptions.UnblockWhenFinished,
                        _=>
                        {
                            var FileName = Path.GetTempFileName() + ".wav";
                            MyCall.Info($"Attaching {FileName}");
                            MyCall.AttachCallerData(new Dictionary<string, string> { { "public_lastrecord", FileName } },
                                y =>
                                {
                                    MyCall.Info($"Attached {FileName}");
                                    MyCall.StartRecording(FileName, RecordDirection.Inbound, x=>MyCall.Return(true));
                                    MyTimer = new Timer(z =>
                                    {
                                        EndRecording(true);
                                    }, null, TimeSpan.FromSeconds(int.Parse(MyCall.Caller["public_record_timeout"])), Timeout.InfiniteTimeSpan);
                                }
                            );
                        }
                    );
            }
            catch(Exception ex)
            {
                MyCall.Exception(ex);
                EndRecording(false);
            }
        }

        void EndRecording(bool success)
        {
            MyTimer?.Dispose();
            MyCall?.StopRecording(!success);
            MyCall?.Return(success);
        }
    }
}
