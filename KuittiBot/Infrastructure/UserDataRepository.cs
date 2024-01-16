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
using System.Data;

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
                var query = "INSERT INTO Users (UserId, UserName, CurrentState) VALUES (@UserId, @UserName, @CurrentState)";

                var parameters = new DynamicParameters();
                parameters.Add("UserId", user.UserId, DbType.String);
                parameters.Add("UserName", user.UserName, DbType.String);
                parameters.Add("CurrentState", user.CurrentState.ToString(), DbType.String);

                var rowsAffected = await connection.ExecuteAsync(query, parameters);
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
                var query = "UPDATE Users SET UserName = @UserName, CurrentState = @CurrentState WHERE UserId = @UserId";
                using var connection = new SqlConnection(_connectionString);

                var parameters = new DynamicParameters();
                parameters.Add("UserId", user.UserId, DbType.String);
                parameters.Add("UserName", user.UserName, DbType.String);
                parameters.Add("CurrentState", user.CurrentState.ToString(), DbType.String);

                await connection.ExecuteAsync(query, parameters);
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
                var query = "SELECT * FROM Users WHERE UserId = @UserId";
                using var connection = new SqlConnection(_connectionString);
                return await connection.QuerySingleOrDefaultAsync<UserDataEntity>(query, new { UserId = userId });
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
                var query = "SELECT CurrentState FROM Users WHERE UserId = @UserId";
                using var connection = new SqlConnection(_connectionString);
                return await connection.QuerySingleOrDefaultAsync<string>(query, new { UserId = userId });
            }
            catch (Exception e)
            {
                throw new Exception($"Retrieving user id '{userId}' from user table failed: " + e.Message, e);
            }
        }
        public async Task<int> GetCurrentSessionByIdAsync(string userId)
        {
            try
            {
                var query = "SELECT CurrentSession FROM Users WHERE UserId = @UserId";
                using var connection = new SqlConnection(_connectionString);
                return await connection.QuerySingleOrDefaultAsync<int>(query, new { UserId = userId });
            }
            catch (Exception e)
            {
                throw new Exception($"Retrieving user id '{userId}' from user table failed: " + e.Message, e);
            }
        }

        public async Task SetNewSessionForUserAsync(int sessionId, string userId)
        {
            try
            {
                string query = "UPDATE Users SET CurrentSession = @SessionId WHERE UserId = @userId";
                using var connection = new SqlConnection(_connectionString);
                await connection.ExecuteAsync(query, new { SessionId = sessionId, UserId = userId });
            }
            catch (Exception e)
            {
                throw new Exception("Updating the success state in session cache table failed: " + e.Message, e);
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
