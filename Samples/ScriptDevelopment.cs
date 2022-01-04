using System;
using System.Linq;
using TCX.Configuration;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OMSamples.Samples
{
    [SampleCode("scriptdev")]
    [SampleDescription("Shows how to make simple verification of the prepared script. Check deploy as RoutePoint and make test call.")]
    [SampleWarning("Some actions of this sample may override existing configuration. DON'T use it on production environment")]
    [SampleParam("arg1", "deployandcall|testcall    |check             |showfailed|online")]
    [SampleParam("arg2", "RoutePointDN |RoutePointDN|[RoutePointDN ...]|          |      ")]
    [SampleParam("arg3" ,"filename     |toDN        |                  |          |      ")]
    class ScriptDevelopment : ISample
    {
        void UpdateOrCreate(PhoneSystem ps, string CallFlowName, string filename)
        {
            var scriptname = $"{CallFlowName}.{Path.GetFileNameWithoutExtension(filename)}".TrimEnd('.').Replace('%', '*');
            try
            {
                var rp = ps.GetDNByNumber(scriptname) as RoutePoint ?? ps.GetTenant().CreateRoutePoint(scriptname, File.ReadAllText(filename));
                if (rp.ID != 0)
                {
                    if (rp.GetPropertyValue("TEST_DEPLOYMENT") != "1")
                    {
                        Console.WriteLine($"CallFlow route point {CallFlowName} is not configured for test purposes (dn property TEST_DEPLOYMENT is not set to 1)");
                        return;
                    }
                    var toApply = File.ReadAllText(filename);
                    if (rp.ScriptCode == toApply && rp.GetPropertyValue("COMPILATION_SUCCEEDED") != "1")
                    {
                        if (toApply.EndsWith(" "))
                            toApply = toApply.Substring(0, toApply.Length - 1);
                        else
                            toApply += " ";
                    }
                    rp.ScriptCode = toApply;
                }
                else
                    rp.SetProperty("TEST_DEPLOYMENT", "1");
                rp.Save();
                Console.WriteLine($"{scriptname} has been updated");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{scriptname} update failed\n{ex}");
            }
        }

        public void Run(PhoneSystem ps, params string[] args)
        {
            switch (args[1])
            {
                //tries to create callflow routing points according to folder content.
                //runs FileSystem watcher which synchronize filesystem content with 3CX configuration
                //runs 3CX Configuration listener which provides reports wach time when CallFlow route points created by this procedure
                //will be updated
                case "online":
                    {
                        var CallFlowName = Path.GetFileName(args[2]);
                        Console.WriteLine($"Deploy {CallFlowName}");
                        var RoutePointTrack = new PsTypeEventListener<RoutePoint>("DN");
                        ///each time when RoutePoints created from the folder will be updated by the server
                        ///Full state of the script will be shown with error/warnings report and script code will be printed with "highlights" of the
                        ///problematic parts. (yellow - warning, red - error, darkgray - hidden warnings)
                        RoutePointTrack.SetTypeHandler(x => ShowState(x), x => ShowState(x), (x, y) => Deleted(x, y), x => x.Number.StartsWith($"{CallFlowName}.")|| x.Number== CallFlowName);
                        if (Directory.EnumerateFiles(args[2], "*.cs")
                            .All(filename => { UpdateOrCreate(ps, CallFlowName, filename); return true; }))
                        {
                            ///start filesystem whatcher which will delete/create/update callflow scripts according to the folder changes.
                            FileSystemWatcher mywatch = new FileSystemWatcher(args[2], "*.cs");
                            mywatch.NotifyFilter = NotifyFilters.LastWrite;
                            mywatch.EnableRaisingEvents = true;
                            mywatch.Changed += (x, y) => Task.Delay(1000).ContinueWith(_=>UpdateOrCreate(ps, CallFlowName, y.FullPath));
                            mywatch.Created += (x, y) => Task.Delay(1000).ContinueWith(_ => UpdateOrCreate(ps, CallFlowName, y.FullPath));
                            mywatch.Deleted += (x, y) =>
                            {
                                //In this example, folder name specifies DN name of the RoutePoint.
                                //as far as foldername does not allow *, we use % as a placeholder of *
                                //so if foldername is %90 - *90 Routepoint will be created.
                                if (ps.GetDNByNumber($"{CallFlowName}.{Path.GetFileNameWithoutExtension(y.Name)}".TrimEnd('.').Replace('%', '*')) is RoutePoint rp)
                                {
                                    if (rp.GetPropertyValue("TEST_DEPLOYMENT") == "1")
                                    {
                                        rp.Delete();
                                        Console.WriteLine($"{rp} removed");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"CallFlow route point {CallFlowName} is not configured for test purposes (dn property TEST_DEPLOYMENT is not set to 1). Ignore deleting of files");
                                    }
                                }
                            };
                            while (!Program.Stop)
                            {
                                ///we are running infinite loop here. meanwhile, RoutePointTrack and FileSystem whatcher will do theirs work.
                                Thread.Sleep(1000);
                            }
                        }
                    }
                    break;
                //simple way to create callflow routepoint and upload script from the file
                case "deploy":
                    {
                        using (var testRP = (ps.GetDNByNumber(args[2]) as RoutePoint) ?? ps.GetTenant().CreateRoutePoint(args[2], File.ReadAllText(args[3])))
                        {
                            if (testRP.ID == 0)
                                testRP.SetProperty("TEST_DEPLOYMENT", "1");
                            if(testRP.GetPropertyValue("TEST_DEPLOYMENT") == "1")
                            {
                                if (testRP.ID != 0)
                                    testRP.ScriptCode = File.ReadAllText(args[3]);
                                testRP.Save();
                            }
                            else
                            {
                                Console.WriteLine($"RoutePoint {args[2]} is not configured for test deployment (dn property TEST_DEPLOYMENT is not set to 1)");
                            }
                        }
                    }
                    break;
                //the source must have single registered device.
                case "testcall":
                    {
                        ps.MakeCall(ps.GetDNByNumber(args[2]).GetRegistrarContactsEx().Single(), args[3]);
                    }
                    break;
                //just prints full state of the requested CallFlow scripts deployed on server.
                case "check":
                    {
                        using (var rps = ((args.Length < 3) ? ps.GetRoutePoints() :
                            args.Skip(2).SelectMany(
                                x =>
                                {
                                    var rp = ps.GetDNByNumber(x) as RoutePoint;
                                    if (rp != null)
                                        return new[] { rp };
                                    else
                                        return ps.GetRoutePoints().GetDisposer(y=>y.Number.StartsWith(x)).ToArray();
                                }).Distinct().ToArray()).GetDisposer())
                        {
                            foreach (var rp in rps)
                            {
                                ShowState(rp);
                            }
                        }
                    }
                    break;
                //allows to delete CallFlow script which was deployed using this OM sample
                case "delete":
                    {
                        using (var rp = ps.GetDNByNumber(args[2]) as RoutePoint)
                        {
                            if(rp==null)
                                Console.WriteLine($"RoutePoint {args[2]} does not exist");
                            else if (rp.GetPropertyValue("TEST_DEPLOYMENT") != "1")
                                Console.WriteLine($"RoutePoint {args[2]} is not marked for test deployment script (TEST_DEPLOYMENT!=1)");
                            else
                            {
                                Console.Write($"Are you sure want to delete {args[2]}(y/N)?");
                                if (Console.ReadLine().ToUpperInvariant() == "Y")
                                    rp.Delete();
                                else
                                {
                                    Console.WriteLine($"Cancelled");
                                }
                            }
                        }
                    }
                    break;
                //shows all problematic CallFlow scripts where last try to update/compile script was not successful.
                case "showfailed":
                    using (var rps = ((args.Length < 3) ? ps.GetRoutePoints() :
                        args.Skip(2).SelectMany(
                            x =>
                            {
                                var rp = ps.GetDNByNumber(x) as RoutePoint;
                                if (rp != null)
                                    return new[] { rp };
                                else
                                    return ps.GetRoutePoints().GetDisposer(y => y.Number.StartsWith(x)).ToArray();
                            }).Distinct()).Where(x => x.GetPropertyValue("INVALID_SCRIPT") == "1").ToArray().GetDisposer())
                    {
                        foreach (var rp in rps)
                        {
                            ShowState(rp);
                        }
                    }
                    break;
                default:
                    throw new InvalidOperationException($"{args[1]} - action is not defined");
            }
        }

        private void Deleted(RoutePoint x, int id)
        {
            Console.WriteLine($"Deleted {id}");
        }

        struct ColoredSpan
        {
            public ConsoleColor color;
            public KeyValuePair<int, int> location;
        }
        void ShowCompilationResult(string code, string Report)
        {
            var foreground = Console.ForegroundColor;
            try
            {
                var allspans = Report.Split('\n').Where(x => x.StartsWith(":["))
                    .Select(x => x.Split(':', StringSplitOptions.RemoveEmptyEntries))
                    .Select(x => x[0].Last() == ')' ? (x[0] + x[2].Trim().First()) : x[0]) //add 'E' if not specified
                    .Select(
                        x => 
                            new string(x.Skip(1).Take(x.Length - 3).ToArray()).Split('.', StringSplitOptions.RemoveEmptyEntries)
                            .Append(new string(x.Last(), 1)).ToArray())
                    .Select(x => new ColoredSpan()
                    {
                        location = KeyValuePair.Create(int.Parse(x[0]), int.Parse(x[1])),
                        color = x[2] == "H" ? ConsoleColor.DarkGray : x[2] == "W" ? ConsoleColor.Yellow : x[2] == "E" ? ConsoleColor.Red : foreground
                    }).Append(new ColoredSpan() { color = foreground, location = KeyValuePair.Create(code.Length + 1, code.Length + 1) }).OrderBy(x => x.location.Key).ThenBy(x => x.location.Value).ToArray();
                int currentlocation = 1;
                foreach (var span in allspans)
                {
                    if (currentlocation < span.location.Key)
                    {
                        Console.ForegroundColor = foreground;
                        Console.Write(new string(code.Skip(currentlocation - 1).Take(span.location.Key - currentlocation).ToArray()));
                        currentlocation += span.location.Key - currentlocation;
                    }
                    if (currentlocation == span.location.Key)
                    {
                        Console.ForegroundColor = span.color;
                        Console.Write(new string(code.Skip(currentlocation - 1).Take(span.location.Value - span.location.Key).ToArray()));
                        currentlocation += span.location.Value - span.location.Key;
                    }
                    if (currentlocation < span.location.Value)
                    {
                        Console.ForegroundColor = span.color;
                        Console.Write(new string(code.Skip(currentlocation-1).Take(span.location.Value - currentlocation).ToArray()));
                        currentlocation += span.location.Value - currentlocation;
                    }
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Malformed Report {Report}");
            }
            finally
            {
                Console.ForegroundColor = foreground;
            }
        }
        private void ShowState(RoutePoint rp)
        {
            Console.WriteLine($"------ Report for {rp}--------");
            var foreground = Console.ForegroundColor;
            try
            {
                var rejected_code = rp.GetPropertyValue("REJECTED_CODE");
                var compilation_result = rp.GetPropertyValue("COMPILATION_RESULT");
                bool compilation_succeeded = rp.GetPropertyValue("COMPILATION_SUCCEEDED") == "1";
                bool invalid_script = rp.GetPropertyValue("INVALID_SCRIPT") == "1";
                if (compilation_succeeded)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Code successfully applied");
                }
                else
                {
                    if (invalid_script)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Route point code compilation failed.");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("RoutePoint works with old code. Last try tp update code has been rejected:");
                    }
                }
                Console.ForegroundColor = foreground;
                Console.WriteLine($"\nCompilation result:\n{compilation_result}");
                ShowCompilationResult(compilation_succeeded?rp.ScriptCode:rejected_code, compilation_result);
                Console.WriteLine($"------ End of Report for {rp}--------");
            }
            finally
            {
                Console.ForegroundColor = foreground;
            }
        }
            
    }
}
