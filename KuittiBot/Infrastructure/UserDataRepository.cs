using AzureTableDataStore;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Data.SqlClient;

namespace KuittiBot.Functions.Infrastructure
{
    public class UserDataRepository : IUserDataRepository
    {
        private ILogger<UserDataRepository> _logger;
        private readonly string _connectionString;

        public UserDataRepository(
            ILogger<UserDataRepository> logger, string connectionString)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = connectionString;
        }

        public async Task InsertAsync(UserDataEntity user)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                var query = "INSERT INTO Users (Id, UserName, CurrentState) VALUES (@Id, @UserName, @CurrentState)";
                var rowsAffected = await connection.ExecuteAsync(query, user);
            }
            catch (Exception e)
            {
                throw new Exception("Inserting user data failed: " + e.Message, e);
            }
        }

        public async Task UpdateUserAsync(UserDataEntity user)
        {
            try
            {
                var query = "UPDATE Users SET UserName = @UserName, CurrentState = @CurrentState WHERE Id = @Id";
                using var connection = new SqlConnection(_connectionString);
                await connection.ExecuteAsync(query, user);
            }
            catch (Exception e)
            {
                throw new Exception("Updating property table failed: " + e.Message, e);
            }
        }

        public async Task<UserDataEntity> GetUserByIdAsync(string userId)
        {
            try
            {
                var query = "SELECT * FROM Users WHERE Id = @Id";
                using var connection = new SqlConnection(_connectionString);
                return await connection.QuerySingleOrDefaultAsync<UserDataEntity>(query, new { Id = userId });
            }
            catch (Exception e)
            {
                throw new Exception($"Retrieving user id '{userId}' from property table failed: " + e.Message, e);
            }
        }

        public async Task<string> GetUserStateByIdAsync(string userId)
        {
            try
            {
                var query = "SELECT CurrentState FROM Users WHERE Id = @Id";
                using var connection = new SqlConnection(_connectionString);
                return await connection.QuerySingleOrDefaultAsync<string>(query, new { Id = userId });

            }
            catch (Exception e)
            {
                throw new Exception($"Retrieving user id '{userId}' from user table failed: " + e.Message, e);
            }
        }


        public async Task<IList<UserDataEntity>> GetAllUsers()
        {
            try
            {
                var query = "SELECT * FROM Users";
                using var connection = new SqlConnection(_connectionString);
                return (await connection.QueryAsync<UserDataEntity>(query)).ToList();
            }
            catch (Exception e)
            {
                throw new Exception($"Retrieving users from user table failed: " + e.Message, e);
            }
        }

        //public async Task DeleteAsync(string kohdetunnus)
        //{
        //    try
        //    {
        //        await _tableDataStore.DeleteAsync(BatchingMode.None, x => x.Id == kohdetunnus);
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception($"Failed to delete property number {kohdetunnus}: " + e.Message, e);
        //    }
        //}
    }
}
