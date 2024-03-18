using System.Collections.Generic;
using System.Linq;

namespace JobsGeParser;

public class Repo
{
	private readonly List<JobApplication> _applications = new List<JobApplication>();

	public void Save(JobApplication applications) =>
		_applications.Add(applications);

	public IEnumerable<JobApplication> GetProcessedApplications() =>
		_applications;

	public IEnumerable<JobApplication> ListDotnetApplications() =>
		_applications.Where(a => a.Name.ToLower().Contains(".net"));
}