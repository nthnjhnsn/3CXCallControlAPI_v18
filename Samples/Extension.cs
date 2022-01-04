using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCX.Configuration;

namespace OMSamples.Samples
{
    [SampleCode("extension")]
    [SampleParam("arg1", "show       | create   | delete    | update    |lookupemail|duplicate|bulkcreation|bulkdelete|bulkupdate|testmyphonebatch")]
    [SampleParam("arg2", "[dn/dnlist]| dn       | dn        | dn/dnlist |email      |fromext  |dn/dnlist   |dn/dnlist |dn/dnlist |")]
    [SampleParam("arg3", "           | params   |           | paramrs   |           |tonumber |            |          |params    |")]
    [SampleDescription("Working with Extension. Partial configuration\n list_of_parameters is sequence of space separated strings (taken in quotes if required):\n" +
        "    FIRST_NAME=<string> - first name\n" +
        "    LAST_NAME=<string> - last name\n" +
        "    EMAIL=<string> - email\n" +
        "    MOBILE=<numric string> - mobile number\n" +
        "    OUTBOUND_CALLER_ID=<numeric string> - mobile number\n" +
        "    profile.<AvailableProfileNAME>=AV(NA:<DestinationType>.[<number>].[<externalnumber>],[+|-]NAI:<DestinationType>.[<number>].[<externalnumber>],BUSY:<DestinationType>.[<number>].[<externalnumber>],[+|-]BUSYI:<DestinationType>.[<number>].[<externalnumber>])\n" +
        "    profile.<AwayProfileNameNAME>=AW(IALL:<DestinationType>.[<dnnumber>].[<externalnumber>],[+|-]IOOO:<DestinationType>.[<number>].[<externalnumber>],EALL:<DestinationType>.[<number>].[<externalnumber>],[+|-]EOOO:<DestinationType>.[<number>].[<externalnumber>])\n" +
        "    CURRENT_STATUS=<profilename> - name of the current profile\n" +
        "    prop.<NAME>=<value> - set DN property with name <NAME> to the <value>\n"+
        "    OVERRIDE_STATUS=<profilename>,<timespan>\n" +
        "    BINDTOMS=true|false\n" +
        "    REINVITES=true|false\n" +
        "    REPLACES=true|false\n" +
        "    RECORDCALLS=true|false\n"+
        "    SRTP=true|false\n"+
        "    SRTPMODE={SRTPDisabled|SRTPEnabled|SRTPEnforced}\n" +
        "    Extension.<extension_simple_property>=<propval>\n" +
        "    AGENTLOGIN=<listofqueues>"+
        "    AGENTLOGOUT=<listofqueues>"
        )]
    class ExtensionSample : ISample
    {
        string getRandomString(ushort minlen, ushort maxlen)
        {
            return PhoneSystem.GenerateRandomString(minlen, maxlen, PhoneSystem.PasswordGenerationOptions.DigitsLettersLowerCase);
        }
        string DestToString(Destination x)
        {
            return $"{x?.To}.{x?.Internal?.Number}.{x?.External}";
        }

