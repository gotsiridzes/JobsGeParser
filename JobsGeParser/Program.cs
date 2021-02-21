using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace JobsGeParser
{
    class Program
    {
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            var jobsClient = new JobsGeClient();
            Console.ForegroundColor = ConsoleColor.White;
            var jobs = await jobsClient.GetJobApplicationsAsync();
            Console.WriteLine("Got all jobs.");

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
