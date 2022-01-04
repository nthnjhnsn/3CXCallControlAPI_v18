using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TCX.Configuration;
using CallFlow;
using System.Collections.Generic;

/// <summary>
/// small sample which shows how script can work with external "web" api
/// </summary>
namespace dummy
{
    /// <summary>
    /// Logic is:
    /// when incoming call is arriving to routing point the script asks caller for the code and then send http request to the configured url.
    /// url us taken from TEST_API_URL DN property, the key for the request is taken from TEST_API_KEY DN property
    /// resulting url is formed as
    /// <TEST_API_URL>?PartnerID={userinput}&Phone={CallerNumber}&ApiKey={<TEST_API_KEY>}
    /// implementation of the url should provide response (JSON) which can be deserialized as ValidateCallResponse object.
    /// The response should deliver required data as specified in the script.
    /// call is delivered to the list of destination (sequential delivery).
    /// </summary>
    public class PartnerRoute : ScriptBase<PartnerRoute>
    {
        private string MakeSourceAddress(string prefix)
        {
            return $"\"{prefix}:{MyCall.Caller.CallerName}\"<sip:_{MyCall.Caller.CallerID}@{MyCall.PS.GetParameterValue("SIPDOMAIN")}>";
        }

        TaskCompletionSource<bool> currentInputCompletion = null;
        void MyDTMFHandler(char x) //simple - collect input and then if '#' is pressed start RoutePartner.
        {
            switch (x)
            {
                case '#':
                    {
                        MyCall.Info("localCompletion.TrySetResult(false)");
                        currentInputCompletion?.TrySetResult(true);
                    }
                    break;
                default:
                    PartnerId = PartnerId + x;
                    MyCall.Info($"PartnerId = {PartnerId}");
                    break;
            }
        }

