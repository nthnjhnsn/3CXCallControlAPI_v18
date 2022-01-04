using System;
using System.Threading.Tasks;
using CallFlow;
/// <summary>
/// namespace is ignored by scripting engine
/// </summary>
namespace dummy
{
    /// <summary>
    /// This is synthetic script which plays goodbye Thanks you goodbye prompt using language specified by "public_language" key or default system prompt if not specified
    /// Then Calles ICall.Return which will return cantrol to the Calling script. If there are no calling script call will be either delivered to the
    /// default destination which was set by the callflow, or will be terminated.
    /// </summary>
    public class GoodByeAndTerminate : ScriptBase<GoodByeAndTerminate>
    {
        public override void Start()
        {
            MyCall.Info($"GoodByeAndTerminate: {this.GetType()}");
            (new Task(
               () => 
               {
                  try
                  { 
                      MyCall.Info($"About to send e-mail");
                      (new TcxMail.Mail("stepan@3cx.com", $"GoodByeAndTerminate called for '{MyCall.Caller.CallerName}'{MyCall.Caller.CallerID}", "Script test")).Send();
                      MyCall.Info($"Email Sent");
                  }
                  catch(Exception ex)
                  {
                       MyCall.Info($"{ex}");
                  }
               }
             )
             ).Start();

            MyCall.Info($"GoodByeAndTerminate: task is running");
            MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { "BYE" }, PlayPromptOptions.Blocked, (x) => MyCall.Return(false));
        }
    }
}
