using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCX.Configuration;

namespace OMSamples.Samples
{
    [SampleCode("connection")]
    [SampleWarning("")]
    [SampleDescription("shows how to work with ActiveConnection objects")]
    [SampleParam("arg1", "dnregs                |answer|ondn |all|drop  |pickup |divertvm|divert |bargein |listen |whisper|record         |transfer|join   |makecall|callservice      |attacheddata       |dn_media_stat|call_media_stat|media_monitor |")]
    [SampleParam("arg2", "numstartswith or [all]|achash|dnnum|   |achash|achash |achash  |achash |achash  |achash |achash |achash         |achash  |achash |reghash |servicename      |achach             |dn-number    |call-id        |toshow        |")]
    [SampleParam("arg3", "additional-keys       |      |     |   |      |destnum|        |destnum|reghash |reghash|reghash|RecordingAction|destnum |achash2|destnum |list of key=value|[list of key=value]|0 or 1       |0 or 1         |all|[dn [...]]|")]
    class ActiveConnections : ISample
    {
        Statistics[] toshow;
        int STATLINES = 20;
        int lastPrintwidth = int.MaxValue;
        void RefreshHeader(PhoneSystem ps)
        {
            if (lastPrintwidth != Console.WindowWidth)
            {
                lastPrintwidth = Console.WindowWidth;
                Console.SetCursorPosition(0, 0);
                Console.Write($"{"name",-25}{"RTT",-6}{"LJ",-5}{"RJ",-5}{"LFr",-8}{"RFr",-8}{"Llost",-10}{"Rlost",-10}{"BindAddr",-30}{"DestinationSDP",-30}{"",-20}".PadRight(Console.WindowWidth).Substring(0, Console.WindowWidth));
                Console.SetCursorPosition(0, 1);
                Console.Write($"{"",-77}{"SDPAddr",-30}{"Destination",-30}{"State",-20}".PadRight(Console.WindowWidth).Substring(0, Console.WindowWidth));
                RefreshList(ps);
            }
        }
        void InitializeStat(PhoneSystem ps)
        {
            toshow = new Statistics[STATLINES];
            ps.InitializeStatistics("S_MS_REPORTS");
            RefreshHeader(ps);
        }
        void RefreshList(PhoneSystem ps)
        {
            var res = ps.GetAllStatisticsRecords("S_MS_REPORTS").OrderByDescending(x => x.ID).Take(STATLINES).OrderBy(x => x.ID).ToArray();
            Array.Clear(toshow, res.Length, toshow.Length - res.Length);
            Array.Copy(res, toshow, res.Length);
            var line = 0;
            foreach (var a in toshow)
            {
                PrintStatAt(line += 2, a);
            }
        }
        void PrintStatAt(int line, Statistics s)
        {
            var tmpBk = Console.BackgroundColor;
            var tmpFg = Console.ForegroundColor;

            Console.BackgroundColor = (line % 4) != 0 ? tmpBk : tmpFg;
            Console.ForegroundColor = (line % 4) != 0 ? tmpFg : tmpBk;
            Console.SetCursorPosition(0, line);
            if (s != null)
            {
                Console.Write($"{s?.GetName(),-25}{s?["RTCPRoundTrip"],-6}{s?["JitterLocal"],-5}{s?["JitterRemote"],-5}{s?["FractionLostLocal"],-8}{s?["FractionLostRemote"],-8}{s?["TotalLostLocal"],-10}{s?["TotalLostRemote"],-10}{s?["BindAddr"],-30}{s?["DestinationSDP"],-30}{"",-20}".PadRight(Console.WindowWidth).Substring(0, Console.WindowWidth));
            }
            else
            {
                Console.Write("Empty".PadRight(Console.WindowWidth).Substring(0, Console.WindowWidth));
            }
            Console.SetCursorPosition(0, line + 1);
            Console.Write($"{"",-77}{s?["SDPAddr"],-30}{s?["Destination"],-30}{s?["State"],-20}".PadRight(Console.WindowWidth).Substring(0, Console.WindowWidth));
            Console.BackgroundColor = tmpBk;
            Console.ForegroundColor = tmpFg;
        }

