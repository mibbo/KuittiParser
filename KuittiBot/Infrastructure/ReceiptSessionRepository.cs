using AzureTableDataStore;
using Dapper;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static Dapper.SqlMapper;

namespace KuittiBot.Functions.Infrastructure
{
    public class ReceiptSessionRepository : IReceiptSessionRepository
    {
        private ILogger<ReceiptSessionRepository> _logger;
        private readonly string _connectionString;
        public ReceiptSessionRepository(
            ILogger<ReceiptSessionRepository> logger,
            string connectionString)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = connectionString;
        }

        public async Task InsertSessionIfNotExistAsync(SessionInfo session)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                var sessionExistsQuery = "SELECT COUNT(1) FROM Receipts WHERE Hash = @Hash";
                var sessionExists = await connection.ExecuteScalarAsync<int>(sessionExistsQuery, new { Hash = session.Hash }) > 0;

                if (!sessionExists)
                {
                    string query = "INSERT INTO Receipts (SessionId, UserId, Hash, FileName, FileId, SessionSuccesful, ShopName, RawTotalCost) VALUES (@SessionId, @UserId, @Hash, @FileName, @FileId, @SessionSuccesful, @ShopName, @RawTotalCost)";
                    var rowsAffected = await connection.ExecuteAsync(query, session);
                }
                else
                {
                    Console.WriteLine($"The same hash for the Session file '{session.FileName}' already exists in the storage with filename '{session.FileName}'");
                }
            }
            catch (Exception e)
            {
                throw new Exception("Inserting into session table failed: " + e.Message, e);
            }
        }

        //public async Task<int> GetSessionCountByUserId(string userId)
        //{
        //    try
        //    {
        //        Expression<Func<ReceiptSessionEntity, bool>> query = file => file.UserId == userId;
        //        var file = await _tableDataStore.FindAsync(query);
        //        return file.Count();
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception("Retrieving from session cache table failed: " + e.Message, e);
        //    }
        //}

        //public async Task<ReceiptSessionEntity> GetSessionByHash(string hash)
        //{
        //    try
        //    {
        //        Expression<Func<ReceiptSessionEntity, bool>> query = file => file.Hash == hash;
        //        var file = await _tableDataStore.FindAsync(query);
        //        return file.ToList().FirstOrDefault();
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception("Retrieving from session cache table failed: " + e.Message, e);
        //    }
        //}

        public async Task UpdateSession(SessionInfo updatedSession)
        {
            try
            {
                string query = "UPDATE Receipts SET SuccessState = @SuccessState, ShopName = @ShopName WHERE Hash = @Hash";
                using var connection = new SqlConnection(_connectionString);
                await connection.ExecuteAsync(query, updatedSession);
            }
            catch (Exception e)
            {
                throw new Exception("Updating the success state in session cache table failed: " + e.Message, e);
            }
        }


        //public async Task<int> GetCount(string userId)
        //{
        //    try
        //    {
        //        Expression<Func<UserDataCacheEntity, bool>> query = user => user.Id == userId;
        //        var user = await _tableDataStore.FindAsync(query);
        //        return user.ToList().Count()+1;
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception("Retrieving from session cache table failed: " + e.Message, e);
        //    }
        //}

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
