using System;
using System.Collections.Generic;
using System.Text;

namespace JobsGeParser
{
    public class JobApplication
    {
        private string _link;
        private string _companyLink;
        private int _id;
        private string _name;
        private string _company;
        private DateTime _published;
        private DateTime _endDate;

        public int Id 
        { 
            get => _id; 
            set => _id = value; 
        }
        
        public string Name 
        { 
            get => _name; 
            set => _name = value; 
        }
        
        public string Link
        {
            get => _link;

            set => _link = string.Concat(Constants.BaseAddress, value);
        }
        
        public string Company 
        { 
            get => _company; 
            set => _company = value; 
        }
        
        public string CompanyLink
        {
            get => _companyLink;
            set => _companyLink = string.Concat(Constants.BaseAddress, value);
        }

        public DateTime Published 
        { 
            get => _published; 
            set => _published = value; 
        }

        public DateTime EndDate 
        { 
            get => _endDate; 
            set => _endDate = value; 
        }

        private string _description;

        public string Description
        {
            get { return _description; }
            set { _description = value; }
        }

    }
}