        void UpdateStat(PhoneSystem ps, Statistics mediastat)
        {
            if (mediastat.ID == 0)
                return;
            var index = Array.IndexOf(toshow, mediastat);
            if (index == -1) //0 based index - not found
            {
                var last = toshow.Last();
                var first = toshow.FirstOrDefault();

                index = Array.IndexOf(toshow, null); //first available index
                if (index == -1) //array is full
                {
                    if (last.ID < mediastat.ID) //more recent then last. scroll up and append new one
                    {
                        Array.Copy(toshow, 1, toshow, 0, STATLINES - 1);
                        index = STATLINES - 1;
                    }
                    else if (first.ID > mediastat.ID)
                    {
                        return;//nothing to update
                    }
                }
                else if (index != 0)
                {
                    if (first.ID > mediastat.ID && toshow[index - 1].ID < first.ID)
                    {
                        RefreshHeader(ps);
                    }
                }

            }
            RefreshHeader(ps);
            toshow[index] = mediastat;
            PrintStatAt((index + 1) * 2, mediastat);
        }

        void DeleteStat(PhoneSystem ps, Statistics mediastat, int RecID)
        {
            var index = Array.IndexOf(toshow, mediastat);
            if (index != -1)
            {
                RefreshHeader(ps);
                Array.Copy(toshow, index + 1, toshow, index, toshow.Length - index - 1);
                toshow[toshow.Length - 1] = null;
                var line = index * 2;
                foreach (var a in toshow.Skip(index))
                {
                    PrintStatAt(line += 2, a);
                    index++;
                }
                for (; index < STATLINES; index++)
                {
                    PrintStatAt(line += 2, null);
                }
            }
        }

        string connectionAsString(ActiveConnection ac)
        {
            return $"ID={ac.ID}:CCID={ac.CallConnectionID}:S={ac.Status}:DN={ac.DN.Number}:EP={ac.ExternalParty}:REC={ac.RecordingState}";
        }

        void PrintAllCalls(PhoneSystem ps)
        {
            foreach (var c in ps.GetActiveConnectionsByCallID())
            {
                Console.ResetColor();
                Console.WriteLine($"Call {c.Key}:");
                foreach (var ac in c.Value.OrderBy(x => x.CallConnectionID))
                {
                    Console.WriteLine($"    {connectionAsString(ac)}");
                }
            }
        }

        void PrintDNCall(Dictionary<ActiveConnection, ActiveConnection[]> ownertoparties)
        {
            try
            {
                foreach (var kv in ownertoparties)
                {
                    Console.WriteLine($"Call {kv.Key.CallID}:");
                    var owner = kv.Key;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"    {connectionAsString(owner)}");
                    Console.ResetColor();
                    foreach (var party in kv.Value)
                    {
                        Console.WriteLine($"    {connectionAsString(party)}");
                    }
                }
            }
            finally
            {
                Console.ResetColor();
            }
        }
        class RuntimeMediaMonitorActivator : PsTypeEventListener<ActiveConnection>
        {
            readonly HashSet<int> activateondns;
            readonly HashSet<int> activatedAC = new HashSet<int>();
            public RuntimeMediaMonitorActivator(params string[] dnlist)
                :base("CONNECTION")
            {
                if (dnlist.Length > 0 && dnlist[0] != "all")
                {
                    activateondns = dnlist.Select(x => PS.GetDNByNumber(x)?.ID).Where(x => x.HasValue).Select(x => x.Value).ToArray().ToHashSet();
                }

                SetTypeHandler(Updated, Updated, Deleted);
            }
            void Updated(ActiveConnection ac)
            {
                if ((ac?.ID ?? 0) == 0 || activatedAC.Contains(ac.ID) || (!(activateondns?.Contains((ac?.DN?.ID ?? 0)) ?? true)))
                    return;
                PS.InvokeCommand("request-ms-report",
                    new Dictionary<string, string>
                    { { "call-id", ac.CallID.ToString() }, { "leg-id", ac.CallConnectionID.ToString() }, { "startstop","1"} }
                );
                activatedAC.Add(ac.ID);
            }
            void Deleted(ActiveConnection ac, int id)
            {
                activatedAC.Remove(id);
            }
        }

