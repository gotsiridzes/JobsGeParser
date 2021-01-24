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
            var jobs = await jobsClient.GetJobApplicationsAsync();
            var repo = new Repository();

            foreach (var item in jobs)
            {
                await repo.Insert(item);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }
    }
}