        Task<bool> CollectUserInputIteration(string prepend_with_prompt, int timeout_seconds)
        {
            //MyCall.Info(CollectUserInputIteration())
            MyCall.CancelReroute();  //sync method
            Task<bool> theTask = null;
            if (!string.IsNullOrEmpty(prepend_with_prompt))
            {
                theTask = MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { prepend_with_prompt }, PlayPromptOptions.Blocked);
            }
            return theTask =
                (
                theTask?.ContinueWith((x) => MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { "CONFIRM" }, PlayPromptOptions.ResetBufferAtStart | PlayPromptOptions.CancelPlaybackAtFirstChar))
                .Unwrap()
                ??
                MyCall.PlayPrompt(null, new[] { "CONFIRM" }, PlayPromptOptions.ResetBufferAtStart | PlayPromptOptions.CancelPlaybackAtFirstChar))
            .ContinueWith((x) =>
            {
                currentInputCompletion?.TrySetResult(false);
                var localCompletion = new TaskCompletionSource<bool>();
                currentInputCompletion = localCompletion;
                Task.Delay(TimeSpan.FromSeconds(timeout_seconds), MyCall.MediaCancellation)
                .ContinueWith(_ => { MyCall.Info("localCompletion.TrySetResult(false)"); localCompletion.TrySetResult(false); }, TaskContinuationOptions.OnlyOnRanToCompletion);
                return currentInputCompletion.Task;
            }).Unwrap()
            .ContinueWith(x => MyCall.PlayPrompt(MyCall.Caller["public_language"],
                !string.IsNullOrEmpty(PartnerId) ? new[] { "YOU_HAVE_ENTERED", $"##{PartnerId}" } : new[] { "INV_INP" }, PlayPromptOptions.Blocked)).Unwrap()
            .ContinueWith(async (x) =>
                {
                    if (!string.IsNullOrEmpty(PartnerId))
                    {
                        try
                        {
                            var result = await (x.Result ? GetPartnerInfo(PartnerId, MyCall.Caller.CallerID, MyCall.MediaCancellation) : Task.FromResult<ValidateCallResponse>(null));
                            if (result != null)
                            {
                                //this is synthetic implementation. If user is known then try to deliver call to the list of destinations
                                //here can be procedure which choose destination basing on information received from webapi
                                return CheckAvailableAndCall(
                                     $"{result.UserType} {result.Level}",
                                     new[] { "0000", "0001", "1011", "1002", "1003" }).Result;
                            }
                        }
                        catch
                        {
                        }
                    }
                    return false;
                }).Unwrap();
        }
        

        void MyPartyChanged() //we need to cancel current flow and restart it with another caller.
        {
            MyCall.Info($"OnPartyChanged: {MyCall.Caller.CallerName}({MyCall.Caller.CallerID}) from {MyCall.Caller.DIDName}({MyCall.Caller.CalledNumber}) - {MyCall.Caller}");
            PartnerId = "";
        }

        string PartnerId { get; set; }
        public override void Start()
        {
            MyCall.OnTerminated += () => MyCall.Info($"Terminated {MyCall.Caller.CallerID}");
            MyCall.OnDTMFInput += MyDTMFHandler;
            MyCall.OnPartyChanged += MyPartyChanged;
            MyCall.Info($"StartCall: {MyCall.Caller.CallerName}({MyCall.Caller.CallerID}) from {MyCall.Caller.DIDName}({MyCall.Caller.CalledNumber}) - {MyCall.Caller}");
            Task.Run(async
                () =>
                {
                    string theprompt = null;
                    for (int i = 0; i < 3 && ExecutionMode == ScriptExecutionMode.Active; i++)
                    {
                        PartnerId = "";
                        try
                        {
                            if (await CollectUserInputIteration(theprompt, 20))
                            {
                                System.Diagnostics.Debug.Assert(ExecutionMode == ScriptExecutionMode.Wrapup);
                                //here is wrapup actions without MyCall...
                                break;
                            }
                            else
                            {
                                theprompt = "BUSY";
                            }
                        }
                        catch (Exception ex)
                        {
                            MyCall.Error("Main loop exception:");
                            MyCall.Exception(ex);
                        }
                    }
                    if (ExecutionMode == ScriptExecutionMode.Active)
                    {
                        MyCall.SwitchTo("Common.GoodByeAndTerminate");
                    }
                    else
                    {
                        MyCall.Return(false);
                    }
                }
                );

        }

        Task<bool> CheckAvailableAndCall(string prefix, IEnumerable<string> Destinations)
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
                        try
                        {
                            await MyCall.RouteTo(destination, MakeSourceAddress(prefix), 10);
                        }
                        catch(Exception ex){MyCall.Exception(ex);}
                        return "CALLTRAN_FAILED";
                    }
                    else
                        return "BUSY";
                })
                .Unwrap()
                .ContinueWith(__ =>
                            MyCall.PlayPrompt(MyCall.Caller["public_language"], new[] { $"##{destination}", __.Result }, PlayPromptOptions.Blocked)
                ).Unwrap();
            }
            return retval;
        }

        //expected structure which should be returned by external api
        class ValidateCallResponse
        {
            public string UserType { get; set; }
            public string Level { get; set; }
            public bool Enabled { get; set; }
            public bool Active { get; set; }
            public bool SupportExpired { get; set; }
        }

        async Task<ValidateCallResponse> GetPartnerInfo(string partnerId, string phone, CancellationToken token)
        {
            string responseText = null;
            try
            {

                string apiKey = MyCall.DN.GetPropertyValue("TEST_API_KEY"); //should be specified as dn property

                //first part of url should as dn property. 
                //Parameters od the url are predefined and WEBAPI should undesrstand them.
                string url = $"{MyCall.DN.GetPropertyValue("TEST_API_URL")}?PartnerID={partnerId}&Phone={phone}&ApiKey={apiKey}";
                MyCall.Info("StartHTTP with {0}", url);
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(url, token);
                    responseText = await response.Content.ReadAsStringAsync();
                    MyCall.Info($"{response}:\nStatusCode: {response.StatusCode}\n{responseText}");
                }

                try
                {
                    var parsedResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<ValidateCallResponse>(responseText);
                    MyCall.Info("end HTTP");
                    return parsedResponse;
                }
                catch
                {
                    MyCall.Error("Response parsing failure");
                    throw;
                }

            }
            catch (Exception ex)
            {
                MyCall.Error($"GetPartnedInfo failed:\n {responseText}\n{ex}");
                throw;
            }
        }
    }
}