using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Web.Configuration;
using System;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.DAL;
using System.Data.Entity;
using VendTech.BLL.Common;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Net.Http;
using System.Data.Entity.Infrastructure;
using System.Transactions;
using Polly;
using System.Data.SqlClient;
using Org.BouncyCastle.Asn1.Ocsp;

namespace VendTech.BLL.Managers
{
    public class VendtechExtensionSales : BaseManager, IVendtechExtensionSales
    {
        private readonly VendtechEntities _context;
        private readonly TransactionIdGenerator idGenerator;
        private readonly IPOSManager _posManager;
        private readonly IAsyncPolicy _retryPolicy;

        public VendtechExtensionSales(VendtechEntities context, TransactionIdGenerator idGenerator, IPOSManager posManager)
        {
            _context = context;
            this.idGenerator = idGenerator;
            _posManager = posManager;
            
            _retryPolicy = Policy
                .Handle<DbUpdateException>()
                .Or<DbUpdateConcurrencyException>()
                .Or<SqlException>()
                .WaitAndRetryAsync(5, retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

        async Task<ReceiptModel> IVendtechExtensionSales.RechargeFromVendtechExtension(RechargeMeterModel model)
        {
            var response = new ReceiptModel { ReceiptStatus = new ReceiptStatus() };
            var trax = new TransactionDetail();

            var user = await _context.Users.FirstOrDefaultAsync(p => p.UserId == model.UserId);
            var pos = await _context.POS.FirstOrDefaultAsync(p => p.POSId == model.POSId);
            var meter = await _context.Meters.FirstOrDefaultAsync(d => d.MeterId == model.MeterId);
            var validationResult = model.validateRequest(user, pos);

            if (validationResult != "clear")
            {
                response.ReceiptStatus.Status = "unsuccessful";
                response.ReceiptStatus.Message = validationResult;
                return response;
            }

            model.UpdateRequestModel(meter == null ? "" : meter?.Number, pos.POSId);

            var pendingTrx = await getLastMeterPendingTransaction(model.MeterNumber);

            var isDuplicate = model.IsRequestADuplicate(pendingTrx);

            var transaction = isDuplicate ? pendingTrx : trax;

            try
            {
                trax = await ProcessTransaction(isDuplicate, model, transaction);
                var receipt = BuildRceipt(isDuplicate ? pendingTrx : trax);
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
                            throw new ArgumentException(vendResponseResult.FailedResponse.ErrorMessage);
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
                            throw new ArgumentException(vendResponse.Message);
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
            catch(ArgumentException)
            {
                throw;  
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(
                    new Exception($"ProcessTransactionException for {model.TransactionId} || {transactionDetail.TransactionId}", ex),
                    $"Source: {ex?.Source ?? "Unknown"}, Inner: {ex?.InnerException?.Message ?? "None"}"
                );
                 throw;

            }

        }
        private async Task ProcessPending(VtechExtensionResponse vendResponse, 
            VendtechExtSalesResult vendResponseResult,
            TransactionDetail transactionDetail, RechargeMeterModel model)
        {
            int count = 0;
            do
            {
                vendResponse = await QueryStatusRequest(model, transactionDetail);
                vendResponseResult = vendResponse.Result;

                transactionDetail.VendStatus = vendResponseResult.FailedResponse.ErrorMessage;
                transactionDetail.VendStatusDescription = vendResponseResult?.FailedResponse?.ErrorDetail;
                transactionDetail.QueryStatusCount = count;
                transactionDetail.StatusResponse = JsonConvert.SerializeObject(vendResponseResult);
                count += 1;
                
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    await _retryPolicy.ExecuteAsync(async () => await _context.SaveChangesAsync());
                    transaction.Complete();
                }
            } while (vendResponse.Status.ToLower() == "pending");
        }

        private async Task ProcessFailed(VtechExtensionResponse vendResponse,
            VendtechExtSalesResult vendResponseResult,
            TransactionDetail transactionDetail)
        {
            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                vendResponseResult = vendResponse?.Result;

                transactionDetail.VendStatus = vendResponseResult.FailedResponse.ErrorMessage;
                transactionDetail.VendStatusDescription = vendResponseResult?.FailedResponse?.ErrorDetail;
                transactionDetail.Status = (int)RechargeMeterStatusEnum.Failed;
                transactionDetail.Finalised = true;
                transactionDetail.PaymentStatus = (int)PaymentStatus.Failed;
                transactionDetail.VendStatus = vendResponseResult?.Status;
                transactionDetail.VendStatusDescription = vendResponseResult?.Status;

                await _retryPolicy.ExecuteAsync(async () => await _context.SaveChangesAsync());
                transaction.Complete();
            }
            
            ReadErrorMessage(vendResponse.Message, vendResponse.Result.Code, transactionDetail);
        }
        private async Task ProcessSuccess(VtechExtensionResponse vendResponse,
            VendtechExtSalesResult vendResponseResult,
            TransactionDetail transactionDetail, long posId)
        {
            vendResponseResult = vendResponse?.Result;
            POS pos = transactionDetail.User.POS.FirstOrDefault(d => d.POSId == posId);
            transactionDetail = await UpdateTransactionOnSuccess(vendResponseResult, transactionDetail, pos);

            Common.PushNotification.Instance
                    .IncludeAdminWidgetSales()
                    .IncludeUserBalanceOnTheWeb(transactionDetail.UserId)
                    .Send();
        }
        private void ReadErrorMessage(string message, int code, TransactionDetail tx)
        {
            FlagTransaction(tx, RechargeMeterStatusEnum.Failed);
            if (message == "The request timed out with the Ouc server.")
            {
                throw new ArgumentException(message);
            }
            if (code == 4514)
            {
                DisablePlatform(PlatformTypeEnum.ELECTRICITY);
                NotifyAdmin();
                throw new ArgumentException("Error: Vending is disabled");
            }

            if (code == 4094)
            {
                DisablePlatform(PlatformTypeEnum.ELECTRICITY);
                NotifyAdmin();
                throw new ArgumentException("Error: Vending is disabled");
            }

            if (message == "InCMS-BL-CO000846. The amount is too low for recharge")
            {
                throw new ArgumentException("The amount is too low for recharge");
            }

            if (message == "Unexpected error in OUC VendVoucher")
            {
                throw new ArgumentException(message);
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

        private void FlagTransaction(TransactionDetail tx, RechargeMeterStatusEnum status)
        {
            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                tx.Status = (int)status;
                _retryPolicy.ExecuteAsync(async () => await _context.SaveChangesAsync()).GetAwaiter().GetResult();
                transaction.Complete();
            }
        }
        private void DisablePlatform(PlatformTypeEnum pl)
        {
            var plt = _context.Platforms.FirstOrDefault(d => d.PlatformType == (int)pl);
            if (plt != null)
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    plt.DisablePlatform = true;
                    _retryPolicy.ExecuteAsync(async () => await _context.SaveChangesAsync()).GetAwaiter().GetResult();
                    transaction.Complete();
                }
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
            Utilities.SendEmail("vblell@gmail.com", "[URGENT] VENDTECH OUT OF FUNDS", body);

        }

        private async Task<TransactionDetail> getLastMeterPendingTransaction(string MeterNumber) =>
           await _context.TransactionDetails.Where(p => p.Status ==
           (int)RechargeMeterStatusEnum.Pending && p.MeterNumber1.ToLower() == MeterNumber.ToLower()).FirstOrDefaultAsync();

        public async Task<ReceiptModel> GetStatusFromVendtechExtension(string trxId, long userId)
        {
            var response = new ReceiptModel { ReceiptStatus = new ReceiptStatus { Status = "", Message = "" } };
            var pendingTrax = _context.TransactionDetails.FirstOrDefault(e => e.TransactionId == trxId);
            if (userId == 0)
                userId = pendingTrax.UserId;

            var pos = await _context.POS.FirstOrDefaultAsync(p => p.VendorId == userId);
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
                TransactionId = Convert.ToInt64(pendingTrax.TransactionId),
                POSId = pos.POSId
            };

            var verifiedTrax = await ProcessTransaction(true, requestModel, pendingTrax, true);

            if (verifiedTrax != null)
            {
                var receipt = BuildRceipt(verifiedTrax);
                receipt.ShouldShowSmsButton = (bool)verifiedTrax.POS.WebSms;
                receipt.ShouldShowPrintButton = (bool)verifiedTrax.POS.WebPrint;
                receipt.mobileShowSmsButton = (bool)verifiedTrax.POS.PosSms;
                receipt.mobileShowPrintButton = (bool)verifiedTrax.POS.PosPrint;
                receipt.ReceiptStatus.Status = "successful";
                return receipt;
            }

            return response;
        }

