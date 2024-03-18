# JobsGeParser Description

## Description

 This is a simple parser for the website [jobs.ge](https://jobs.ge/). 
 It is written in C# and uses the `htmlagilitypack` and .net libraries to parse the website and extract the job listings for IT.

 ## Usage
 there are three http methods that can be used to get the job listings.

 1. api/jobs/retrieve - this method will retrieve the job listings from the website.
 The job listings are stored in the database after they are retrieved from the website.
 
 2. api/jobs/ - this method will retrieve the job listings from the database (in this case in-memory db/list).  

 3. api/jobs/dotnet - this method will retrieve the job listings which contain .net in the title.