        void ImportCSV(PhoneSystem ps)
        {
            
        }
        public void Run(PhoneSystem ps, params string[] args)
        {
            var initial = ps.GetCacheHealthReport();
            Console.WriteLine(string.Join("\n", ps.GetCacheHealthReport().Select(x => $"{x.Key}={x.Value}")));

            var emaillookup = ps.CreateLookup(()=>ps.GetExtensions(), y=>y.EmailAddress, "DN");
            //for the cases expected dn/dnlist
            var secondparam = args.Skip(2).FirstOrDefault();
            var nums = ((secondparam?.StartsWith("(")??false) && (secondparam?.EndsWith(")")??false)?args[2].Trim(new[] { '(', ')'}).Split(',', StringSplitOptions.RemoveEmptyEntries) : new[] { secondparam }).Where(x=>x!=null);
            if (nums.All(x=>x=="*"))
                nums = ps.GetAll<Extension>().Select(x => x.Number).OrderBy(x=>x).ToArray();
            switch (args[1])
            {
                case "lookupemail":
                    //stright to list
                    break;
                case "bulkregistrarcheck":
                    {
                        var all = PhoneSystem.Root.GetDN();
                        var sw = Stopwatch.StartNew();
                        long totalChecks = 0;
                        var nextreport = sw.Elapsed+TimeSpan.FromSeconds(5);
                        while(!Program.Stop)
                        {
                            var res = all.Count(x => x.IsRegistered);
                            totalChecks += all.Length;
                            if (nextreport < sw.Elapsed)
                            {
                                nextreport = sw.Elapsed+TimeSpan.FromSeconds(5);
                                Console.WriteLine($"{sw.Elapsed.TotalMilliseconds / totalChecks}ms - {res} of {all.Length})");
                            }
                        }
                        return;
                    }
                case "doubledelete":
                    {
                        PhoneSystem.Logger = new MyLog();
                        PhoneSystem.LogTransactionDetails = true;
                        PhoneSystem.LogTransactionIds = true;

                        var tenant = ps.GetTenant();
                        var extension = tenant.CreateExtension(tenant.GetNextAvailableDN("00000"));
                        extension.Save();
                        var extension2 = ps.GetByID<Extension>(extension.ID);
                        Console.WriteLine("Wait for 10 seconds");
                        Task.Delay(10000).Wait();
                        Console.WriteLine("Deleting");
                        var a = new[] { extension, extension2 }.AsParallel().Select(x => { x.Delete(); return 1; }).ToArray();
                        Console.WriteLine("Done");
                        return;
                    }
                case "removephonebook":
                    {
                        using (var extensions = nums.Select(x => ps.GetDNByNumber(x) as Extension).Where(x => x != null).ToArray().GetDisposer())
                        {
                            var sw = Stopwatch.StartNew();
                            using (var res = extensions.SelectMany(x => x.GetPhoneBookEntries()).ToArray().OMDelete("Removing personal phonebook"))
                            {

                            }
                            sw.Stop();
                            Console.WriteLine($"delete of {extensions.Length} phonebook entries is done in {sw.Elapsed.TotalSeconds} seconds");
                        }
                        return;
                    }
                case "testmyphonebatch":
                    File.WriteAllLines("startall.bat", nums.Select(x => ps.GetDNByNumber(x) as Extension).Where(x => x != null).Select(x => $"start MyphoneCmdTest.exe -user={x.Number} -pwd={x.AuthPassword}"));
                    return;
                case "bulkcreation":
                    {
                        var tenant = ps.GetTenant();
                        var max = int.Parse(args.Skip(3).First());
                        var extnumber = args.Skip(2).First();
                        var thelist = new string[max];
                        var extensions = new Extension[max];
                        var sw = Stopwatch.StartNew();
                        for (int i = 0; i < max; i++)
                        {
                            if(i==0)
                                extnumber = tenant.GetNextAvailableDN(args.Skip(2).First());
                            else
                                extnumber = tenant.GetNextAvailableDN($"{int.Parse(thelist[i-1])+1}");
                            thelist[i] = extnumber;
                            Console.Write($"Creating Ext{extnumber}...");
                            var ext = tenant.CreateExtension(extnumber);
                            ext.SetProperty("BULK_CREATED", "1");
                            ext.FirstName = $"{i}";
                            ext.LastName = "BulkCreated";
                            Console.WriteLine($"Done");
                            extensions[i] = ext;
                        }
                        var elapsed = sw.Elapsed;
                        Console.WriteLine($"Creation time: {elapsed} ({extensions.Length})");
                        for (int i = 0; i < max; i += 50)
                        {
                            var elapsed2 = sw.Elapsed;
                            int sz = Math.Min(max - i, 50);
                            var array = extensions.Skip(i).Take(sz).ToArray();
                            using (var transaction = ps.CreateBatch($"OMSamples: Saving {string.Join(",", array.Select(x => x.Number))}"))
                            {
                                transaction.AppendUpdate();
                                transaction.Commit()?.Dispose();
                            }
                            Console.WriteLine($"SavingTime: {sw.Elapsed - elapsed2} ({sz})");
                        }
                        Console.WriteLine($"Total SavingTime: {sw.Elapsed - elapsed} ({extensions.Length})");
                        nums = thelist;
                    }
                    break;
                case "bulkdelete":
                    {
                        var sw = Stopwatch.StartNew();

                        Console.Write($"Prepare bulk delete...");
                        var res = ps.GetDN().Where(x => x.GetPropertyValue("BULK_CREATED") == "1").ToArray();
                        Console.Write($"{res.Length} object(s)...");
                        var elapsed = sw.Elapsed;
                        Console.WriteLine($"Done in {elapsed}");
                        for (int i = 0; i < res.Length; i += 50)
                        {
                            var elapsed2 = sw.Elapsed;
                            var sz = Math.Min(res.Length - i, 50);
                            Console.Write($"Commit {i}-{i + sz}...");
                            var array = res.Skip(i).Take(sz).ToArray();
                            using (var result = array.OMDelete($"Deleting: {string.Join(",", array.Select(x => x.Number))}"))
                            {
                                Console.WriteLine($"{result.Name} \n is Done in {sw.Elapsed - elapsed2}");
                            }
                        }
                        Console.WriteLine($"Total time: {sw.Elapsed-elapsed}");
                        return;
                    }
                case "create":
                case "update":
                    {
                        var sw = Stopwatch.StartNew();
                        var param_set = args.Skip(3).Select(x => x.Split('=')).ToDictionary(x => x.First(), x => string.Join("=", x.Skip(1).ToArray()));
                        //update may be done for the set of the extension
                        //in this case extensions may be specified as the list using (x,y,z,...)
                        var extensions = args[1] == "create" ? new[] { ps.GetTenant().CreateExtension(args[2]) } : nums.Select(x => ps.GetDNByNumber(x) as Extension).Where(x => x != null ).ToArray();
                        string overrideProfileName = null;
                        DateTime overrideExpiresAt = DateTime.UtcNow; //will not be used if there is no OVERRIDE_STATUS option.
                        var need_patch = false;
                        Console.ForegroundColor = ConsoleColor.Red;
                        foreach (var extension in extensions.Where(x=>x.FwdProfiles.Length!=5))
                        {
                            Console.WriteLine($"Extension {extension.Number} exluded due to incurrect set of profiles");
                            need_patch = true;
                        }
                        if(need_patch)
                        {
                            extensions = extensions.Where(x => x.FwdProfiles.Length == 5).ToArray();
                        }
                        Console.ResetColor();
                        foreach (var extension in extensions)
                        {
                            foreach (var paramdata in param_set)
                            {
                                var paramname = paramdata.Key;
                                var paramvalue = CheckRandom(paramdata.Value);
                                switch (paramname)
                                {
                                    case "AGENTLOGOUT":
                                    case "AGENTLOGIN":
                                        {
                                            var data = paramvalue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                            var queues = new HashSet<string>(data);
                                            foreach (var agent in extension.QueueMembership.Where(x => queues.Contains(x.Queue.Number)))
                                            {
                                                agent.QueueStatus = paramname == "AGENTLOGIN" ? QueueStatusType.LoggedIn : QueueStatusType.LoggedOut;
                                            }
                                        }
                                        break;
                                    case "OVERRIDE_STATUS":
                                        {
                                            var data = paramvalue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                            var profile = extension.FwdProfiles.First(x => x.Name == data[0]);
                                            var expiresAt = DateTime.UtcNow + TimeSpan.Parse(data[1]);
                                            if (profile.ID != 0)//profile is in persistent storage we can set override
                                            {
                                                extension.OverrideExpiresAt = expiresAt;
                                                extension.CurrentProfileOverride = profile;
                                            }
                                            else
                                            {
                                                overrideProfileName = data[0];
                                                overrideExpiresAt = expiresAt;
                                            }

                                        }
                                        break;
                                    case "GROUPS":
                                        {
                                            Dictionary<Group, UpdateOperation> updateset;
                                            try
                                            {
                                                updateset = paramvalue.Split(',', StringSplitOptions.RemoveEmptyEntries).ToDictionary
                                                    (
                                                    x => ps.GetGroupByName(x) ?? (x.StartsWith("+") || x.StartsWith("-") ? ps.GetGroupByName(x.Substring(1)) : null), // + or  - is prepended to the group name will fail if gtoup is not specified
                                                    x => ps.GetGroupByName(x) != null ?
                                                    UpdateOperation.Updated :
                                                        x.StartsWith('+') ?
                                                        UpdateOperation.Inserted
                                                        : UpdateOperation.Deleted //
                                                    );
                                            }
                                            catch(Exception ex)
                                            {
                                                throw new InvalidOperationException($"Failed to build group update {ex}");
                                            }
                                            
                                            if (updateset.All(x=>x.Value == UpdateOperation.Updated) //override
                                            || updateset.All(x => x.Value != UpdateOperation.Updated) //add/remove specific
                                            )
                                            {
                                                bool leave_unspecified = !string.IsNullOrEmpty(paramvalue) && updateset.All(x => x.Value != UpdateOperation.Updated);
                                                var current = extension.GroupMembers.Select(x => x.Group.Name).ToHashSet(); //take current

                                                //create set of GroupMember objects which are define NEW membership
                                                var addedTo = updateset.Where(x => !current.Contains(x.Key.Name)&&x.Value!=UpdateOperation.Deleted).Select(x=>x.Key.CreateGroupMember(extension));
                                                //leave only Group Members which are not deleted or 
                                                var remainsIn = extension.GroupMembers.Where(x => updateset.TryGetValue(x.Group, out var action) ? action != UpdateOperation.Deleted : leave_unspecified);
                                                var newMembers = addedTo.Union(remainsIn).ToArray();
                                                //allownogroups - allows to remove extension from all groups
                                                extension.GroupMembers = (param_set.ContainsKey("allowemptygroups") ||
                                                newMembers.Any()) ?
                                                    newMembers :
                                                    new[] { extension.GroupMembers.FirstOrDefault(x => x.Group.Name == "__DEFAULT__") ?? ps.GetGroupByName("__DEFAULT__").CreateGroupMember(extension) };
                                            }
                                            else
                                            {
                                                throw new InvalidOperationException("List of groups should be either override or declare explicit modifications (+- prefix)");
                                            }
                                        }
                                        break;
                                    case "FIRST_NAME":
                                        extension.FirstName = paramvalue;
                                        break;
                                    case "LAST_NAME":
                                        extension.LastName = paramvalue;
                                        break;
                                    case "EMAIL":
                                        extension.EmailAddress = paramvalue;
                                        break;
                                    case "MOBILE":
                                        extension.SetProperty("MOBILENUMBER", paramvalue);
                                        break;
                                    case "OUTBOUND_CALLER_ID":
                                        extension.OutboundCallerID = paramvalue;
                                        break;
                                    case "BINDTOMS":
                                        extension.DeliverAudio = bool.Parse(paramvalue);
                                        break;
                                    case "REINVITES":
                                        extension.SupportReinvite = bool.Parse(paramvalue);
                                        break;
                                    case "REPLACES":
                                        extension.SupportReplaces = bool.Parse(paramvalue);
                                        break;
                                    case "RECORDCALLS":
                                        extension.RecordCalls = bool.Parse(paramvalue);
                                        break;
                                    case "SRTP":
                                        extension.EnableSRTP = bool.Parse(paramvalue);
                                        break;
                                    case "SRTPMODE":
                                        extension.SRTPMode = Enum.Parse<SRTPModeType>(paramvalue);
                                        break;
                                    case "CURRENT_STATUS":
                                        extension.CurrentProfile = extension.FwdProfiles.Where(x => x.Name == paramvalue).First();
                                        break;
                                    case "AUTHID":
                                        if (!string.IsNullOrWhiteSpace(paramvalue) && paramvalue.All(x => Char.IsLetterOrDigit(x)) && Encoding.UTF8.GetBytes(paramvalue).Length == paramvalue.Length)
                                        {
                                            extension.AuthID = paramvalue;
                                        }
                                        else
                                            throw new ArgumentOutOfRangeException("AUTHID should be alphanumeric ASCII");
                                        break;
                                    case "AUTHPASS":
                                        if (!string.IsNullOrWhiteSpace(paramvalue) && paramvalue.All(x => Char.IsLetterOrDigit(x)) && Encoding.UTF8.GetBytes(paramvalue).Length == paramvalue.Length)
                                        {
                                            extension.AuthPassword = paramvalue;
                                        }
                                        else
                                            throw new ArgumentOutOfRangeException("AUTHPASS should be alphanumeric ASCII");
                                        break;
                                    case "allowemptygroups":
                                        //used to allow empty groupmembership don't require any handling
                                        break;
                                    default: //options and TODEST
                                        {
                                            if (paramname.StartsWith("prop."))
                                            {
                                                extension.SetProperty(paramname.Replace("prop.", ""), paramvalue);
                                                break;
                                            }
                                            else if (paramname.StartsWith("profile."))
                                            {
                                                var profilename = paramname.Replace("profile.", "");
                                                var profile = extension.FwdProfiles.Where(x => x.Name == profilename).First();
                                                var options = new string(paramvalue.Skip(3).ToArray()).Trim(')').Split(',').Select(x => x.Split(':')).ToDictionary(x => x[0], x => x[1]);
                                                if (paramvalue.StartsWith("AV(") && paramvalue.EndsWith(")")) //"Available route"
                                                {
                                                    var route = profile.AvailableRoute;
                                                    foreach (var o in options)
                                                    {
                                                        var thekey = o.Key;
                                                        if (thekey.StartsWith("+") || thekey.StartsWith("-"))
                                                        {
                                                            switch (thekey)
                                                            {
                                                                case "+NAI":
                                                                    route.NoAnswer.InternalInactive = false;
                                                                    break;
                                                                case "-NAI":
                                                                    route.NoAnswer.InternalInactive = true;
                                                                    break;
                                                                case "+BUSYI":
                                                                    route.Busy.InternalInactive = route.NotRegistered.InternalInactive = false;
                                                                    break;
                                                                case "-BUSYI":
                                                                    route.Busy.InternalInactive = route.NotRegistered.InternalInactive = true;
                                                                    break;
                                                            }
                                                            if (o.Value == "")
                                                            {
                                                                //just switch activity.
                                                                continue;
                                                            }
                                                            else
                                                            {
                                                                thekey = thekey.Substring(1);
                                                            }
                                                        }
                                                        var data = o.Value.Split('.');
                                                        if (Enum.TryParse(data[0], out DestinationType dt))
                                                        {
                                                            var dest = new DestinationStruct(dt, ps.GetDNByNumber(string.Join(".", data.Skip(1).SkipLast(1))), data.Last());
                                                            switch (thekey)
                                                            {
                                                                case "NA":
                                                                    route.NoAnswer.AllCalls = dest;
                                                                    break;
                                                                case "NAI":
                                                                    route.NoAnswer.Internal = dest;
                                                                    break;
                                                                case "BUSY":
                                                                    route.Busy.AllCalls = route.NotRegistered.AllCalls = dest;
                                                                    break;
                                                                case "BUSYI":
                                                                    route.Busy.Internal = route.NotRegistered.Internal = dest;
                                                                    break;
                                                            }
                                                        }
                                                        else
                                                            throw new ArgumentOutOfRangeException($"Unexpected destination definition{o.Key}<->{o.Value}");
                                                    }
                                                }
                                                if (paramvalue.StartsWith("AW(") && paramvalue.EndsWith(")")) //"Available route"
                                                {
                                                    var route = profile.AwayRoute;
                                                    var external = profile.AwayRoute.External;
                                                    foreach (var o in options)
                                                    {
                                                        var thekey = o.Key;
                                                        if (thekey.StartsWith("+") || thekey.StartsWith("-"))
                                                        {
                                                            switch (thekey)
                                                            {
                                                                case "+EOOO":
                                                                    route.External.OutOfHoursInactive = false;
                                                                    break;
                                                                case "-EOOO":
                                                                    route.External.OutOfHoursInactive = true;
                                                                    break;
                                                                case "+IOOO":
                                                                    route.Internal.OutOfHoursInactive = false;
                                                                    break;
                                                                case "-IOOO":
                                                                    route.Internal.OutOfHoursInactive = true;
                                                                    break;
                                                            }
                                                            if (o.Value == "")
                                                            {
                                                                //just switch activity.
                                                                continue;
                                                            }
                                                            else
                                                            {
                                                                thekey = thekey.Substring(1);
                                                            }
                                                        }
                                                        var data = o.Value.Split('.');
                                                        if (Enum.TryParse(data[0], out DestinationType dt))
                                                        {
                                                            var dest = new DestinationStruct(dt, ps.GetDNByNumber(data[1]), data[2]);
                                                            switch (thekey)
                                                            {
                                                                case "IALL":
                                                                    route.Internal.AllHours = dest;
                                                                    break;
                                                                case "IOOO":
                                                                    route.Internal.OutOfOfficeHours = dest;
                                                                    break;
                                                                case "EALL":
                                                                    route.External.AllHours = dest;
                                                                    break;
                                                                case "EOOO":
                                                                    route.External.OutOfOfficeHours = dest;
                                                                    break;
                                                            }
                                                        }
                                                        else
                                                            throw new ArgumentOutOfRangeException($"Unexpected destination definition{o.Key}<->{o.Value}");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                throw new InvalidOperationException($"Unknown patameter{paramname}={paramvalue}");
                                            }
                                        }
                                        break;
                                }

                            }
                            if (overrideProfileName != null) //desired override profile was not in persistent storage (new extension)
                            {
                                extension.CurrentProfileOverride = extension.FwdProfiles.First(x => x.Name == overrideProfileName);
                                extension.OverrideExpiresAt = overrideExpiresAt;
                            }
                        }
                        var elapsed = sw.Elapsed;
                        Console.WriteLine($"Updating time: {elapsed} ({extensions.Length})");
                        using (var result = extensions.OMSave($"Updating: {string.Join(",", extensions.Select(x => x.Number))}"))
                        {
                            Console.WriteLine($"Transaction {result.Name}");
                        }
                        Console.WriteLine($"Saving time: {sw.Elapsed - elapsed} ({extensions.Length})");
                        Console.WriteLine($"{sw.Elapsed}");

                    }
                    break;
                case "delete":
                    {
                        foreach (var a in nums)
                        {
                            (ps.GetDNByNumber(a) as Extension).Delete();
                            Console.WriteLine($"Deleted Extension {args[2]}");
                        }
                        return;
                    }
                //case "duplicate":
                //    {
                //        var context = new SerializationExtension.Context() { AdjustHandler = (x, y, z, a) => true };

                //        var from = ps.GetDNByNumber(args[2]) as Extension;
                //        //{
                //        //    //complete
                //        //    var fromXML = from.SerializeObject(context, ps.GetTenant(), null);
                //        //    //patch XML if required. Number is required for sure
                //        //    fromXML.Element("Number").Value = args[3];
                //        //    //deserialize object
                //        //    var to = ps.CreateFromXML(fromXML, ps.GetTenant(), context) as Extension;
                //        //    to.Save();
                //        //    foreach (var a in context.repeatList)
                //        //    {
                //        //        a.Repeat(out var state);
                //        //    }

                //        //    to.Delete();

                //        //    context.repeatList.Clear();
                //        //}
                //        //{
                //        //    //ByParts:
                //        //    var fwddata = OMClassSerializationData.Create(typeof(FwdProfile));
                //        //    var ruledata = OMClassSerializationData.Create(typeof(ExtensionRule));
                //        //    var fwdXML = from.FwdProfiles.SerializeObjectArray("FwdProfiles", context);
                //        //    var ruleXML = from.ForwardingRules.SerializeObjectArray("ForwardingRules", context);

                //        //    var to = ps.GetTenant().CreateExtension(args[3]);
                //        //    fwddata.CreateArray(fwdXML, to, context);
                //        //    to.ForwardingRules = ruledata.CreateArray(ruleXML, to, context).Cast<ExtensionRule>().ToArray();
                //        //    to.Save();
                //        //    foreach (var a in context.repeatList)
                //        //    {
                //        //        a.Repeat(out var state);
                //        //    }
                //        //}
                //    }
                //    break;
                case "show":
                    //simply display results
                    if (!nums.Any())
                        nums = ps.GetAll<Extension>().Select(x => x.Number);
                    break;
                default:
                    throw new ArgumentException("Invalid action name");
            }
            //show result
            {
                var sw = Stopwatch.StartNew();
                using (var extensions =
                        (args[1] == "lookupemail" ? emaillookup.Lookup(args[2]).ToArray() : nums.Select(x => ps.GetDNByNumber(x)).Cast<Extension>().ToArray()).GetDisposer())
                {
                    var first = extensions.First(); //exeption is there are no such extension
                    foreach (var extension in extensions)
                    {
                        Console.WriteLine($"Extension - {extension.Number}:");
                        Console.WriteLine($"  RECORDCALLS={extension.RecordCalls}");
                        Console.WriteLine($"  BINDTOMS={extension.DeliverAudio}");
                        Console.WriteLine($"  REINVITES={extension.SupportReinvite}");
                        Console.WriteLine($"  REPLACES={extension.SupportReplaces}");
                        Console.WriteLine($"  SRTP={extension.EnableSRTP}");
                        Console.WriteLine($"  SRTPMODE={extension.SRTPMode}");
                        Console.WriteLine($"  AUTHID={extension.AuthID}");
                        Console.WriteLine($"  AUTHPASS={extension.AuthPassword}");
                        Console.WriteLine($"  ENABLED={extension.Enabled}");
                        Console.WriteLine($"    FIRST_NAME={extension.FirstName}");
                        Console.WriteLine($"    LAST_NAME={extension.LastName}");
                        Console.WriteLine($"    EMAIL={extension.EmailAddress}");
                        Console.WriteLine($"    MOBILE={extension.GetPropertyValue("MOBILENUMBER")}");
                        Console.WriteLine($"    OUTBOUND_CALLER_ID={extension.OutboundCallerID}");
                        Console.WriteLine($"    CURRENT_STATUS={extension.CurrentProfile?.Name}");
                        Console.WriteLine($"    GROUPS={string.Join(",", extension.GroupMembers.Select(x => x.Group.Name))}");
                        Console.WriteLine($"    RIGHTS=\n        {string.Join("\n        ", extension.GroupMembers.Select(x => $"{x.Group.Name}={x.CumulativeRights}(Overrides={x.Overrides})"))}");
                        Console.WriteLine($"    QUEUES=\n        {string.Join("\n        ", extension.QueueMembership.Select(x => x.Queue.Number + "=" + x.QueueStatus.ToString()))}");
                        foreach (var fp in extension.FwdProfiles)
                        {
                            switch (fp.TypeOfRouting)
                            {
                                case RoutingType.Available:
                                    {
                                        var route = fp.AvailableRoute;
                                        var na = route.NoAnswer.AllCalls;
                                        var nai = route.NoAnswer.Internal;
                                        var b = route.Busy.AllCalls;
                                        var bi = route.Busy.Internal;
                                        var nasign = route.NoAnswer.InternalInactive ? "-" : "+";
                                        var bsign = route.Busy.InternalInactive ? "-" : "+";
                                        Console.WriteLine($"    profile.{fp.Name}=AV(NA:{DestToString(na)},{nasign}NAI:{DestToString(nai)},BUSY:{DestToString(b)},{bsign}BUSYI:{DestToString(bi)})");
                                    }
                                    break;
                                case RoutingType.Away:
                                    {
                                        var route = fp.AwayRoute;
                                        var eall = route.External.AllHours;
                                        var eooo = route.External.OutOfOfficeHours;
                                        var iall = route.Internal.AllHours;
                                        var iooo = route.Internal.OutOfOfficeHours;
                                        var eosign = route.External.OutOfHoursInactive ? "-" : "+";
                                        var iosign = route.Internal.OutOfHoursInactive ? "-" : "+";
                                        Console.WriteLine($"    profile.{fp.Name}=AW(IALL:{DestToString(iall)},{iosign}IOOO:{DestToString(iooo)},EALL:{DestToString(eall)},{eosign}EOOO:{DestToString(eooo)})");
                                    }
                                    break;
                                default:
                                    Console.WriteLine($"profile.{fp.Name}=!!!Invalid route type");
                                    break;
                            }
                        }
                        Console.WriteLine("    DNProperties:");
                        foreach (var p in extension.GetProperties())
                        {
                            var name = p.Name;
                            var value = p.Value.Length > 50 ? new string(p.Value.Take(50).ToArray()) + "..." : p.Value;
                            Console.WriteLine($"        prop.{name}={value}");
                        }
                    }
                }
                Console.WriteLine($"Show time:{sw.Elapsed}");
                Console.WriteLine(string.Join("\n", ps.GetCacheHealthReport().Select(x => $"{x.Key}={x.Value}")));
            }
        }

        private string CheckRandom(string value)
        {
            var randkey = "RAND(";
            var randkeyend = ")";
            if (!value.StartsWith(randkey) || !value.EndsWith(randkeyend))
                return value;
            var randparams = value.Substring(randkey.Length, value.Length - randkey.Length - randkeyend.Length).Split(',');
            ushort min = ushort.Parse(randparams[0]);
            ushort max = min;
            if (randparams.Length>1)
            {
                 max = ushort.Parse(randparams[1]);
            }
            return getRandomString(min, max);
        }
    }
}
