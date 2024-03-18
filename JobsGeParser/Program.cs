using JobsGeParser;

var jobsClient = new JobsGeClient();
Console.ForegroundColor = ConsoleColor.White;
var jobs = await jobsClient.GetJobApplicationsAsync();
Console.WriteLine("Got all jobs.");

Console.WriteLine("Press any key to continue...");
Console.ReadKey();