namespace JobsGeParser;

public class JobApplication
{
    public JobApplication(
        int id,
        string name,
        string company,
        DateOnly published,
        DateOnly endDate)
    {
        Id = id;
        Name = name;
        Company = company;
        Published = published;
        EndDate = endDate;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    public string Link { get; private set; }

    public string Company { get; private set; }

    public string CompanyLink { get; private set; }

    public DateOnly Published { get; private set; }

    public DateOnly EndDate { get; private set; }

    public string? Description { get; private set; }

    public void SetDescription(string description) => Description = description;

    public void SetLink(string link) => Link = link;

    public void SetCompanyLink(string companyLink) => CompanyLink = companyLink;
}