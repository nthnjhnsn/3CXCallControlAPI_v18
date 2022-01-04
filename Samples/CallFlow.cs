using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TCX.Configuration;

namespace OMSamples.Samples
{
    [SampleCode("callflow")]
    [SampleParam("arg1", "list|create|delete|update|fromfolder")]
    [SampleParam("arg2...argN", "[prefix=name_prefix|name=RoutePointName [script=<filename>][force=true]]")]
    [SampleDescription("prints, updates, creates, deletes callflow script route points. fromfolder creates callflow routing points basing on ralative path of .cs files")]
    class RoutePointSample: ISample
    {
        public void Run(PhoneSystem ps, params string[] args_in)
        {
            var action = args_in.Skip(1).FirstOrDefault()??"list"; //second parameter is action. list if not specified
            var parameters = args_in.Skip(2).Select(x => x.Split('=')).ToDictionary(x => x[0], x => string.Join("=", x.Skip(1)));
            switch (action)
            {
                case "create":
                    {
                        var newrp = ps.GetTenant().CreateRoutePoint(parameters["name"], File.ReadAllText(parameters["script"]));
                        newrp.SetProperty("TEST_DEPLOYMENT", "1");
                        newrp.Save();
                    }
                    break;
                case "update":
                    {
                        var rp = ps.GetDNByNumber(parameters["name"]) as RoutePoint;
                        if (rp?.GetPropertyValue("TEST_DEPLOYMENT") == "1")
                        {
                            rp.ScriptCode = File.ReadAllText(parameters["script"]);
                            rp.Save();
                        }
                        else
                        {
                            Console.WriteLine("'{0}' cannot be updated because it {1}", parameters["name"],
                                rp == null ? "does not exist" : "is not marked for test deployment"
                            );

                        }
                    }
                    break;
                case "delete":
                    if (parameters.TryGetValue("name", out var thename))
                    {
                        if (ps.GetDNByNumber(thename) is RoutePoint rp && rp.GetPropertyValue("TEST_DEPLOYMENT") == "1")
                        {
                            rp.Delete();
                            Console.WriteLine($"Removed '{thename}'");
                        }
                        else
                        {
                            Console.WriteLine($"Not found '{thename}' - either does not exist or not marked for test deployment");
                        }
                    }
                    else if (parameters.TryGetValue("prefix", out var prefix))
                    {
                        var forced = parameters.TryGetValue("force", out var force);
                        using (var rps = ps.GetAll<RoutePoint>().GetDisposer(x => x.Number.StartsWith(prefix) && x.GetPropertyValue("TEST_DEPLOYMENT") == "1"))
                            foreach (var rp in rps)
                            {
                                Console.Write($"Do you want to delete {rp.Number} (y/N)?");
                                string answer = null;
                                if (!forced) //if forced is not specified ask for confirmation
                                    answer = Console.ReadLine();
                                else
                                {
                                    answer = "y";
                                    Console.WriteLine("y");
                                }
                                if (answer == "y" || answer == "Y")
                                {
                                    rp.Delete();
                                }
                            }
                    }
                    else
                    {
                        Console.WriteLine("'delete' action requires prefix or name. Empty prefix 'prefix=' can beused to delete all CallFlows (RoutePoints)");
                    }
                    break;
                case "fromfolder":
                    {
                        var rootfolder = parameters["name"];
                        var Directories = new[] { rootfolder }.Concat(Directory.EnumerateDirectories(parameters["name"]));
                        var forced = parameters.TryGetValue("force", out var force);
                        foreach (var dirname in Directories)
                        {
                            Console.WriteLine($"{dirname}");
                            var namePrefix = "";
                            if (dirname != rootfolder)
                            {
                                namePrefix = Path.GetFileName(dirname);
                            }

                            foreach (var filename in Directory.EnumerateFiles(dirname, "*.cs"))
                            {
                                var scriptname = $"{namePrefix}.{Path.GetFileNameWithoutExtension(filename)}".TrimEnd('.').Replace('%', '*');
                                Console.WriteLine($"{scriptname}<-{filename}");
                                try
                                {
                                    using (var rp = ps.GetDNByNumber(scriptname) as RoutePoint ?? ps.GetTenant().CreateRoutePoint(scriptname, File.ReadAllText(filename)))
                                    {
                                        if (rp.ID != 0)
                                        {
                                            if(rp.GetPropertyValue("TEST_DEPLOYMENT") == "1")
                                            {
                                                rp.ScriptCode = File.ReadAllText(filename);
                                            }
                                            else
                                            {
                                                Console.WriteLine($"Existing '{rp.Number}' is not marked for test deployment");
                                                continue;
                                            }
                                        }
                                        if (rp.ID == 0)
                                            rp.SetProperty("TEST_DEPLOYMENT", "1");
                                        if (!forced)
                                        {
                                            Console.Write("Are you sure want to {0} '{1}' from {2} (y/N)?", rp.ID == 0 ? "create" : "update", rp.Number, filename);
                                            if (Console.ReadLine().ToLowerInvariant() != "y")
                                                continue;
                                        }
                                        rp.Save();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"{ex}");
                                }
                            }
                        }
                    }
                    break;
                case "list":
                    {
                        parameters.TryGetValue("prefix", out var prefix);
                        using (var rps = ps.GetAll<RoutePoint>().GetDisposer(x => prefix == null || x.Number.StartsWith(prefix)))
                            foreach (var a in rps)
                            {
                                Console.WriteLine("{0}{1}", a.GetPropertyValue("TEST_DEPLOYMENT")=="1"?"TEST":"    ",  a);
                                Console.WriteLine($"    {string.Join("\n    ", a.GetProperties().Select(x => $"{x.Name}={new string(x.Value.Take(50).ToArray())}").ToArray())}");
                            }
                    }
                    break;
                default:
                    throw new Exception($"Undefined action {action}");
            }
        }
    }
}
