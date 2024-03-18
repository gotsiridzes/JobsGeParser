using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace JobsGeParser;

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
				command.CommandTimeout = 0;

				command.Parameters.AddWithValue("@Id", entity.Id);
				command.Parameters.AddWithValue("@Link", entity.Link);
				command.Parameters.AddWithValue("@Name", entity.Name);
				command.Parameters.AddWithValue("@Company", entity.Company);
				command.Parameters.AddWithValue("@CompanyLink", entity.CompanyLink);
				command.Parameters.AddWithValue("Published", entity.Published);
				command.Parameters.AddWithValue("@EndDate", entity.EndDate);
				command.Parameters.AddWithValue("@Description", entity.Description);
				await TryOpenConnectionAsync(connection);

				using (var transaction = await connection.BeginTransactionAsync() as SqlTransaction)
				{
					try
					{
						command.Transaction = transaction;
						await command.ExecuteNonQueryAsync();
						await transaction!.CommitAsync();
					}
					catch (Exception)
					{
						await transaction!.RollbackAsync();
						throw;
					}
				}
			}
		}
	}

	private static async Task TryOpenConnectionAsync(SqlConnection connection)
	{
		try
		{
			await connection.OpenAsync();
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Connected!!!");
		}
		catch (Exception ex)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("Can not connect to server, trying again ...\n\t{0}", ex.Message);
			await Task.Delay(5000);
			await TryOpenConnectionAsync(connection);
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

	public async Task CheckJobs(JobApplication job)
	{
		using (var connection = GetConnection())
		{
			using (var command = connection.CreateCommand())
			{
				command.CommandText = "[Job].[CheckJob]";
				command.CommandType = System.Data.CommandType.StoredProcedure;

				command.Parameters.AddWithValue("@JobId", job.Id);
				await connection.OpenAsync();

				using (var transaction = await connection.BeginTransactionAsync() as SqlTransaction)
				{
					try
					{
						command.Transaction = transaction;
						await command.ExecuteNonQueryAsync();
						await transaction!.CommitAsync();
					}
					catch (Exception)
					{
						await transaction!.RollbackAsync();
						throw;
					}
				}
			}
		}
	}
}