        public ReceiptModel BuildRceipt(TransactionDetail model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            if (string.IsNullOrEmpty(model.MeterToken1))
                throw new ArgumentException("Did not result in a vend");

            if (model.POS == null) 
                model.POS = new POS();

            try
            {
                var receipt = new ReceiptModel
                {
                    AccountNo = model?.AccountNumber ?? string.Empty,
                    POS = model?.POS?.SerialNumber ?? string.Empty,
                    CustomerName = model?.Customer ?? string.Empty,
                    ReceiptNo = model?.ReceiptNumber ?? string.Empty,
                    Address = model?.CustomerAddress ?? string.Empty,
                    Tarrif = !string.IsNullOrEmpty(model.Tariff) ? Utilities.FormatAmount(Convert.ToDecimal(model.Tariff)) : "0",
                    DeviceNumber = model?.MeterNumber1 ?? string.Empty,
                    DebitRecovery = Convert.ToDecimal(model?.DebitRecovery ?? "0"),
                    Amount = FormatAmount(model?.TenderedAmount ?? 0),
                    Charges = !string.IsNullOrEmpty(model.ServiceCharge) ? Utilities.FormatAmount(Convert.ToDecimal(model.ServiceCharge)) : "0",
                    Commission = "0.00",
                    Unit = !string.IsNullOrEmpty(model.Units) ? Utilities.FormatAmount(Convert.ToDecimal(model.Units)) : "0",
                    UnitCost = !string.IsNullOrEmpty(model.CostOfUnits) ? Utilities.FormatAmount(Convert.ToDecimal(model.CostOfUnits)) : "0",
                    SerialNo = model?.SerialNumber ?? string.Empty,
                    Pin1 = Utilities.FormatThisToken(model?.MeterToken1) ?? string.Empty,
                    Pin2 = Utilities.FormatThisToken(model?.MeterToken2) ?? string.Empty,
                    Pin3 = Utilities.FormatThisToken(model?.MeterToken3) ?? string.Empty,
                    Discount = "0",
                    Tax = !string.IsNullOrEmpty(model.TaxCharge) ? Utilities.FormatAmount(Convert.ToDecimal(model.TaxCharge)) : "0",
                    TransactionDate = model?.CreatedAt.ToString("dd/MM/yyyy hh:mm") ?? string.Empty,
                    VendorId = model?.User?.Vendor ?? string.Empty,
                    EDSASerial = model?.SerialNumber ?? string.Empty,
                    VTECHSerial = model?.TransactionId ?? string.Empty,
                    PlatformId = model?.PlatFormId ?? 0,
                    ShouldShowSmsButton = model?.POS?.WebSms ?? false,
                    ShouldShowPrintButton = model?.POS?.WebPrint ?? false,
                    mobileShowSmsButton = model?.POS?.PosSms ?? false,
                    mobileShowPrintButton = model?.POS?.PosPrint ?? false,
                    CurrentBallance = model?.POS?.Balance ?? 0
                };

                return receipt;
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(ex, $"Error building receipt for transaction {model?.TransactionId}");
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
            var deviceTokens = user.TokensManagers.Where(p => p.DeviceToken != null && p.DeviceToken != string.Empty).Select(p => new { p.AppType, p.DeviceToken }).ToList().Distinct(); ;
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
         
       
        private async Task<TransactionDetail> UpdateTransactionOnSuccess(VendtechExtSalesResult response_data, TransactionDetail trans, POS pos)
        {
            try
            {
                if (response_data?.SuccessResponse?.Voucher == null)
                    throw new ArgumentNullException(nameof(response_data.SuccessResponse.Voucher));

                var voucher = response_data.SuccessResponse.Voucher;
                
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    // Update transaction details
                    trans.CostOfUnits = voucher.CostOfUnits ?? "0";
                    trans.MeterToken1 = voucher.MeterToken1?.ToString() ?? string.Empty;
                    trans.MeterToken2 = voucher.MeterToken2?.ToString() ?? string.Empty;
                    trans.MeterToken3 = voucher?.MeterToken3?.ToString() ?? string.Empty;
                    trans.Status = (int)RechargeMeterStatusEnum.Success;
                    trans.POSId = pos?.POSId ?? throw new ArgumentNullException(nameof(pos.POSId));
                    trans.UserId = pos.VendorId ?? throw new ArgumentNullException(nameof(pos.VendorId));
                    trans.AccountNumber = voucher?.AccountNumber ?? string.Empty;
                    trans.Customer = voucher?.Customer ?? string.Empty;
                    trans.ReceiptNumber = voucher?.ReceiptNumber ?? string.Empty;
                    trans.SerialNumber = response_data.SuccessResponse.VendtechTransactionId ?? string.Empty;
                    trans.RTSUniqueID = response_data.SuccessResponse.VendtechTransactionId;
                    trans.ServiceCharge = voucher?.ServiceCharge ?? "0";
                    trans.CurrentDealerBalance = Convert.ToDecimal(response_data?.SuccessResponse?.WalleBalance ?? "0");
                    trans.Tariff = voucher?.Tariff ?? "0";
                    trans.TaxCharge = voucher?.TaxCharge ?? "0";
                    trans.Units = voucher?.Units ?? "0";
                    trans.CustomerAddress = voucher?.CustomerAddress ?? string.Empty;
                    trans.Finalised = true;
                    trans.VProvider = string.Empty;
                    trans.StatusRequestCount = 0;
                    trans.Sold = true;
                    trans.VendStatusDescription = "success";
                    trans.VoucherSerialNumber = response_data?.SuccessResponse?.Voucher.VoucherSerialNumber ?? string.Empty;
                    trans.VendStatus = string.Empty;

                    // Save changes with retry
                    await _retryPolicy.ExecuteAsync(async () => await _context.SaveChangesAsync());

                    // Deduct balance
                    trans = await _posManager.DeductBalanceAsync(pos.POSId, trans);

                    transaction.Complete();
                }

                return trans;
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
            var trans = new TransactionDetail();
            trans.PlatFormId = (int)model.PlatformId;
            trans.UserId = model.UserId;
            trans.MeterId = model.MeterId;
            trans.POSId = model.POSId;
            trans.MeterNumber1 = model.MeterNumber;
            trans.MeterToken1 = model.MeterToken1;
            trans.Amount = model.Amount;
            trans.IsDeleted = false;
            trans.Status = (int)RechargeMeterStatusEnum.Pending;
            trans.CreatedAt = DateTime.UtcNow;
            trans.AccountNumber = "";
            trans.CurrentDealerBalance = 00;
            trans.Customer = "";
            trans.ReceiptNumber = "";
            trans.RequestDate = DateTime.UtcNow;
            trans.RTSUniqueID = "00";
            trans.SerialNumber = "";
            trans.ServiceCharge = "";
            trans.Tariff = "";
            trans.TaxCharge = "";
            trans.TenderedAmount = model.Amount;
            trans.TransactionAmount = model.Amount;
            trans.Units = "";
            trans.VProvider = "";
            trans.Finalised = false;
            trans.StatusRequestCount = 0;
            trans.Sold = false;
            trans.DateAndTimeSold = "";
            trans.DateAndTimeFinalised = "";
            trans.DateAndTimeLinked = "";
            trans.VoucherSerialNumber = "";
            trans.VendStatus = "";
            trans.VendStatusDescription = "";
            trans.StatusResponse = "";
            trans.DebitRecovery = "0";
            trans.CostOfUnits = "0";
            trans.PaymentStatus = (int)PaymentStatus.Pending;
            string transactionId = await idGenerator.GenerateNewTransactionId();
            trans.TransactionId = transactionId;

            try
            {
                using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    _context.TransactionDetails.Add(trans);
                    await _retryPolicy.ExecuteAsync(async () => await _context.SaveChangesAsync());
                    transaction.Complete();
                }
                return trans;
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(ex, $"Failed to create transaction record for meter {model?.MeterNumber}");
                throw;
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
            try
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
            catch (HttpRequestException)
            {
                throw;
            }
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