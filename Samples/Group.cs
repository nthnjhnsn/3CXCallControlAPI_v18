using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCX.Configuration;
using TCX.Configuration.CommonRightsCache;
using System.Reflection;
using System.Xml.Serialization;
using System.Threading;
using System.Diagnostics;

namespace OMSamples.Samples
{
    namespace OMSamples.Samples
    {
        [SampleCode("groups")]
        [SampleParam("arg1..argN", "[group=groupname [dns=[[+-]dnnumber[,...]] [role=rolename] [{<right>=[true|false]}]]")]
        [SampleDescription("Shows how to set or update rights.\n"
            +"Supports bulk updates when dns parameter is specified. dns support * as placeholder of all gorup memebers\n"
            +"Examples:\n"
            +"groups group=<groupname> dns=* role=users \n"
            +"    sets users role to all dns"
            + "groups group=<groupname> dns=101,+100,-200 ......\n"
            + "   100 will be added with specified role and overrides. if 100 already exists + is ignored\n"
            + "   200 will be removed. \n"
            + "   101 overrides will be joined with existing and role will be set to the requested value\n"
            + "NOTE: if role is specified - it will be overwriten for all mentioned extensions. if role is not specified - new members will be created with users role\n"
            + "      if members are not specified - the specifed role rights will be updated. tole is mandatory in this case\n"
            + "      The described limitations are the limitations of this sample implementation. PBX allows to make any modifications with group update\n"

            )]
        class GroupSample : ISample
        {
            HashSet<string> nonrightsparameters = new HashSet<string> { "role", "dns", "group" };
            public void Run(PhoneSystem ps, params string[] args_in)
            {
                var parameters = args_in.Skip(1).Select(x => x.Split('=')).ToDictionary(x => x[0], x => string.Join("=", x.Skip(1)));
                Group grp = ps.GetGroupByName(parameters.TryGetValue("group", out var grpname) ? grpname : ""); //will be null isf not specified

                RightsDescriptor tosetrights = new RightsDescriptor();

                var fields = typeof(RightsDescriptor).GetFields().Where(x => !x.GetCustomAttributes().Any()).ToDictionary(x => x.Name, x => x);
                if (parameters.TryGetValue("role", out var arole) || parameters.Any(x => !nonrightsparameters.Contains(x.Key)))
                {
                    var toset = (object)new RightsDescriptor(arole);//can be null - means override only existing 
                    foreach (var p in parameters.Where(x => !nonrightsparameters.Contains(x.Key)))
                    {
                        if (!string.IsNullOrEmpty(p.Value))
                        {
                            Console.WriteLine($"{p.Key} will be set to {p.Value}");
                            fields[p.Key].SetValue(toset, bool.Parse(p.Value));
                        }
                        else
                        {
                            Console.WriteLine($"{fields[p.Key].Name} will be reset");
                        }
                    }
                    tosetrights = (RightsDescriptor)toset;
                    if(tosetrights.rolename == null)
                    {
                        Console.WriteLine($"role is not specified. Only update/delete operations on members can be applied");
                    }
                }
                var existingMembers = grp?.GroupMembers.ToDictionary(x => x.DNRef.ID); //we use it to build "group members update below"

                parameters.TryGetValue("dns", out var dnnumber);
                if (dnnumber != null)
                {
                    bool hasupdates = false;
                    //group can be modified by preparing the new group member array and assigning it to grp.GroupMembers.
                    //to do this = take grp.GroupMembers array and build new collection of the members which does not contain removed members,
                    //append new GroupMember objects created by grp.CreateGroupMember
                    //and then assign array of the collected object back to the grp.GroupMembers
                    //
                    //Here we use other technique to updated group:
                    //grp object is simply instructed to generate additional actions to the objects attached to it
                    //thereis also another way to make update using IOMTransaction PhoneSystem.CreateBatch(nname of the transaction);
                    using (var test_transaction = ps.CreateBatch("TestTransaction")) //very similar to AttachOnSave/DeleteOnSave
                    {
                        if (dnnumber == "*")
                            dnnumber = string.Join(",", grp.GroupMembers.Select(x => x.DNRef.Number));
                        else if (dnnumber == "-*")
                            dnnumber = string.Join(",", grp.GroupMembers.Select(x => $"-{x.DNRef.Number}"));
                        foreach (var dnn in dnnumber.Split(new[] { ',' }))
                        {
                            var thedn = ps.GetDNByNumber(dnn.TrimStart('-', '+'));
                            existingMembers.TryGetValue(thedn.ID, out var member);
                            switch (dnn.StartsWith("+") ? UpdateOperation.Inserted : dnn.StartsWith("-") ? UpdateOperation.Deleted : UpdateOperation.Updated)
                            {
                                case UpdateOperation.Inserted:
                                    if (member == null) //needs insert
                                        member = grp.CreateGroupMember(thedn, tosetrights.GetCombinedWithOverwrite(new RightsDescriptor(parameters["role"], null)).ToString());
                                    goto case UpdateOperation.Updated;
                                case UpdateOperation.Updated:
                                    {
                                        if (member == null)
                                        {
                                            Console.WriteLine($"{dnn} member is not found");
                                        }
                                        else
                                        {
                                            var isnew = member.ID == 0;
                                            var resultingrights = (isnew ? new RightsDescriptor("users") : member.Overrides).GetCombinedWithOverwrite(tosetrights, null);
                                            if (isnew || !member.Overrides.Equals(resultingrights))
                                            {
                                                member.RoleTag = resultingrights.ToString();
                                                Console.WriteLine((isnew ? "to create" : "to update") + $" - {member} with overrides \n{resultingrights}");
                                                grp.AttachOnSave(member);
                                                test_transaction.AppendUpdate(member);
                                                hasupdates = true;
                                            }
                                            else
                                            {
                                                Console.WriteLine($"No updates required for {member}");
                                            }
                                        }
                                    }
                                    break;
                                case UpdateOperation.Deleted:
                                    {
                                        Console.WriteLine($"to remove - {member}");
                                        grp.DeleteOnSave(member);
                                        test_transaction.AppendDelete(member);
                                        hasupdates = true;
                                    }
                                    break;
                            }
                        }
                        //The same as below. If we need to continue with updated rights cache we need to wait until group update will come
                        //here the demostration how to use PsTypeEventListener to wait for the specific update
                        var restorecolor = Console.ForegroundColor;
                        try
                        {
                            if (hasupdates)
                            {
                                //lets report how much time it has take to upply update
                                using (var listener = new PsTypeEventListener<Group>())
                                {
                                    lock (listener)
                                    {
                                        listener.SetTypeHandler(
                                        x =>
                                        {
                                            lock (listener)
                                                Monitor.Pulse(listener);
                                        },
                                        null, //not required in our case
                                        null, //not required in our case
                                        x => x.ID == grp.ID, //only our object
                                        null //not required in our case.
                                        );
                                        //we prepared both variants, so call either
                                        grp.Save();
                                        //OR
                                        //test_transaction.Commit();
                                        //and wait for update will be ready
                                        if (!Monitor.Wait(listener, TimeSpan.FromSeconds(5)))
                                        {
                                            Console.ForegroundColor = ConsoleColor.Red;
                                            Console.WriteLine($"Object was not updated in expected time!!!");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"No updates are required");
                            }
                        }
                        finally
                        {
                            Console.ForegroundColor = restorecolor;
                        }
                    }
                }
                else if (grp != null && parameters.Any(x=>!nonrightsparameters.Contains(x.Key)))
                {
                    var role_rights_descriptor = ps.GetRights(grp, parameters["role"]); //cached group rights (last received update)
                                                                                        //we can use data stored in Group object snapshot like here
                                                                                        //Assert may be triggered in case of object is not properly configured, or if group rights is updated after Group snapshot is taken
                    var roles = grp.Roles;

                    var isValid = RightsDescriptor.TryParse(roles.First(x => x.Name == parameters["role"]).RightsData, out var parsing_role_rights);
                    if (isValid)
                    {
                        try
                        {
                            var parsed_withot_check = RightsDescriptor.Parse(roles.First(x => x.Name == parameters["role"]).RightsData);
                            System.Diagnostics.Debug.Assert(parsed_withot_check.Equals(parsing_role_rights));
                            System.Diagnostics.Debug.Assert(parsed_withot_check.Equals(role_rights_descriptor));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"RightsDescriptor.Parse failed for {parameters["role"]} - {ex}");
                        }
                    }
                    RightsDescriptor newRights = new RightsDescriptor(parameters["role"]);
                    //we use lastknow rights for modification.
                    //we must find it because it checked above by Linq.First()
                    for (var i = 0; i < roles.Length; i++)
                    {
                        if (roles[i].Name == parameters["role"])
                        {
                            newRights = role_rights_descriptor.GetCombinedWithOverwrite(tosetrights);
                            Console.WriteLine($"{roles[i].Name}@{grp} is updated to \n{newRights}");
                            roles[i].RightsData = newRights.ToString();
                        }
                    }
                    //here we simply save the results
                    //grp.Save() is returned when new data will arrive but before the update events will be processed.
                    //It may cause race when rights cache is not yet updated and PhoneSystem.GetRights/GroupMember.Overrides/CumulativeRights will still provide
                    //previous value.
                    //if we need to work with rights cache right after save then
                    var restorecolor = Console.ForegroundColor;
                    try
                    {
                        //lets report how much time it has take to upply update
                        using (var listener = new PsTypeEventListener<Group>())
                        {
                            lock (listener)
                            {
                                listener.SetTypeHandler(
                                x =>
                                {
                                    lock (listener)
                                        Monitor.Pulse(listener);
                                },
                                null, //not required in our case
                                null, //not required in our case
                                x => x.ID == grp.ID, //only our object
                                null //not required in our case.
                                );
                                //we prepared both variants, so call either
                                grp.Save();
                                if (!Monitor.Wait(listener, TimeSpan.FromSeconds(5)))
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"Object was not updated in expected time!!!");
                                }
                                var cached_after_update = ps.GetRights(grp, parameters["role"]);
                                System.Diagnostics.Debug.Assert(newRights.Equals(cached_after_update));
                                if (!newRights.Equals(cached_after_update))
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"Cache is not updated properly:\nEcpected: {newRights}\n Cached after Update:\n{cached_after_update}");
                                }
                            }
                        }
                    }
                    finally
                    {
                        Console.ForegroundColor = restorecolor;
                    }
                }
                //now show the result.
                //in case if group is not be specified - all groups will be printed out
                var defcolor = Console.ForegroundColor;
                try
                {
                    //displayresult
                    foreach (var g in grp != null ? new[] { grp } : ps.GetAll<Group>())
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"{g}");
                        foreach (var r in g.Roles.Where(x => !parameters.TryGetValue("role", out var therole) || x.Name == therole))
                        {
                            Console.WriteLine($"{r.Name}:{r.RightsData}");
                        }
                        //show only specified role.
                        foreach (var m in g.GroupMembers.Where(x=>!parameters.TryGetValue("role", out var therole) || x.Overrides.rolename==therole))
                        {
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine($"{m}");
                        }
                    }
                }
                finally
                {
                    Console.ForegroundColor = defcolor;
                }
            }
        }
    }
}
