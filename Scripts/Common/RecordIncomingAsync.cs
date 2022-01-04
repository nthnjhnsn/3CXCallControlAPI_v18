using System;
using System.Threading;
using CallFlow;
using System.Threading.Tasks;

namespace dummy
{
    /// <summary>
    /// Another sample which uses asynchronous execution of the Tasks and provides recorded audio only as LocalRecordingStream (not file on the disk)
    /// </summary>
    public class RecordIncomingAsync : ScriptBase<RecordIncomingAsync>
    {
        CancellationTokenSource endOfRecordCancellation = new CancellationTokenSource();
        void dtmfcancellation(char x)
        {
            MyCall.OnDTMFInput -= dtmfcancellation;
            endOfRecordCancellation.Cancel();
        }
        public async override void Start()
        {
            try
            {
                MyCall.OnDTMFInput += dtmfcancellation;
                await MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { "BEEP" }, PlayPromptOptions.Blocked | PlayPromptOptions.UnblockWhenFinished)
                    .ContinueWith(x => MyCall.StartRecording(RecordDirection.Inbound)).Unwrap() //we started recording
                    .ContinueWith(x => endOfRecordCancellation.CancelAfter(TimeSpan.FromSeconds(int.Parse(MyCall.Caller["public_record_timeout"]))))
                    .ContinueWith(x => Task.Delay(Timeout.InfiniteTimeSpan, CancellationTokenSource.CreateLinkedTokenSource(MyCall.MediaCancellation, endOfRecordCancellation.Token).Token))
                    .Unwrap()
                    .ContinueWith(x => MyCall.StopRecording(true), TaskContinuationOptions.OnlyOnCanceled)
                    .ContinueWith(x =>
                    {
                        while (MyCall.LocalRecordingState == LocalRecordingState.RECORDING&& ExecutionMode == ScriptExecutionMode.Active) Task.Delay(100).Wait();
                        MyCall.Return(MyCall.LocalRecordingStream != null);
                    });
            }
            catch (Exception ex)
            {
                MyCall.Exception(ex);
                EndRecording(false);
            }
        }
        public override void OnExecutionModeChanged(ScriptExecutionMode switchedFrom)
        {
            if(ExecutionMode==ScriptExecutionMode.Wrapup)
            {
                endOfRecordCancellation.Cancel();
            }
        }
        void EndRecording(bool success)
        {
            MyCall?.StopRecording(true); //in any case we simply cancel the recording. Content will be captured by LocalRecordingStream.
            MyCall?.Return(success);
        }
    }
}
