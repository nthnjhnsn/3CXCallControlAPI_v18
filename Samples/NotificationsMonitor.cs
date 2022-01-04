using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCX.Configuration;
using System.Threading;

namespace OMSamples.Samples
{
    class MyLog : PhoneSystem.ILog
    {
        public void Critical(string mess, params object[] varArgs)
        {
            Console.WriteLine($"{DateTime.Now.TimeOfDay}|CRT:{mess}", varArgs);
        }

        public void Dispose()
        {
        }

        public void Error(string mess, params object[] varArgs)
        {
            Console.WriteLine($"{DateTime.Now.TimeOfDay}|ERR:{mess}", varArgs);
        }

        public void Exception(Exception ex)
        {
            Console.WriteLine($"{DateTime.Now.TimeOfDay}|EXC:{ex}");
        }

        public void Info(string mess, params object[] varArgs)
        {
            Console.WriteLine($"{DateTime.Now.TimeOfDay}|INF:{mess}", varArgs);
        }

        public void Trace(string mess, params object[] varArgs)
        {
            Console.WriteLine($"{DateTime.Now.TimeOfDay}|TRC:{mess}", varArgs);
        }
    }

    [SampleCode("notifications")]
    [SampleParam("arg1", "Object type name")]
    [SampleDescription("Shows update notifications of specified data class. All notifications will be shown if arg1 is not specified")]
    class NotificationsMonitorSample : ISample
    {
        class MyListener : PsArgsEventListener
        {
            static readonly string indent = new string(' ', 21);
            string UpdateInfo(string action, NotificationEventArgs update)
            {
                var type = update.ConfObject?.GetType();
                if (type != null)
                {
                    type = OMClassSerializationData.Create(update.ConfObject?.GetType())?.MainInterface;
                }
                return $"{indent}{action}: UpdateRef={update.EntityName}.{update.RecID} - ConfObject={(type?.Name) ?? null}.{((IOMSnapshot)update.ConfObject)?.ID}\n{indent}{update.ConfObject}"
                    + ((update.Operation == UpdateOperation.Updated && update.ConfObject is ExternalLine) ?
                    $"\nRULES:\n{indent}    " + string.Join($"\n{indent}    ", (update.ConfObject as ExternalLine).RoutingRules.Select(x => $"{x} - {x.PriorityHint} - '{x.RuleName}' - {x.Conditions.Condition} - {x.Data}")) : "");

            }

            public MyListener(HashSet<string> EntityNames)
            {
                SetArgsHandler(
                                    //updated
                                    (x) => Console.WriteLine(UpdateInfo("UPDATED", x)),
                                    //inserted
                                    (x) => Console.WriteLine(UpdateInfo("INSERTED", x)),
                                    //deleted
                                    (x) => Console.WriteLine(UpdateInfo("DELETED", x)),
                                    (x)=> !EntityNames.Any()||EntityNames.Contains(x.EntityName), null);
            }
            
        }
        public void Run(PhoneSystem ps, params string[] args)
        {
            var classes = new HashSet<string>(args.Skip(1).Select(x=>x.ToUpperInvariant()).Where(x=>!x.StartsWith("LOG")));
            var log = args.Skip(1).Select(x => x.ToUpperInvariant()).Where(x=>x.StartsWith("LOG"));
            if (log.Any())
            {
                PhoneSystem.Logger = new MyLog();
                PhoneSystem.LogTransactionDetails = log.Contains("LOGTRANSACTIONDETAILS");
                PhoneSystem.LogTransactionIds = log.Contains("LOGTRANSACTIONIDS");
            }

            using (var listener = new MyListener(classes))
            {
                while (!Program.Stop)
                {
                    Thread.Sleep(5000);
                }
            }
        }
    }
}