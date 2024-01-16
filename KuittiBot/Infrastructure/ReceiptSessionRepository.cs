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

        public async Task<int> InitializeSession(SessionInfo session)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                var sessionExistsQuery = "SELECT COUNT(1) FROM Receipts WHERE Hash = @Hash";
                var sessionExists = await connection.ExecuteScalarAsync<int>(sessionExistsQuery, new { Hash = session.Hash }) > 0;

                if (!sessionExists)
                {
                    string insertQuery = "INSERT INTO Receipts (Hash, FileName, SessionSuccessful) VALUES (@Hash, @FileName, @SessionSuccessful); SELECT CAST(SCOPE_IDENTITY() as int);";
                    var sessionId = await connection.ExecuteScalarAsync<int>(insertQuery, session);
                    return sessionId;
                }
                else
                {
                    Console.WriteLine($"The same hash for the Session file '{session.FileName}' already exists in the storage with filename '{session.FileName}'");
                    return -1; 
                }
            }
            catch (Exception e)
            {
                throw new Exception("Inserting into session table failed: " + e.Message, e);
            }
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
                    string query = "INSERT INTO Receipts (SessionId, Hash, FileName, SessionSuccesful, ShopName, RawTotalCost) VALUES (@SessionId, @Hash, @FileName, @SessionSuccesful, @ShopName, @RawTotalCost)";
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
                string query = "UPDATE Receipts SET SessionSuccessful = @SessionSuccessful, ShopName = @ShopName, RawTotalCost = @RawTotalCost WHERE Hash = @Hash";
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

        public async Task SetSessionPayers(List<string> payers, int sessionId, string userId)
        {
            try
            {
                foreach (var payer in payers)
                {
                    string insertPayerQuery = "INSERT INTO Payers (UserId, Name) VALUES (@UserId, @Name); SELECT CAST(SCOPE_IDENTITY() as int);";
                    string insertPayerReceiptQuery = "INSERT INTO PayerReceipts (PayerId, SessionId) VALUES (@PayerId, @SessionId);";

                    using var connection = new SqlConnection(_connectionString);

                    // Insert into Payers table and get the PayerId
                    var payerId = await connection.ExecuteScalarAsync<int>(insertPayerQuery, new { UserId = userId, Name = payer});

                    // Insert into PayerReceipts table
                    await connection.ExecuteAsync(insertPayerReceiptQuery, new { PayerId = payerId, SessionId = sessionId });
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error in setting session payers: " + e.Message, e);
            }
        }

        //TODO: merge Receipt and ReceiptSessionEntity
        public async Task SaveReceiptAsync(Receipt receipt/*, ReceiptSessionEntity session*/)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Transaction ensures all or nothing is committed
            using var transaction = connection.BeginTransaction();

            try
            {
                //// Insert receipt and get SessionId
                //var sessionId = await connection.ExecuteScalarAsync<int>(
                //    "INSERT INTO Receipts (Hash, FileName, SessionSuccessful, ShopName, RawTotalCost) OUTPUT INSERTED.SessionId VALUES (@Hash, @FileName, @SessionSuccessful, @ShopName, @RawTotalCost);",
                //    new { session.Hash, session.FileName, session.SessionSuccessful, receipt.ShopName, receipt.RawTotalCost },
                //    transaction);

                string query = "UPDATE Receipts SET SessionSuccessful = @SessionSuccessful, ShopName = @ShopName, RawTotalCost = @RawTotalCost WHERE SessionId = @SessionId;";
                await connection.ExecuteAsync(query, new { receipt.SessionSuccessful, receipt.ShopName, receipt.RawTotalCost, SessionId = receipt.SessionId}, transaction);


                foreach (var product in receipt.Products)
                {
                    // Insert product and get ProductId
                    var productId = await connection.ExecuteScalarAsync<int>(
                        "INSERT INTO Products (Cost) OUTPUT INSERTED.ProductId VALUES (@Cost);",
                        new { product.Cost },
                        transaction);

                    // Insert discounts if any
                    foreach (var discount in product.Discounts ?? Enumerable.Empty<decimal>())
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO ProductDiscounts (ProductId, Discount) VALUES (@ProductId, @Discount);",
                            new { ProductId = productId, Discount = discount },
                            transaction);
                    }

                    // Link receipt and product
                    await connection.ExecuteAsync(
                        "INSERT INTO ReceiptProducts (SessionId, ProductId, DividedCost) VALUES (@SessionId, @ProductId, @DividedCost);",
                        new { SessionId = receipt.SessionId, ProductId = productId, DividedCost = product.DividedCost },
                        transaction);
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
