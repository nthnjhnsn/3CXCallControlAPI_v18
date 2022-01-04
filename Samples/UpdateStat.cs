using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCX.Configuration;
using System.Threading;
using System.Diagnostics;

namespace OMSamples.Samples
{
    [SampleCode("update_stat")]
    [SampleDescription("This sample creates and continuously update Statistic object named 'MYSTAT'. After running this sample statistics 'MYSTAT' will be available for create_delete_stat sample")]
    class UpdateStatSample : ISample
    {
        public void Run(PhoneSystem ps, params string[] args)
        {
            //string[] strs = { "фіва", "олдлофів" };
            String[] strs = { "stat_value_1", "stat_value_2" };
            Statistics myStat;
            //ps.InitializeStatistics();
            //var arrayS = Enumerable.Range(1, 1000).Select(x => ps.CreateStatistics($"S_TEST{x}")).ToArray();
            //Console.WriteLine("started");
            //bool swapa = false;
            //var sw = Stopwatch.StartNew();
            //for (int i=0; i<10000; i++)
            //{
            //    foreach (var s in arrayS)
            //    {
            //        s["s1"] = strs[swapa ? 1 : 0];
            //        s["s2"] = strs[swapa ? 0 : 1];
            //    }
            //    foreach (var s in arrayS)
            //    {
            //        var a = s["s1"];
            //        var b = s["s2"];
            //        if(a!= strs[swapa ? 1 : 0]||b != strs[swapa ? 0 : 1])
            //        {
            //            Console.WriteLine("not equal");
            //        }
            //    }
            //    swapa = !swapa;
            //}
            //Console.WriteLine($"Ended {sw.ElapsedMilliseconds}ms");
            //return;
            myStat = ps.CreateStatistics("S_TEST");
            bool swap = false;
            String filter = null;
            if (args.Length > 1)
                filter = args[1];
            using (var listener = new StatListener("STATISTICS", ps))
            {
                int i = 0;
                while (true)
                {
                    if ((++i % 5) == 0)
                        myStat.clearall();
                    else
                    {
                        myStat["s1"] = strs[swap ? 1 : 0];
                        myStat["s2"] = strs[swap ? 0 : 1];
                    }
                    swap = !swap;
                    try
                    {
                        myStat.update();
                        System.Console.WriteLine("(" + i.ToString() + ") NewStat=" + myStat.ToString() + "\n------------");
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine("Exception catched" + e.ToString());
                    }
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
