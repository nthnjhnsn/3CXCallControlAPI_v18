using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TCX.Configuration;

namespace OMSamples.Samples
{

    class DNListener : PsTypeEventListener<DN>
    {
        static internal ConcurrentDictionary<int, string> fullset;
        static string DestToString(DestinationStruct dest)
        {
            return $"{dest.To}:{dest.Internal?.Number ?? dest.External}";
        }
        static string printInboundRule(TCX.Configuration.ExternalLineRule r)
        {
            var fwd = r.ForwardDestinations;
            return $"{r.ID}-{r.Conditions.Condition.Type}-{r.Conditions.CallType.Type}-{r.Conditions.Hours.Type}-{r.Data}:{DestToString(fwd.OfficeHoursDestination)}-({fwd.AlterDestinationDuringOutOfOfficeHours}){DestToString(fwd.OutOfOfficeHoursDestination)}-({fwd.AlterDestinationDuringHolidays}){DestToString(fwd.HolidaysDestination)}";
        }

        public static string DNInfo(DN dn, bool addrules = false)
        {
            if (dn == null)
            {
                return "<NULL>";
            }
            if (dn is ExternalLine el)
            {
                return $"{dn.GetType().Name}.{dn.Number}[ID/Hash={dn.ID}]" + (addrules?
                    ($":\nInOffice:\n    {string.Join("\n    ", el.RoutingRules.Select(x => printInboundRule(x)))}\n"):"");
            }
            else
            {
                return $"{dn.GetType().Name}.{dn.Number}[ID/Hash={dn.ID}]" + (addrules ?
                    ($":\nInOffice:\n    {string.Join("\n    ", dn.InOfficeInboundReferences.Select(x => printInboundRule(x)))}\n" +
                    $"OutOfOffice:\n    {string.Join("\n    ", dn.OutOfOfficeInboundReferences.Select(x => printInboundRule(x)))}\n" +
                    $"Holidays:\n    {string.Join("\n    ", dn.HolidayInboundReferences.Select(x => printInboundRule(x)))}\n") : "");
            }
        }

        public DNListener(PhoneSystem ps)
            : base("DN")
        {
            SetTypeHandler(
                                //updated
                                (x) => Console.WriteLine($"UPDATED {DNInfo(x, true)}"),
                                //inserted
                                (x) => Console.WriteLine($"INSERTED {DNInfo(x, true)}"),
                                //deleted
                                (x, y) => Console.WriteLine($"DELETED {DNInfo(x, true)}"),
                                null, null);
            fullset = new ConcurrentDictionary<int, string>(ps.GetDN().ToDictionary(x => x.ID, x => x.Number));
        }
    }
    class RegistrationListener : PsTypeEventListener<DN>
    {
        string RegistrationInfo(DN dn)
        {
            if (dn == null)
            {
                return "<NULL>";
            }
            return $"{DNListener.DNInfo(dn)}=Registrar\n[\n{string.Join("\n    ", dn.GetRegistrarContactsEx().Select(x => $"ID/Hash={x.ID} - {x.Contact}"))}\n]";
        }

        public RegistrationListener()
            : base("REGISTRATION")
        {
            SetTypeHandler(
                                //updated
                                (x) => Console.WriteLine($"REGISTRATION UPDATED {RegistrationInfo(x)}"),
                                //inserted
                                (x) => Console.WriteLine($"REGISTRATION INSERTED {RegistrationInfo(x)}"),
                                //deleted
                                (x, y) => Console.WriteLine($"REGISTRATION DELETED {RegistrationInfo(x)}"),
                                null, null);
        }
    }

    class VoiceMailBoxListener : PsTypeEventListener<DN>
    {
        string VoiceMailInfo(DN dn)
        {
            return $"{DNListener.DNInfo(dn)}=VMBOX({dn?.VoiceMailBox.New}/{dn?.VoiceMailBox.Total})";
        }
        public VoiceMailBoxListener()
            : base("VMBOXINFO")
        {
            SetTypeHandler(
                                //updated
                                (x) => Console.WriteLine($"UPDATED {VoiceMailInfo(x)}"),
                                //inserted
                                (x) => Console.WriteLine($"REGISTRATION INSERTED {VoiceMailInfo(x)}"),
                                //deleted
                                (x, y) => Console.WriteLine($"REGISTRATION DELETED {VoiceMailInfo(x)}"),
                                null, null);
        }
    }
    [SampleCode("dn_monitor")]
    [SampleDescription("Shows how to listen for DN updates")]
    class DNmonitorSample : ISample
    {
        public void Run(PhoneSystem ps, params string[] args)
        {
            using (var disposer = new PsArgsEventListener[] {
            new DNListener(ps),
            new RegistrationListener(),
            new VoiceMailBoxListener()
            }.GetDisposer())
            {

                while (!Program.Stop)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var elapsed = sw.Elapsed;
                    try
                    {
                        /*
                        using (var all = DNListener.fullset.Values.Select(x => ps.GetDNByNumber(x)).Where(x => x != null).ToArray().GetDisposer())
                        {
                            var currentelapsed = (sw.Elapsed - elapsed).TotalMilliseconds;
                            Console.WriteLine($"ByNumber: {currentelapsed}ms ({all.Value.Length}) - {currentelapsed / all.Value.Length}ms per item");
                        }
                        elapsed = sw.Elapsed;
                        using (var all = DNListener.fullset.Keys.Select(x => ps.GetByID<DN>(x)).Where(x => x != null).ToArray().GetDisposer())
                        {
                            var currentelapsed = (sw.Elapsed - elapsed).TotalMilliseconds;
                            Console.WriteLine($"ByID: {currentelapsed}ms ({all.Value.Length}) - {currentelapsed / all.Value.Length}ms per item");
                        }
                        */
                        Thread.Sleep(1000);
                    }
                    finally
                    {
                        sw.Stop();
                    }
                    Thread.Sleep(1000);
                }
            }
        }
    }
}