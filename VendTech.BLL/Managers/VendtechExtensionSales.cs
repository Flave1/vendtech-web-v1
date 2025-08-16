using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Web.Configuration;
using System;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.DAL;
using System.Data.Entity;
using VendTech.BLL.Common;
using System.Linq;
using System.Data.Entity.Infrastructure;
using Polly;
using System.Data.SqlClient;
using System.Data.Entity.Core;
using System.Collections.Generic;
using System.Diagnostics;

namespace VendTech.BLL.Managers
{
    public class VendtechExtensionSales : BaseManager, IVendtechExtensionSales
    {
        private readonly TransactionIdGenerator idGenerator;
        private readonly IPOSManager _posManager;
        private readonly IAsyncPolicy _retryPolicy;

        public VendtechExtensionSales(TransactionIdGenerator idGenerator, IPOSManager posManager)
        {
            this.idGenerator = idGenerator;
            _posManager = posManager;

            // Configure retry policy for retriable database operations
            _retryPolicy = Policy
                
                .Handle<DbUpdateConcurrencyException>()
                .Or<SqlException>()
                .Or<EntityException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    Config.MAX_RETRY_ATTEMPTS,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount) =>
                    {
                        Utilities.LogExceptionToDatabase(
                            exception,
                            $"Retry {retryCount} after {timeSpan.TotalSeconds}s"
                        );
                    });
        }

        private bool IsRetriableSqlException(SqlException ex)
        {
            int[] retryableErrors = {
                -2,     // Timeout
                4060,   // Cannot open database
                40197,  // The service has encountered an error processing your request
                40501,  // The service is currently busy
               // 40613,  // Database is not currently available
                49918,  // Cannot process request
                //49919,  // Cannot process create or update request
                49920,  // Service is too busy
                11001   // Network error
            };

            return retryableErrors.Contains(ex.Number);
        }

        private async Task<T> ExecuteOperation<T>(Func<Task<T>> operation, string operationName)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var result = await operation();
                    return result;
                });
            }
            catch (DbUpdateException ex)
            {
                Utilities.LogExceptionToDatabase(ex, $"Database update error during {operationName}");
                throw new InvalidOperationException($"Failed to save changes during {operationName}. Please try again.", ex);
            }
            catch (TimeoutException ex)
            {
                Utilities.LogExceptionToDatabase(ex, $"Timeout during {operationName}");
                throw new InvalidOperationException("The operation timed out. Please try again.", ex);
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(ex, $"Error during {operationName}");
                throw;
            }
        }


        async Task<ReceiptModel> IVendtechExtensionSales.RechargeFromVendtechExtension(RechargeMeterModel model)
        {
            var response = new ReceiptModel { ReceiptStatus = new ReceiptStatus() };
            var trax = new TransactionDetail();
            User user;
            POS pos;
            Meter meter;
            Platform platform;
            using (var _context = new VendtechEntities())
            {
                user = await _context.Users.FirstOrDefaultAsync(p => p.UserId == model.UserId);
                pos = user.POS.FirstOrDefault(p => p.POSId == model.POSId);
                meter = await _context.Meters.FirstOrDefaultAsync(d => d.MeterId == model.MeterId);
                platform = await _context.Platforms.FirstOrDefaultAsync(d => d.PlatformType == (int)PlatformTypeEnum.ELECTRICITY);
            }

            var validationResult = model.validateRequest(user, pos, platform);
            if (validationResult != "clear")
            {
                response.ReceiptStatus.Status = "unsuccessful";
                response.ReceiptStatus.Message = validationResult;
                return response;
            }

            try
            {
                model.UpdateRequestModel(meter == null ? "" : meter?.Number, pos.POSId);

                var pendingTrx = await getLastMeterPendingTransaction(model.MeterNumber, model.Amount);

                var isDuplicate = model.IsRequestADuplicate(pendingTrx);

                var transaction = isDuplicate ? pendingTrx : trax;

                trax = await ProcessTransaction(isDuplicate, model, transaction);
                var receipt = await BuildRceipt(trax.TransactionDetailsId);
                PushNotification(user, model, trax.TransactionDetailsId);

                return receipt;
            }
            catch (ArgumentException ex)
            {
                response.ReceiptStatus.Message = ex.Message;
                response.ReceiptStatus.Status = "unsuccessful";
                return response;
            }
            catch (Exception)
            {
                response.ReceiptStatus.Message = "Did not result in a vend. Please try again!";
                response.ReceiptStatus.Status = "unsuccessful";
                return response;
            }
        }

        public async Task<TransactionDetail> ProcessTransaction(bool isDuplicate, RechargeMeterModel model,
          TransactionDetail transactionDetail, bool treatAsPending = false)
        {
            VtechExtensionResponse vendResponse = null;
            VendtechExtSalesResult vendResponseResult = new VendtechExtSalesResult();

            try
            {
                if (!isDuplicate)
                {
                    if (!treatAsPending)
                        transactionDetail = await CreateRecordBeforeVend(model);

                    model.UpdateRequestModel(transactionDetail, model.POSId);
                    vendResponse = await MakeRechargeRequest(model, transactionDetail);

                    
                    if (vendResponse is null || vendResponse?.Result is null)
                    {
                        Utilities.LogExceptionToDatabase(new Exception($"{vendResponse}"));
                        throw new ArgumentException("Unable to process transaction");
                    }
                    else
                    {
                        if (vendResponse.Status.ToLower() == "pending")
                        {
                            await ProcessPending(vendResponse, vendResponseResult, transactionDetail, model);
                        }

                        if (vendResponse.Status.ToLower() == "failed")
                        {
                            await ProcessFailed(vendResponse, vendResponseResult, transactionDetail);
                            throw new ArgumentException(vendResponse.Result.FailedResponse.ErrorMessage);
                        }

                        if (vendResponse.Status.ToLower() == "success")
                        {
                            await ProcessSuccess(vendResponse, vendResponseResult, transactionDetail, model.POSId);
                            return transactionDetail;
                        }
                        else
                        {
                            throw new ArgumentException("Did not result in a vend! Try again");
                        }

                    }
                }
                else
                {
                    model.UpdateRequestModel(transactionDetail, model.POSId);
                    vendResponse = await QueryStatusRequest(model, transactionDetail);

                    if (vendResponse is null || vendResponse?.Result is null)
                    {
                        Utilities.LogExceptionToDatabase(new Exception($"{vendResponse}"));
                        throw new ArgumentException("Unable to process transaction");
                    }
                    else
                    {
                        if (vendResponse.Status.ToLower() == "pending")
                        {
                            await ProcessPending(vendResponse, vendResponseResult, transactionDetail, model);
                        }

                        if (vendResponse.Status.ToLower() == "failed")
                        {
                            await ProcessFailed(vendResponse, vendResponseResult, transactionDetail);
                            throw new ArgumentException(vendResponse.Result.FailedResponse.ErrorMessage);
                        }

                        if (vendResponse.Status.ToLower() == "success")
                        {
                            await ProcessSuccess(vendResponse, vendResponseResult, transactionDetail, model.POSId);
                            return transactionDetail;
                        }
                        else
                        {
                            throw new ArgumentException("Did not result in a vend! Try again");
                        }


                    }
                }
            }
            catch (Exception ex)
            {
                if(ex is ArgumentException)
                    throw;
                else
                {
                    Utilities.LogExceptionToDatabase(
                        new Exception($"ProcessTransactionException for {model.TransactionId} || {transactionDetail.TransactionId}", ex),
                        $"Source: {ex?.Source ?? "Unknown"}, Inner: {ex?.Message ?? ex?.InnerException?.Message}"
                    );
                    throw;
                }

            }

        }
        private async Task ProcessPending(
        VtechExtensionResponse vendResponse,
        VendtechExtSalesResult vendResponseResult,
        TransactionDetail transactionDetail,
        RechargeMeterModel model)
        {
            int count = 0;

            try
            {
                do
                {
                    // Query status from vend service
                    vendResponse = await QueryStatusRequest(model, transactionDetail);
                    vendResponseResult = vendResponse.Result;

                    // Update in-memory transaction detail
                    transactionDetail.VendStatus = vendResponseResult?.FailedResponse?.ErrorMessage;
                    transactionDetail.VendStatusDescription = vendResponseResult?.FailedResponse?.ErrorDetail;
                    transactionDetail.QueryStatusCount = count;
                    transactionDetail.StatusResponse = JsonConvert.SerializeObject(vendResponseResult);

                    // Update database
                    string connectionString = WebConfigurationManager.ConnectionStrings["DefaultConnection"].ToString();
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string sql = @"
                    UPDATE TransactionDetails
                    SET
                        VendStatus = @VendStatus,
                        VendStatusDescription = @VendStatusDescription,
                        QueryStatusCount = @QueryStatusCount,
                        StatusResponse = @StatusResponse,
                        Request = @Request,
                        Response = @Response
                    WHERE TransactionDetailsId = @TransactionDetailsId";

                        using (SqlCommand cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@VendStatus", (object)transactionDetail.VendStatus ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@VendStatusDescription", (object)transactionDetail.VendStatusDescription ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@QueryStatusCount", transactionDetail.QueryStatusCount);
                            cmd.Parameters.AddWithValue("@StatusResponse", transactionDetail.StatusResponse);
                            cmd.Parameters.AddWithValue("@TransactionDetailsId", transactionDetail.TransactionDetailsId);
                            cmd.Parameters.AddWithValue("@Request", transactionDetail.Request ?? string.Empty);
                            cmd.Parameters.AddWithValue("@Response", transactionDetail.Response ?? string.Empty);

                            await conn.OpenAsync();
                            cmd.CommandTimeout = 60;
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    // Increment for the next query cycle
                    count++;

                    // Optional: add delay between retries if needed
                   await Task.Delay(2000);
                   Utilities.LogExceptionToDatabase(new Exception("count on repeat: " + count), $"requesting pending transaction: {transactionDetail?.TransactionId ?? "N/A"}");
                } while (vendResponse.Status?.ToLower() == "pending");
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(ex,
                    $"Error processing pending transaction {transactionDetail?.TransactionId ?? "N/A"}");
                throw;
            }
        }



        private async Task ProcessFailed(
         VtechExtensionResponse vendResponse,
         VendtechExtSalesResult vendResponseResult,
         TransactionDetail transactionDetail)
        {
            // Extract result safely
            vendResponseResult = vendResponse?.Result;

            await _posManager.RefundDeductedBalanceAsync(transactionDetail.POSId.Value, transactionDetail);
            // Build connection
            string connectionString = WebConfigurationManager.ConnectionStrings["DefaultConnection"].ToString();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string sql = @"
                    UPDATE TransactionDetails
                    SET
                        VendStatus = @VendStatus,
                        VendStatusDescription = @VendStatusDescription,
                        Status = @Status,
                        Finalised = @Finalised,
                        Request = @Request,
                        Response = @Response
                    WHERE TransactionDetailsId = @TransactionDetailsId";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@VendStatus", vendResponseResult?.Status ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@VendStatusDescription", vendResponseResult?.Status ?? (object)DBNull.Value);                    
                    cmd.Parameters.AddWithValue("@TransactionDetailsId", transactionDetail.TransactionDetailsId);
                    cmd.Parameters.AddWithValue("@Request", transactionDetail.Request ?? string.Empty);
                    cmd.Parameters.AddWithValue("@Response", transactionDetail.Response ?? string.Empty);

                    if(vendResponse?.Message == "Error in server handshake")
                    {
                        cmd.Parameters.AddWithValue("@Status", (int)RechargeMeterStatusEnum.Pending);
                    }
                    else if(vendResponse?.Message == "Transaction (Process ID 88) was deadlocked on lock resources with another process and has been chosen as the deadlock victim. Rerun the transaction.")
                    {
                        cmd.Parameters.AddWithValue("@Status", (int)RechargeMeterStatusEnum.Pending);
                    }
                    else if(vendResponse?.Message == "ex.Message: getData Error ex.InnerMessage: Execution Timeout Expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.")
                    {
                        cmd.Parameters.AddWithValue("@Status", (int)RechargeMeterStatusEnum.Pending);
                     }
                    else
                    {
                        cmd.Parameters.AddWithValue("@Status", (int)RechargeMeterStatusEnum.Failed);
                        //cmd.Parameters.AddWithValue("@PaymentStatus", (int)PaymentStatus.Failed);
                        cmd.Parameters.AddWithValue("@Finalised", true);
                    }
                        

                    await conn.OpenAsync();
                    await ExecuteOperation(async () =>
                    {
                        cmd.CommandTimeout = 60;
                        return await cmd.ExecuteNonQueryAsync();
                    }, "ProcessFailed");

                }
            }

            await ReadErrorMessage(vendResponse?.Message, vendResponse.Result.Code, transactionDetail);
        }


        private async Task ProcessSuccess(VtechExtensionResponse vendResponse,
            VendtechExtSalesResult vendResponseResult,
            TransactionDetail transactionDetail, long posId)
        {
            vendResponseResult = vendResponse?.Result;
            POS pos;
            using (var _context = new VendtechEntities())
            {
                pos = await _context.POS.FirstOrDefaultAsync(p => p.POSId == posId);
            }
            transactionDetail = await UpdateTransactionOnSuccess(vendResponseResult, transactionDetail, pos);

            Common.PushNotification.Instance
                    .IncludeAdminWidgetSales()
                    .IncludeUserBalanceOnTheWeb(transactionDetail.UserId)
                    .Send();
        }
        private async Task ReadErrorMessage(string message, int code, TransactionDetail tx)
        {
            if (message == "Error in server handshake")
            {
                throw new ArgumentException("Please Wait For 1 Minute. For This Transaction To Finalize.");
            }
            if(message == "Transaction (Process ID 88) was deadlocked on lock resources with another process and has been chosen as the deadlock victim. Rerun the transaction.")
            {
                throw new ArgumentException("Please Wait For 1 Minute. For This Transaction To Finalize.");
            }
            if(message == "ex.Message: getData Error ex.InnerMessage: Execution Timeout Expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.")
            {
                throw new ArgumentException("Please Wait For 1 Minute. For This Transaction To Finalize.");
            }
            if (message == "The request timed out with the Ouc server.")
            {
                throw new ArgumentException(message);
            }

            await FlagTransaction(tx, RechargeMeterStatusEnum.Failed);
            if (code == 4514)
            {
                await DisablePlatform(PlatformTypeEnum.ELECTRICITY);
                NotifyAdmin();
                throw new ArgumentException("Error: Vending is disabled");
            }

            if (code == 4094)
            {
                await DisablePlatform(PlatformTypeEnum.ELECTRICITY);
                NotifyAdmin();
                throw new ArgumentException("Error: Vending is disabled");
            }

            if (message == "InCMS-BL-CO000846. The amount is too low for recharge")
            {
                throw new ArgumentException("The amount is too low for recharge");
            }

            if (message == "Unexpected error in OUC VendVoucher")
            {
                throw new ArgumentException("EDSA service is currently down! Please try again later");
            }

            if (message == "CB001600 : InCMS-BL-CB001600. Error serial number, contracted service not found or not active.")
            {
                throw new ArgumentException("Error serial number, contracted service not found or not active");
            }
            if (message == "-47 : InCMS-BL-CB001273. Error, purchase units less than minimum.")
            {
                throw new ArgumentException("Purchase units less than minimum.");
            }
            if (message == "The specified TransactionID already exists for this terminal.")
            {
                throw new ArgumentException("Please try again!!");
            }
        }

        private async Task FlagTransaction(TransactionDetail tx, RechargeMeterStatusEnum status)
        {
            try
            {
                if (tx == null || tx.TransactionDetailsId == 0)
                    throw new ArgumentException("Invalid transaction details ID.");

                string connectionString = WebConfigurationManager.ConnectionStrings["DefaultConnection"].ToString();
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string sql = @"
                    UPDATE TransactionDetails
                    SET Status = @Status
                    WHERE TransactionDetailsId = @TransactionDetailsId";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Status", (int)status);
                        cmd.Parameters.AddWithValue("@TransactionDetailsId", tx.TransactionDetailsId);

                        await ExecuteOperation(async () =>
                        {
                            await conn.OpenAsync();
                            cmd.CommandTimeout = 60;
                            return await cmd.ExecuteNonQueryAsync();
                        }, "FlagTransaction");
                    }
                }

                // Optional: update local object too
                tx.Status = (int)status;
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(ex, $"Error flagging transaction {tx?.TransactionId ?? "N/A"}");
                throw;
            }
        }

        private async Task DisablePlatform(PlatformTypeEnum pl)
        {
            try
            {
                string connectionString = WebConfigurationManager.ConnectionStrings["DefaultConnection"].ToString();
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string sql = @"
                    UPDATE Platform
                    SET DisablePlatform = @DisablePlatform
                    WHERE PlatformType = @PlatformType";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@DisablePlatform", true);
                        cmd.Parameters.AddWithValue("@PlatformType", (int)pl);

                        await ExecuteOperation(async () =>
                        {
                            conn.Open();
                            cmd.CommandTimeout = 60;
                            return cmd.ExecuteNonQuery();
                        }, "DisablePlatform");
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(ex, $"Error disabling platform {(int)pl} ({pl})");
                throw;
            }
        }


        void NotifyAdmin()
        {
            var body = $"Hello Victor</br></br>" +
                $"This is to notify you that VENDTECH IServices is receiving errors from EDSA or RTS and has been disabled</br></br>" +
                $"1) VENDTECH IS OUT OF FUNDS</br></br>" +
                $"2) RTS SERVICES IS DISABLED</br></br>" +
                $"Please keep in mind to ENABLE Services again.</br></br>" +
                $"{Utilities.DomainUrl}/Admin/Platform/ManagePlatforms (ENABLE EDSA ON VENDTECH PLATFORM)";
            Utilities.SendEmail("vblell@gmail.com", "[URGENT] VENDING IS DISABLED", body);

        }

        private async Task<TransactionDetail> getLastMeterPendingTransaction(string MeterNumber, decimal amount)
        {
            TransactionDetail transactionDetail;
            using (var _context = new VendtechEntities())
            {
                transactionDetail = await _context.TransactionDetails.Where(p => p.Status ==
                (int)RechargeMeterStatusEnum.Pending && p.MeterNumber1.ToLower() == MeterNumber.ToLower() && p.Amount == amount)
                    .OrderByDescending(d => d.CreatedAt).FirstOrDefaultAsync();
            }
            return transactionDetail;
        }
        

        public async Task<ReceiptModel> GetStatusFromVendtechExtension(string trxId, long userId)
        {
            var response = new ReceiptModel { ReceiptStatus = new ReceiptStatus { Status = "", Message = "" } };
            TransactionDetail pendingTrax;
            POS pos;
            using (var _context = new VendtechEntities())
            {
                pendingTrax = await _context.TransactionDetails.FirstOrDefaultAsync(e => e.TransactionId == trxId);
                if (userId == 0)
                    userId = pendingTrax.UserId;
                pos = await _context.POS.FirstOrDefaultAsync(p => p.VendorId == userId);
            }
            
            if (pendingTrax == null)
            {
                response.ReceiptStatus.Status = "unsuccessful";
                response.ReceiptStatus.Message = "Unable to find transaction";
                return response;
            }

            if (pos == null)
            {
                response.ReceiptStatus.Status = "unsuccessful";
                response.ReceiptStatus.Message = "User account does not have a pos";
                return response;
            }

            var requestModel = new RechargeMeterModel
            {
                UserId = pendingTrax.UserId,
                TransactionId = pendingTrax.TransactionId,
                POSId = pos.POSId
            };

            var verifiedTrax = await ProcessTransaction(true, requestModel, pendingTrax, true);

            if (verifiedTrax != null)
            {
                var receipt = await BuildRceipt(verifiedTrax.TransactionDetailsId);
                receipt.ShouldShowSmsButton = (bool)verifiedTrax.POS.WebSms;
                receipt.ShouldShowPrintButton = (bool)verifiedTrax.POS.WebPrint;
                receipt.mobileShowSmsButton = (bool)verifiedTrax.POS.PosSms;
                receipt.mobileShowPrintButton = (bool)verifiedTrax.POS.PosPrint;
                receipt.ReceiptStatus.Status = "successful";
                return receipt;
            }

            return response;
        }

        public async Task<ReceiptModel> BuildRceipt(long id)
        {
            TransactionDetail td;
            using (var _context = new VendtechEntities())
            {
                td = await _context.TransactionDetails.Where(d => d.TransactionDetailsId == id)
                    .Include(d => d.POS).Include(d => d.User).FirstOrDefaultAsync();
            }
            if (td == null)
                throw new ArgumentNullException(nameof(td));

            if (string.IsNullOrEmpty(td.MeterToken1))
                throw new ArgumentException("Did not result in a vend");

           
            try
            {
                var receipt = new ReceiptModel
                {
                    AccountNo = td?.AccountNumber ?? string.Empty,
                    POS = td?.POS?.SerialNumber ?? string.Empty,
                    CustomerName = td?.Customer ?? string.Empty,
                    ReceiptNo = td?.ReceiptNumber ?? string.Empty,
                    Address = td?.CustomerAddress ?? string.Empty,
                    Tarrif = !string.IsNullOrEmpty(td.Tariff) ? Utilities.FormatAmount(Convert.ToDecimal(td.Tariff)) : "0",
                    DeviceNumber = td?.MeterNumber1 ?? string.Empty,
                    DebitRecovery = Convert.ToDecimal(td?.DebitRecovery ?? "0"),
                    Amount = FormatAmount(td?.TenderedAmount ?? 0),
                    Charges = !string.IsNullOrEmpty(td.ServiceCharge) ? Utilities.FormatAmount(Convert.ToDecimal(td.ServiceCharge)) : "0",
                    Commission = "0.00",
                    Unit = !string.IsNullOrEmpty(td.Units) ? Utilities.FormatAmount(Convert.ToDecimal(td.Units)) : "0",
                    UnitCost = !string.IsNullOrEmpty(td.CostOfUnits) ? Utilities.FormatAmount(Convert.ToDecimal(td.CostOfUnits)) : "0",
                    SerialNo = td?.SerialNumber ?? string.Empty,
                    Pin1 = Utilities.FormatThisToken(td?.MeterToken1) ?? string.Empty,
                    Pin2 = Utilities.FormatThisToken(td?.MeterToken2) ?? string.Empty,
                    Pin3 = Utilities.FormatThisToken(td?.MeterToken3) ?? string.Empty,
                    Discount = "0",
                    Tax = !string.IsNullOrEmpty(td.TaxCharge) ? Utilities.FormatAmount(Convert.ToDecimal(td.TaxCharge)) : "0",
                    TransactionDate = td?.CreatedAt.ToString("dd/MM/yyyy hh:mm") ?? string.Empty,
                    VendorId = td?.User?.Vendor ?? string.Empty,
                    EDSASerial = td?.SerialNumber ?? string.Empty,
                    VTECHSerial = td?.TransactionId ?? string.Empty,
                    PlatformId = td?.PlatFormId ?? 0,
                    ShouldShowSmsButton = td?.POS?.WebSms ?? false,
                    ShouldShowPrintButton = td?.POS?.WebPrint ?? false,
                    mobileShowSmsButton = td?.POS?.PosSms ?? false,
                    mobileShowPrintButton = td?.POS?.PosPrint ?? false,
                    CurrentBallance = td?.POS?.Balance ?? 0,
                    ReceiptStatus = new ReceiptStatus
                    {
                        Message = "Successful",
                        Status = "success"
                    }
                };

                return receipt;
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(ex, $"Error building receipt for transaction {td?.TransactionId}");
                throw;
            }
        }

        private string FormatAmount(decimal amount)
        {
            var formatted = amount.ToString("N");
            return formatted.Contains('.') ? formatted.TrimEnd('0').TrimEnd('.') : formatted;
        }

        private void PushNotification(User user, RechargeMeterModel model, long MeterRechargeId)
        {
            using (var _context = new VendtechEntities())
            {
                var deviceTokens = _context.TokensManagers.Where(p => p.DeviceToken != null && p.DeviceToken != string.Empty && p.UserId == user.UserId).Select(p => new { p.AppType, p.DeviceToken }).ToList().Distinct();
                var obj = new PushNotificationModel();
                obj.UserId = model.UserId;
                obj.Id = MeterRechargeId;
                obj.Title = "Meter recharged successfully";
                obj.Message = $"Your meter has successfully recharged with NLe {Utilities.FormatAmount(model.Amount)} PIN: {model.MeterToken1}{model.MeterToken2}{model.MeterToken3}";
                obj.NotificationType = NotificationTypeEnum.MeterRecharge;
                foreach (var item in deviceTokens)
                {
                    obj.DeviceToken = item.DeviceToken;
                    obj.DeviceType = item.AppType.Value;
                    Common.PushNotification.PushNotificationToMobile(obj);
                }
            }
            
        }


        private async Task<TransactionDetail> UpdateTransactionOnSuccess(VendtechExtSalesResult response_data, TransactionDetail trans, POS pos)
        {
            if (response_data?.SuccessResponse?.Voucher == null)
                throw new ArgumentNullException(nameof(response_data.SuccessResponse.Voucher));

            try
            {
                var voucher = response_data.SuccessResponse.Voucher;

                string connectionString = WebConfigurationManager.ConnectionStrings["DefaultConnection"].ToString();
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string sql = @"
                    UPDATE TransactionDetails
                    SET
                        CostOfUnits = @CostOfUnits,
                        MeterToken1 = @MeterToken1,
                        MeterToken2 = @MeterToken2,
                        MeterToken3 = @MeterToken3,
                        Status = @Status,
                        POSId = @POSId,
                        UserId = @UserId,
                        AccountNumber = @AccountNumber,
                        Customer = @Customer,
                        ReceiptNumber = @ReceiptNumber,
                        SerialNumber = @SerialNumber,
                        RTSUniqueID = @RTSUniqueID,
                        ServiceCharge = @ServiceCharge,
                        CurrentDealerBalance = @CurrentDealerBalance,
                        Tariff = @Tariff,
                        TaxCharge = @TaxCharge,
                        Units = @Units,
                        CustomerAddress = @CustomerAddress,
                        Finalised = @Finalised,
                        VProvider = @VProvider,
                        StatusRequestCount = @StatusRequestCount,
                        Sold = @Sold,
                        VendStatusDescription = @VendStatusDescription,
                        VoucherSerialNumber = @VoucherSerialNumber,
                        VendStatus = @VendStatus,
                        Request = @Request,
                        Response = @Response
                    WHERE TransactionDetailsId = @TransactionDetailsId";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CostOfUnits", voucher.CostOfUnits ?? "0");
                        cmd.Parameters.AddWithValue("@MeterToken1", voucher.MeterToken1?.ToString() ?? string.Empty);
                        cmd.Parameters.AddWithValue("@MeterToken2", voucher.MeterToken2?.ToString() ?? string.Empty);
                        cmd.Parameters.AddWithValue("@MeterToken3", voucher.MeterToken3?.ToString() ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Status", (int)RechargeMeterStatusEnum.Success);
                        cmd.Parameters.AddWithValue("@POSId", pos?.POSId ?? throw new ArgumentNullException(nameof(pos.POSId)));
                        cmd.Parameters.AddWithValue("@UserId", pos.VendorId ?? throw new ArgumentNullException(nameof(pos.VendorId)));
                        cmd.Parameters.AddWithValue("@AccountNumber", voucher?.AccountNumber ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Customer", voucher?.Customer ?? string.Empty);
                        cmd.Parameters.AddWithValue("@ReceiptNumber", voucher?.ReceiptNumber ?? string.Empty);
                        cmd.Parameters.AddWithValue("@SerialNumber", response_data.SuccessResponse.VendtechTransactionId ?? string.Empty);
                        cmd.Parameters.AddWithValue("@RTSUniqueID", response_data.SuccessResponse.VendtechTransactionId);
                        cmd.Parameters.AddWithValue("@ServiceCharge", voucher?.ServiceCharge ?? "0");
                        cmd.Parameters.AddWithValue("@CurrentDealerBalance", Convert.ToDecimal(response_data?.SuccessResponse?.WalleBalance ?? "0"));
                        cmd.Parameters.AddWithValue("@Tariff", voucher?.Tariff ?? "0");
                        cmd.Parameters.AddWithValue("@TaxCharge", voucher?.TaxCharge ?? "0");
                        cmd.Parameters.AddWithValue("@Units", voucher?.Units ?? "0");
                        cmd.Parameters.AddWithValue("@CustomerAddress", voucher?.CustomerAddress ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Finalised", true);
                        cmd.Parameters.AddWithValue("@VProvider", string.Empty);
                        cmd.Parameters.AddWithValue("@StatusRequestCount", 0);
                        cmd.Parameters.AddWithValue("@Sold", true);
                        cmd.Parameters.AddWithValue("@VendStatusDescription", "success");
                        cmd.Parameters.AddWithValue("@VoucherSerialNumber", voucher.VoucherSerialNumber ?? string.Empty);
                        cmd.Parameters.AddWithValue("@VendStatus", string.Empty);
                        cmd.Parameters.AddWithValue("@TransactionDetailsId", trans.TransactionDetailsId);
                        cmd.Parameters.AddWithValue("@Request", trans.Request ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Response", trans.Response ?? string.Empty);


                        await conn.OpenAsync();
                        await ExecuteOperation(async () =>
                        {
                            cmd.CommandTimeout = 60;
                            return await cmd.ExecuteNonQueryAsync();
                        }, "UpdateTransactionOnSuccess");
                    }
                }

                // Then deduct balance as separate logic
                return await _posManager.DeductBalanceAsync(pos.POSId, trans);
            }
            catch (Exception ex)
            {
                string contextInfo = $"Error updating transaction {trans?.TransactionId ?? "N/A"} at {DateTime.UtcNow}";
                Utilities.LogExceptionToDatabase(ex, contextInfo);
                throw;
            }
        }

        private async Task<TransactionDetail> CreateRecordBeforeVend(RechargeMeterModel model)
        {
            var trans = new TransactionDetail
            {
                PlatFormId = (int)model.PlatformId,
                UserId = model.UserId,
                MeterId = model.MeterId,
                POSId = model.POSId,
                MeterNumber1 = model.MeterNumber,
                MeterToken1 = model.MeterToken1,
                Amount = model.Amount,
                IsDeleted = false,
                Status = (int)RechargeMeterStatusEnum.Pending,
                CreatedAt = DateTime.UtcNow,
                AccountNumber = string.Empty,
                CurrentDealerBalance = 0,
                Customer = string.Empty,
                ReceiptNumber = string.Empty,
                RequestDate = DateTime.UtcNow,
                RTSUniqueID = "00",
                SerialNumber = string.Empty,
                ServiceCharge = string.Empty,
                Tariff = string.Empty,
                TaxCharge = string.Empty,
                TenderedAmount = model.Amount,
                TransactionAmount = model.Amount,
                Units = string.Empty,
                VProvider = string.Empty,
                Finalised = false,
                StatusRequestCount = 0,
                Sold = false,
                DateAndTimeSold = string.Empty,
                DateAndTimeFinalised = string.Empty,
                DateAndTimeLinked = string.Empty,
                VoucherSerialNumber = string.Empty,
                VendStatus = string.Empty,
                VendStatusDescription = string.Empty,
                StatusResponse = string.Empty,
                DebitRecovery = "0",
                CostOfUnits = "0",
                PaymentStatus = (int)PaymentStatus.Pending
            };

            try
            {
                trans.TransactionId = await idGenerator.GenerateNewTransactionId();
                string connectionString = WebConfigurationManager.ConnectionStrings["DefaultConnection"].ToString();
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string sql = @"
                    INSERT INTO TransactionDetails (
                        TransactionId, PlatFormId, UserId, MeterId, POSId, MeterNumber1, MeterToken1,
                        Amount, IsDeleted, Status, CreatedAt, AccountNumber, CurrentDealerBalance,
                        Customer, ReceiptNumber, RequestDate, RTSUniqueID, SerialNumber, ServiceCharge,
                        Tariff, TaxCharge, TenderedAmount, TransactionAmount, Units, VProvider, Finalised,
                        StatusRequestCount, Sold, DateAndTimeSold, DateAndTimeFinalised, DateAndTimeLinked,
                        VoucherSerialNumber, VendStatus, VendStatusDescription, StatusResponse,
                        DebitRecovery, CostOfUnits, PaymentStatus
                    )
                    OUTPUT INSERTED.TransactionDetailsId
                    VALUES (
                        @TransactionId, @PlatFormId, @UserId, @MeterId, @POSId, @MeterNumber1, @MeterToken1,
                        @Amount, @IsDeleted, @Status, @CreatedAt, @AccountNumber, @CurrentDealerBalance,
                        @Customer, @ReceiptNumber, @RequestDate, @RTSUniqueID, @SerialNumber, @ServiceCharge,
                        @Tariff, @TaxCharge, @TenderedAmount, @TransactionAmount, @Units, @VProvider, @Finalised,
                        @StatusRequestCount, @Sold, @DateAndTimeSold, @DateAndTimeFinalised, @DateAndTimeLinked,
                        @VoucherSerialNumber, @VendStatus, @VendStatusDescription, @StatusResponse,
                        @DebitRecovery, @CostOfUnits, @PaymentStatus
                    )";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@TransactionId", trans.TransactionId);
                        cmd.Parameters.AddWithValue("@PlatFormId", trans.PlatFormId);
                        cmd.Parameters.AddWithValue("@UserId", trans.UserId);
                        cmd.Parameters.AddWithValue("@MeterId", trans.MeterId ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@POSId", trans.POSId);
                        cmd.Parameters.AddWithValue("@MeterNumber1", trans.MeterNumber1);
                        cmd.Parameters.AddWithValue("@MeterToken1", trans.MeterToken1 ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Amount", trans.Amount);
                        cmd.Parameters.AddWithValue("@IsDeleted", trans.IsDeleted);
                        cmd.Parameters.AddWithValue("@Status", trans.Status);
                        cmd.Parameters.AddWithValue("@CreatedAt", trans.CreatedAt);
                        cmd.Parameters.AddWithValue("@AccountNumber", trans.AccountNumber);
                        cmd.Parameters.AddWithValue("@CurrentDealerBalance", trans.CurrentDealerBalance);
                        cmd.Parameters.AddWithValue("@Customer", trans.Customer);
                        cmd.Parameters.AddWithValue("@ReceiptNumber", trans.ReceiptNumber);
                        cmd.Parameters.AddWithValue("@RequestDate", trans.RequestDate);
                        cmd.Parameters.AddWithValue("@RTSUniqueID", trans.RTSUniqueID);
                        cmd.Parameters.AddWithValue("@SerialNumber", trans.SerialNumber);
                        cmd.Parameters.AddWithValue("@ServiceCharge", trans.ServiceCharge);
                        cmd.Parameters.AddWithValue("@Tariff", trans.Tariff);
                        cmd.Parameters.AddWithValue("@TaxCharge", trans.TaxCharge);
                        cmd.Parameters.AddWithValue("@TenderedAmount", trans.TenderedAmount);
                        cmd.Parameters.AddWithValue("@TransactionAmount", trans.TransactionAmount);
                        cmd.Parameters.AddWithValue("@Units", trans.Units);
                        cmd.Parameters.AddWithValue("@VProvider", trans.VProvider);
                        cmd.Parameters.AddWithValue("@Finalised", trans.Finalised);
                        cmd.Parameters.AddWithValue("@StatusRequestCount", trans.StatusRequestCount);
                        cmd.Parameters.AddWithValue("@Sold", trans.Sold);
                        cmd.Parameters.AddWithValue("@DateAndTimeSold", trans.DateAndTimeSold);
                        cmd.Parameters.AddWithValue("@DateAndTimeFinalised", trans.DateAndTimeFinalised);
                        cmd.Parameters.AddWithValue("@DateAndTimeLinked", trans.DateAndTimeLinked);
                        cmd.Parameters.AddWithValue("@VoucherSerialNumber", trans.VoucherSerialNumber);
                        cmd.Parameters.AddWithValue("@VendStatus", trans.VendStatus);
                        cmd.Parameters.AddWithValue("@VendStatusDescription", trans.VendStatusDescription);
                        cmd.Parameters.AddWithValue("@StatusResponse", trans.StatusResponse);
                        cmd.Parameters.AddWithValue("@DebitRecovery", trans.DebitRecovery);
                        cmd.Parameters.AddWithValue("@CostOfUnits", trans.CostOfUnits);
                        cmd.Parameters.AddWithValue("@PaymentStatus", trans.PaymentStatus);

                        await conn.OpenAsync();
                        await ExecuteOperation(async () =>
                        {
                            cmd.CommandTimeout = 60;
                            object insertedId = await cmd.ExecuteScalarAsync();
                            trans.TransactionDetailsId = Convert.ToInt64(insertedId);
                            return trans;
                        }, "CreateRecordBeforeVend");
                        
                    }
                }

                return await _posManager.DeductBalanceAsync(model.POSId, trans);
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(ex, $"Failed to create transaction record for meter {model?.MeterNumber}");
                throw;
            }
        }


        public async Task CheckPendingTransaction()
        {
            //var excludedTransactionIds = new List<string>
            //{
            //    "354916", "394148", "395656", "397580", "398640", "387952", "388613",
            //    "372554", "401169", "403436", "403915", "401176"
            //};

            var excludedTransactionIds = new List<string>
            {
                "345861", "351093", "362249", "388642", "367536", "382204", "382893",
                "395976", "391870", "392193", "393289", "390218", "392189", "396542",
                "396735", "398247", "398733", "362276", "351468", "359587", "389066",
                "383123", "385344", "389057", "393155", "394014", "396231", "392025",
                "396513", "397948", "390884", "390519", "398625", "398649", "399953",
                "399718", "400108", "401323"
            };

            using (var DbCtx = new VendtechEntities())
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var thresholdTime = now.AddSeconds(-50);
                    var referenceDate = new DateTime(2025, 4, 24);

                    //original transaction list..................

                    //var pendingTrxs = await DbCtx.TransactionDetails
                    //    .Where(t => (t.Status == (int)RechargeMeterStatusEnum.Pending 
                    //    && t.PlatFormId == 1)
                    //             && t.CreatedAt <= thresholdTime
                    //             && t.CreatedAt >= referenceDate)
                    //    .OrderByDescending(d => d.CreatedAt)
                    //    .ToListAsync();

                    //......................................

                    var pendingTrxs = await DbCtx.TransactionDetails
                    .Where(t =>
                        (
                            (t.Status == (int)RechargeMeterStatusEnum.Pending && t.PlatFormId == 1) ||
                            (t.Status == (int)RechargeMeterStatusEnum.Success && t.PaymentStatus != (int)PaymentStatus.Deducted && t.PlatFormId == 1)
                        )
                        && t.CreatedAt <= thresholdTime
                        && t.CreatedAt >= referenceDate
                        && !excludedTransactionIds.Contains(t.TransactionId))
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();


                    for (int i = 0; i < pendingTrxs.Count; i++)
                    {

                        TransactionDetail pendingTrax = pendingTrxs[i];
                        if (pendingTrax == null)
                        {
                            continue;
                        }
                        if (!string.IsNullOrEmpty(pendingTrax.MeterToken1) && pendingTrax.PaymentStatus == (int)PaymentStatus.Deducted)
                        {
                            pendingTrax.Status = (int)RechargeMeterStatusEnum.Success;
                            DbCtx.SaveChanges();
                            continue;
                        }
                        if (!string.IsNullOrEmpty(pendingTrax.MeterToken1) && pendingTrax.PaymentStatus != (int)PaymentStatus.Deducted)
                        {
                            Utilities.LogExceptionToDatabase(new Exception("EdsaTransactionSheduleJob Run on deduction: " + pendingTrax.TransactionId));
                            await _posManager.DeductBalanceAsync(pendingTrax.POSId.Value, pendingTrax);
                            continue;
                        }
                        POS pos;
                        pos = await DbCtx.POS.FirstOrDefaultAsync(p => p.VendorId == pendingTrax.UserId);

                        if (pos == null)
                        {
                            continue;
                        }

                        var requestModel = new RechargeMeterModel
                        {
                            UserId = pendingTrax.UserId,
                            TransactionId = pendingTrax.TransactionId,
                            POSId = pos.POSId
                        };

                        var transaction = await ProcessTransaction(true, requestModel, pendingTrax, true);
                        if (transaction != null && !string.IsNullOrEmpty(transaction.MeterToken1))
                        {
                            Common.PushNotification.Instance
                                .IncludeAdminWidgetSales()
                                .IncludeUserBalanceOnTheWeb(transaction.UserId)
                                .Send();

                            var deviceTokens = DbCtx.TokensManagers.Where(p => p.DeviceToken != null && p.DeviceToken != string.Empty && p.UserId == transaction.UserId).Select(p => new { p.AppType, p.DeviceToken }).ToList().Distinct();
                            var obj = new PushNotificationModel();
                            obj.UserId = transaction.UserId;
                            obj.Id = transaction.TransactionDetailsId;
                            obj.Title = "Pending Meter recharge successfully";
                            obj.Message = $"Your pending transaction of SLe {Utilities.FormatAmount(transaction.Amount)} has successfully been processed. PIN: {transaction.MeterToken1} {transaction.MeterToken2??""} {transaction.MeterToken3 ?? ""}";
                            obj.NotificationType = NotificationTypeEnum.MeterRecharge;
                            foreach (var item in deviceTokens)
                            {
                                obj.DeviceToken = item.DeviceToken;
                                obj.DeviceType = item.AppType.Value;
                                Common.PushNotification.PushNotificationToMobile(obj);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }


        private static VtechElectricitySaleRequest Buid_new_request_object(RechargeMeterModel model)
        {
            return new VtechElectricitySaleRequest
            {
                Amount = model.Amount,
                MeterNumber = model.MeterNumber,
                TransactionId = model.TransactionId.ToString(),
                Simulate = ""
            };
        }

        private static VtechElectricitySaleStatus Buid_new_status_object(RechargeMeterModel model)
        {
            return new VtechElectricitySaleStatus
            {
                TransactionId = model.TransactionId.ToString(),
            };
        }


        private async Task<VtechExtensionResponse> MakeRechargeRequest(RechargeMeterModel model, TransactionDetail transactionDetail)
        {
            string url = WebConfigurationManager.AppSettings["VendtechExtentionServer"].ToString() + "sales/v1/buy";
            VtechElectricitySaleRequest request_model = Buid_new_request_object(model);
            var json = JsonConvert.SerializeObject(request_model);

            var client = new ReliableHttpClient();
            string strings_result = await client.SendPostRequestAsync(url, json);

            transactionDetail.Request = JsonConvert.SerializeObject(request_model);
            transactionDetail.Response = strings_result;

            VtechExtensionResponse response = JsonConvert.DeserializeObject<VtechExtensionResponse>(strings_result);
            return response;
        }

        private async Task<VtechExtensionResponse> QueryStatusRequest(RechargeMeterModel model, TransactionDetail transactionDetail)
        {
            string url = WebConfigurationManager.AppSettings["VendtechExtentionServer"].ToString() + "sales/v1/status";
            VtechElectricitySaleStatus request_model = Buid_new_status_object(model);
            var json = JsonConvert.SerializeObject(request_model);

            var client = new ReliableHttpClient();
            string strings_result = await client.SendPostRequestAsync(url, json);

            transactionDetail.Request = JsonConvert.SerializeObject(request_model);
            transactionDetail.Response = strings_result;

            VtechExtensionResponse response = JsonConvert.DeserializeObject<VtechExtensionResponse>(strings_result);
            return response;
        }
    }
}