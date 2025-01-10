using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using VendTech.BLL.Managers;

namespace VendTech.BLL.Common
{
    public class TransactionIdGenerator
    {
        public bool TransactionIdExist(long transactionId)
        {
            string cacheKey = "TransactionIds";

            if (HttpRuntime.Cache[cacheKey] != null)
            {
                var Ids = HttpRuntime.Cache[cacheKey] as List<long>;
                Utilities.LogExceptionToDatabase(new Exception($"Checking TransactionIdExist for {transactionId}"), JsonConvert.SerializeObject(Ids));
                if (Ids != null && !Ids.Contains(transactionId))
                {
                    return false;
                }
                else
                    return true;
            }
            return false;
        }

        public static void SetTransactionId(long transactionId)
        {
            string cacheKey = "TransactionIds";
            var Ids = HttpRuntime.Cache[cacheKey] as List<long>;
            if(Ids == null)
                Ids = new List<long>();
            Ids.Add(transactionId);
            HttpRuntime.Cache.Insert(cacheKey, Ids, null, DateTime.Now.AddMinutes(10), System.Web.Caching.Cache.NoSlidingExpiration);
        }

        public async Task<string> GenerateNewTransactionId()
        {
            try
            {
                string _connectionString = WebConfigurationManager.ConnectionStrings["DefaultConnection"].ToString();
                long transactionId = 0;
                string query = @"SELECT TOP 1 TransactionId FROM TransactionDetails ORDER BY TransactionDetailsId DESC";

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        var result = await command.ExecuteScalarAsync();
                        if (result != null && long.TryParse(result.ToString(), out long lastTransactionId))
                        {
                            transactionId = lastTransactionId;
                            do
                            {
                                transactionId = transactionId + 1;
                            } while (TransactionIdExist(transactionId));

                            SetTransactionId(transactionId);
                        }
                        else
                        {
                            transactionId = 1;
                        }
                    }
                    connection.Close();
                }

                return transactionId.ToString();
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(new Exception($"Error on GenerateNewTransactionId"), ex.Message);
                throw;
            }
        }
    }
}
