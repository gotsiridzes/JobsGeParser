using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JobsGeParser
{
    public interface IRepository<T>
    {
        Task Insert(T entity);
        Task Delete(T entity);
        Task Update(T entity);
        Task<T> Get(int id);
        Task<IEnumerable<T>> GetAll();
        Task CheckJobs(JobApplication job);
    }
}
