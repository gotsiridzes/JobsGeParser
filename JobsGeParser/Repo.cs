using System.Collections.Generic;

namespace JobsGeParser;

public class Repo
{
    private List<JobApplication> _applications = new List<JobApplication>();

    public void Save(JobApplication applications) =>
        _applications.Add(applications);

    public IEnumerable<JobApplication> GetProcessedApplications() =>
        _applications;
}