using System;
using System.Threading;
using CallFlow;
using System.Collections.Generic;
using TCX.Configuration;
using System.Linq;

/// <summary>
/// namespace is ignored by scripting engine
/// </summary>
namespace dummy
{
    /// <summary>
    /// This script attaches "public_language" key to the Caller attached data.
    /// the value is "prompts set folder" which should be used to obtain the prompts specified by prompt ID.
    /// in sample, the build in property PROMPTSETID of the caller's DN is used
    /// if the DN does not specify valid PromptSet scripts asks caller to choose the language.
    /// result of this script:
    /// Caller connection will be provisioned with "public_language" key and this information will be available until Caller will drop the call.
    /// script can be modified to obtain required language from any external source if required.
    /// script does nothing if "public_language" is already attached to the caller connection
    /// </summary>
    public class ChooseLanguage : ScriptBase<ChooseLanguage>
    {
        class parameters
        {
            public string language;
            public string[] prompts;
            public Action nextAction;
        }
        parameters[] calls;
        int current = 0;
        Timer myTimer = null;
        public override void Start()
        {
            //if language is not attached but DN has specified language
            if(string.IsNullOrEmpty(MyCall.Caller["public_language"]))
            {
                MyCall.Info("Check default language for : {0}", MyCall.Caller.DN.Number);
                var dnlanguage = MyCall.Caller.DN.GetPropertyValue("PROMPTSETID");
                if(!string.IsNullOrEmpty(dnlanguage))
                {
                //lets find it
                    using (var allps = ((PromptSet[])MyCall.PS.GetAll<PromptSet>()).GetDisposer(x => x.Folder == dnlanguage))
                    {
                        if (allps.Any())
                        {
                        //We found it then attach and return
                            MyCall.Info("Set language to '{0}' as specified in DN.{1} configuration", allps.First().PromptSetName, MyCall.Caller.DN.Number);
                            MyCall.AttachCallerData(new Dictionary<string, string> { { "public_language",  allps.First().Folder} },
                                 y =>
                                        {
                                            MyCall.Info($"attached {MyCall.Caller["public_language"]}");
                                            MyCall.Return(y);
                                        }
                                    );
                            return;
                        }
                    }
                }
            }
            if(!MyCall.RunWithMedia(()=>
            {
            try
            {
                using (var allps = ((PromptSet[])MyCall.PS.GetAll<PromptSet>()).GetDisposer(x => x.Folder == MyCall.Caller["public_language"]))
                {
                    if (!allps.Any())
                    {
                        MyCall.Info($"{GetType()} Start with {MyCall.Caller.CallerID}");
                        MyCall.OnTerminated += () =>
                        {
                            MyCall.Info($"{GetType()} OnTerminated {MyCall.Caller.CallerID}");
                        };

                        PhoneSystem ps = MyCall.PS;
                        MyCall.Info($"{GetType()} taken PS");
                        int i = 0;
                        calls = ps.GetAll<PromptSet>()
                            .Select(x =>
                                new parameters
                                {
                                    language = x.Folder,
                                    prompts = new[] { "NUM_" + (i++.ToString()), "CONF_CONFPROMPT" },
                                    nextAction = null
                                }
                             ).ToArray();
                        MyCall.Info($"{calls.Length}");
                        MyCall.OnDTMFInput +=
                            x =>
                            {
                                MyCall.Info($"{x} - Return");
                                MyCall.AttachCallerData(new Dictionary<string, string> { { "public_language", calls[x - '0'].language } },
                                    y =>
                                    {
                                        myTimer?.Dispose();
                                        myTimer = null;
                                        MyCall.Info($"{this.GetType()} - Returns {y}");
                                        MyCall.Return(y);
                                    }
                                );
                            };
                        Next();
                    }
                    else
                    {
                        MyCall.Return(true);
                    }
                }
            }
            catch(Exception ex)
            {
                MyCall.Info($"{ex}");
                MyCall.Return(false);
            }
            return;
            }
            ))
           {
               MyCall.Info("Answering call");
               MyCall.Answer();
           }
        }

        void Next()
        {
            MyCall.Info($"Next with {current}");
            if (current < calls.Length)
            {
                MyCall.PlayPrompt(calls[current].language, calls[current].prompts, PlayPromptOptions.CancelPlaybackAtFirstChar, x => { MyCall.Info("Next()"); if(x) Next(); });
                current++;
            }
            else
            {
                myTimer = new Timer(x=>MyCall?.Return(true), null, TimeSpan.FromSeconds(15), Timeout.InfiniteTimeSpan);
            }
        }
    }
}