        public void Run(PhoneSystem ps, params string[] args)
        {
            //var calls = ps.GetActiveConnectionsByCallID();
            switch (args[1])
            {
                case "dnregs":
                    {
                        if (args[2] == "all")
                        {
                            foreach (var r in ps.GetAll<RegistrarRecord>().OrderBy(x => x.ID))
                            {
                                Console.WriteLine($"{r.ID}-{r.Contact}-{r.UserAgent}-{string.Join("", args.Skip(3).Select(x => $"\n\t{x}={r[x]}"))}");
                            }
                        }
                        else
                        {
                            foreach (var r in ps.GetDNByNumber(args[2]).GetRegistrarContactsEx().OrderBy(x=>x.ID))
                            {
                                Console.WriteLine($"{r.ID}-{r.Contact}-{r.UserAgent}-{string.Join("", args.Skip(3).Select(x => $"\n\t{x}={r[x]}"))}");
                            }
                        }
                    }
                    break;
                case "ondn":
                    {
                        using (var dn = ps.GetDNByNumber(args[2]))
                        {
                            using (var connections = dn.GetActiveConnections().GetDisposer())
                            {
                                var alltakenconnections = connections.ToDictionary(x => x, y => y.OtherCallParties);
                                PrintDNCall(alltakenconnections);
                                foreach (var a in alltakenconnections.Values)
                                {
                                    a.GetDisposer().Dispose();
                                }
                            }
                        }
                    }
                    break;
                case "all":
                    {
                        PrintAllCalls(ps);
                    }
                    break;
                case "drop":
                    ps.GetByID<ActiveConnection>(int.Parse(args[2])).Drop();
                    break;
                case "answer":
                    ps.GetByID<ActiveConnection>(int.Parse(args[2])).Answer();
                    break;
                case "pickup":
                    ps.PickupCall(args[3], ps.GetByID<ActiveConnection>(int.Parse(args[2])));
                    break;
                case "divertvm":
                    {
                        var ac = ps.GetByID<ActiveConnection>(int.Parse(args[2]));
                        ac.Divert(ac.DN.Number, true);
                    }
                    break;
                case "divert":
                    ps.GetByID<ActiveConnection>(int.Parse(args[2])).Divert(args[3], false);
                    break;
                case "bargein":
                    ps.GetByID<ActiveConnection>(int.Parse(args[2])).Bargein(ps.GetByID<RegistrarRecord>(int.Parse(args[3])), TCX.PBXAPI.PBXConnection.BargeInMode.BargeIn);
                    break;
                case "listen":
                    ps.GetByID<ActiveConnection>(int.Parse(args[2])).Bargein(ps.GetByID<RegistrarRecord>(int.Parse(args[3])), TCX.PBXAPI.PBXConnection.BargeInMode.Listen);
                    break;
                case "whisper":
                    ps.GetByID<ActiveConnection>(int.Parse(args[2])).Bargein(ps.GetByID<RegistrarRecord>(int.Parse(args[3])), TCX.PBXAPI.PBXConnection.BargeInMode.Whisper);
                    break;
                case "record":
                    {
                        if (Enum.TryParse(args[3], out RecordingAction ra))
                            ps.GetByID<ActiveConnection>(int.Parse(args[2])).ChangeRecordingState(ra);
                        else
                            throw new ArgumentOutOfRangeException("Invalid record action");
                    }
                    break;
                case "transfer":
                    ps.GetByID<ActiveConnection>(int.Parse(args[2])).ReplaceWith(args[3]);
                    break;
                case "join":
                    {
                        ps.GetByID<ActiveConnection>(int.Parse(args[2])).ReplaceWithPartyOf(
                            ps.GetByID<ActiveConnection>(int.Parse(args[3])));
                    }
                    break;
                case "makecall":
                    {
                        using (var ev = new AutoResetEvent(false))
                        using (var listener = new PsTypeEventListener<ActiveConnection>())
                        using (var registrarRecord = ps.GetByID<RegistrarRecord>(int.Parse(args[2])))
                        {
                            listener.SetTypeHandler(null, (x) => ev.Set(), null, (x) => x["devcontact"].Equals(registrarRecord.Contact), (x) => ev.WaitOne(x));
                            ps.MakeCall(registrarRecord, args[3]);
                            try
                            {
                                if (listener.Wait(5000))
                                {
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine("Call initiated");
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine("Call is not initiated in 5 seconds");
                                }
                            }
                            finally
                            {
                                Console.ResetColor();
                            }
                        }
                    }
                    break;
                case "attacheddata":
                    {
                        var ac = ps.GetByID<ActiveConnection>(int.Parse(args[2]));
                        Console.WriteLine("AttachedData:");
                        Console.WriteLine(string.Join("\n    ", ps.GetByID<ActiveConnection>(int.Parse(args[2])).AttachedData.Select(x => x.Key + "=" + x.Value).ToArray()));
                        var data = args.Skip(3).Select(x => x.Split('=')).Where(x => x[0].StartsWith("public_")).ToDictionary(x => x[0], x => string.Join("=", x.Skip(1)));
                        if (data.Any())
                        {
                            Console.WriteLine("----------");
                            Console.WriteLine("Attaching:");
                            Console.WriteLine(string.Join("\n    ", data.Select(x => x.Key + "=" + x.Value).ToArray()));
                            using (var ev = new AutoResetEvent(false))
                            using (var listener = new PsTypeEventListener<ActiveConnection>())
                            {
                                Console.Write("Wait for update...");
                                listener.SetTypeHandler((x) => { ac = x; ev.Set(); }, null, null, (x) => x.Equals(ac), (x) => ev.WaitOne(x));
                                ac.AttachConnectionData(data);
                                try
                                {
                                    if (listener.Wait(5000))
                                    {
                                        Console.ForegroundColor = ConsoleColor.Green;
                                        Console.WriteLine("Updated:");
                                        Console.WriteLine(string.Join("\n    ", ps.GetByID<ActiveConnection>(int.Parse(args[2])).AttachedData.Select(x => x.Key + "=" + x.Value).ToArray()));
                                    }
                                    else
                                    {
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.WriteLine("No update notifications received.");
                                    }
                                }
                                finally
                                {
                                    Console.ResetColor();
                                }
                            }
                        }

                    }
                    break;
                case "callservice":
                    {
                        ps.ServiceCall(args[2], args.Skip(3).Select(x => x.Split('=')).ToDictionary(x => x[0], x => string.Join("=", x.Skip(1))));
                    }
                    break;
                case "outboundcampaign":
                    {

                        var res = ps.InitializeStatistics("S_QCALLBACK");
                        var tmp = ps.CreateStatistics("S_QCALLBACK", args[2]);
                        if (tmp.ID != 0)
                            tmp.Delete();
                        var stat = PhoneSystem.Root.CreateStatistics("S_QCALLBACK", args[2]);
                        System.Diagnostics.Debug.Assert(stat.ID == 0);
                        stat["destination"] = args[4];
                        stat["queue"] = args[2];
                        stat["display_name"] = $"OutboundCallFrom: {args[3]} to {args[4]}";
                        stat["pv_owner"] = "outbound";
                        stat["pv_source"] = args[3];
                        stat.update(true);
                        var result = "";
                        do
                        {
                            Thread.Sleep(1000);
                            result = ps.GetByID("S_QCALLBACK", stat.ID)["result"];
                            Console.WriteLine($"CallBackResult:{result}");
                        } while (result != "success" && result != "failure" && !Program.Stop);
                        stat.Delete();
                    }
                    break;
                case "dn_media_stat": //sets or resets automatic distribution and show all connection of specfic dn.
                    {
                        using (var dn = ps.GetDNByNumber(args[2]))
                        {
                            var value = args.Skip(3).FirstOrDefault();
                            switch (value)
                            {
                                case "0":
                                case "1":
                                    dn.SetProperty("FORCE_MS_REPORT", value); //value is set
                                    dn.Save();
                                    break;
                                default:
                                    //just show
                                    value = "1";
                                    break;
                            }
                            using (var all = dn.GetActiveConnections().GetDisposer())
                                foreach (var ac in all)
                                {
                                    ps.InvokeCommand("request-ms-report",
                                        new Dictionary<string, string>
                                        { { "call-id", ac.CallID.ToString() }, { "leg-id", ac.CallConnectionID.ToString() }, { "startstop", value} }
                                    );
                                }
                        }
                    }
                    break;
                case "call_media_stat": //use callid provided by "connection ondn" or "connection all"
                    {
                        using (var participants = ps.GetCallParticipants(int.Parse(args[2])).ToArray().GetDisposer())
                            foreach (var ac in participants)
                            {
                                ps.InvokeCommand("request-ms-report",
                                    new Dictionary<string, string>
                                    { { "call-id", ac.CallID.ToString() }, { "leg-id", ac.CallConnectionID.ToString() }, { "startstop", args[3]} }
                                );
                            }
                    }
                    break;
                case "media_monitor":
                    {
                        using (var pslistener = new RuntimeMediaMonitorActivator(args.Skip(3).ToArray()))
                        {
                            using (var listener = new PsTypeEventListener<Statistics>("S_MS_REPORTS"))
                            {
                                STATLINES = int.Parse(args[2]);
                                listener.SetTypeHandler(x => UpdateStat(ps, x), x => UpdateStat(ps, x), (x, y) => DeleteStat(ps, x, y), null, null);
                                InitializeStat(ps);
                                while (!Program.Stop)
                                {
                                    Thread.Sleep(500);
                                }
                            }
                        }
                    }
                    break;
                default:
                    throw new NotImplementedException("action is not implemented");
            }
        }
    }
}
