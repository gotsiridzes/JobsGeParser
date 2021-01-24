using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace JobsGeParser
{
    public class Repository : IRepository<JobApplication>
    {

        public async Task Delete(JobApplication entity)
        {
            using (var connection = GetConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "dbo.DeleteJob";
                    command.CommandType = System.Data.CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@Id", entity.Id);
                    await connection.OpenAsync();

                    await command.ExecuteNonQueryAsync();
                }
            }

        }

        public Task<JobApplication> Get(int id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<JobApplication>> GetAll()
        {
            throw new NotImplementedException();
        }

        public async Task Insert(JobApplication entity)
        {
            using (var connection = GetConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "[Job].[InsertJob]";
                    command.CommandType = System.Data.CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@Id", entity.Id);
                    command.Parameters.AddWithValue("@Link", entity.Link);
                    command.Parameters.AddWithValue("@Name", entity.Name);
                    command.Parameters.AddWithValue("@Company", entity.Company);
                    command.Parameters.AddWithValue("@CompanyLink", entity.CompanyLink);
                    command.Parameters.AddWithValue("Published", entity.Published);
                    command.Parameters.AddWithValue("@EndDate", entity.EndDate);
                    command.Parameters.AddWithValue("@Description", entity.Description);

                    await connection.OpenAsync();

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            await command.ExecuteNonQueryAsync();
                            await transaction.CommitAsync();
                        }
                        catch(Exception)
                        {
                            await transaction.RollbackAsync();
                        }
                    }

                }
            }
        }

        public Task Update(JobApplication entity)
        {
            throw new NotImplementedException();
        }

        private static SqlConnection GetConnection()
        {
            return new SqlConnection(Constants.ConnectionString);
        }


    }
}
