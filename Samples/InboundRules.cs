using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCX.Configuration;
using System.Threading;

namespace OMSamples.Samples
{
    [SampleCode("inboundrule")]
    [SampleParam("arg1", "showall  | update       ")]
    [SampleParam("arg2", "[lineDN] | lineDN       ")]
    [SampleParam("arg3", "         | ruleID       ")]
    [SampleParam("arg4", "         | <All|InOffice|OutOfOffice>.<DestinationType>.[DN].[ExternalNumber]>")]
    [SampleDescription("Set inbound rule destination")]
    class ExternalLineRule: ISample
    {
        string PrintDest(ref DestinationStruct dest)
        {
            return $"{dest.To}.{dest.Internal?.Number}.{dest.External}";
        }
        public void Run(PhoneSystem ps, params string[] args)
        {
            switch(args[1])
            {
                case "showall":
                    {
                        using (var lines = ((args.Length > 2) ? new[] { ps.GetDNByNumber(args[2]) as ExternalLine } : ps.GetExternalLines()).GetDisposer())
                        {
                            foreach (var line in lines)
                            {
                                Console.WriteLine(line);
                                foreach(var rule in line.RoutingRules)
                                {
                                    var destinations = rule.ForwardDestinations;
                                    var office = destinations.OfficeHoursDestination;
                                    var outoffice = destinations.OfficeHoursDestination;
                                    Console.WriteLine($"    {rule} - InOffice.{PrintDest(ref office)} - OutOfOffice.{PrintDest(ref outoffice)}");
                                }
                            }
                        }
                    }
                    break;
                case "update":
                    using (var line = ps.GetDNByNumber(args[2]) as ExternalLine)
                    {
                        int ruleid = int.Parse(args[3]);
                        var therule = line.RoutingRules.First(x => x.ID == ruleid);
                        var destinationparts = args[4].Split('.');
                        var dest = therule.ForwardDestinations;
                        dest.AlterDestinationDuringOutOfOfficeHours = true;
                        var DNnumber = string.Join('.', destinationparts.Skip(2).Take(destinationparts.Length - 3));
                        var theDN = ps.GetDNByNumber(DNnumber);
                        if (!string.IsNullOrEmpty(DNnumber) && (theDN == null || theDN is ExternalLine))
                            throw new ArgumentOutOfRangeException("DN number is not valid");
                        switch (destinationparts[0])
                        {
                            case "All":
                                {
                                    dest.OfficeHoursDestination = new DestinationStruct(Enum.Parse<DestinationType>(destinationparts[1]), theDN, destinationparts.Last());
                                    dest.OutOfOfficeHoursDestination = new DestinationStruct(Enum.Parse<DestinationType>(destinationparts[1]), theDN, destinationparts.Last());
                                }
                                break;
                            case "InOffice":
                                dest.OfficeHoursDestination = new DestinationStruct(Enum.Parse<DestinationType>(destinationparts[1]), theDN, destinationparts.Last());
                                break;
                            case "OutOfOffice":
                                dest.OutOfOfficeHoursDestination = new DestinationStruct(Enum.Parse<DestinationType>(destinationparts[1]), theDN, destinationparts.Last());
                                break;
                            default:
                                throw new InvalidOperationException($"Undefined desination time '{destinationparts[0]}");
                        }
                        line.Save();
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Action '{args[1]}' is not supported");
            }
        }
    }
}
