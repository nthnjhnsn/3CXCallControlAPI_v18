using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TCX.Configuration;

namespace OMSamples.Samples
{
    class StatListener : PsTypeEventListener<Statistics>
    {
        string StatisticsClass;
        string StatisticsInfo(Statistics s)
        {
            return $"{StatisticsClass}.{s?.GetName()}.{s?.ID}: {string.Join("\r\n", s.Content.OrderBy(x=>x.Key).Select(x=>$"{x.Key}={x.Value}"))}";
        }
        PhoneSystem ps;
        public StatListener(string statClass, PhoneSystem ps_in)
            : base(statClass)
        {
            ps = ps_in;
            StatisticsClass = statClass;
            ps.InitializeStatistics(StatisticsClass);
            SetTypeHandler(
                (x) => Console.WriteLine($"UPDATED {StatisticsInfo(x)}"),
                (x) => Console.WriteLine($"INSERTED {StatisticsInfo(x)}"),
                (x, y) => Console.WriteLine($"DELETED {StatisticsInfo(x)}"),
                null, null);
        }
        public override void Dispose()
        {
            ps.DeinitializeStatistics(StatisticsClass);
            base.Dispose();
        }
    }

    [SampleCode("statmonitor")]
    [SampleParam("arg1..agrN", "Statistics ")]
    [SampleDescription("Shows notificatins for specific statistics object.")]
    class StatisticsMonitorSample : ISample
    {
        public void Run(PhoneSystem ps, params string[] args)
        {
            using (var listeners = args.Skip(1).Select(x =>
                {
                    var all_stat = ps.InitializeStatistics(x);
                    //print statistics content
                    Console.WriteLine($"{x}={{\n{string.Join("\n    ", all_stat.Select(y => $"{y.GetName()}.{y.ID}"))}\n}}");
                    foreach(var stat in all_stat)
                    {
                        System.Diagnostics.Debug.Assert(ps.CreateStatistics(x, stat.GetName()).ID != 0);
                    }
                    return new StatListener(x, ps);
                }
                ).ToArray().GetDisposer())
            {
                while (!Program.Stop)
                {
                    Thread.Sleep(5000);
                }
            }
        }
    }
}