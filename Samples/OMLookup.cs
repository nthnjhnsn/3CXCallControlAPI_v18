using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCX.Configuration;

namespace OMSamples.Samples
{
    [SampleCode("omlookup")]
    [SampleDescription("Shows how to use OMLookup to make custom lookup collection of objects. sample is for Extension.EmailAddress")]
    class OMLookup : ISample
    {
        public void Run(PhoneSystem ps, params string[] args)
        {
            var emaillookup = ps.CreateLookup(() => ps.GetExtensions(), y => y.EmailAddress, "DN");
            while (!Program.Stop)
            {
                Console.WriteLine("s <string> - search for email");
                Console.WriteLine("keys - print all keys with information how many objects are corresponding keys");
                Console.Write("Enter cammand:");
                var command = Console.ReadLine();
                if(command.StartsWith("s "))
                {
                    foreach (var k in emaillookup.Lookup(string.Join(" ", command.Split(' ').Skip(1))))
                    {
                        Console.WriteLine($"{k}");
                    }
                }
                else if(command == "keys")
                {
                    foreach (var k in emaillookup.Keys)
                    {
                        Console.WriteLine($"'{k}'={emaillookup.Lookup(k).Count()}");
                    }
                }

            }
        }

    }
}
