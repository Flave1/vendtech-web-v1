using Newtonsoft.Json;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Net;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web;
using System;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.DAL;
using System.Data.Entity;
using VendTech.BLL.Common;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using VendTech.BLL.PlatformApi;
using System.Data.Entity.Validation;

namespace VendTech.BLL.Managers
{
    public class VendtechExtensionSales : BaseManager, IVendtechExtensionSales
    {
        private readonly VendtechEntities _context;
        private HttpClient _client;
        private readonly TransactionIdGenerator idGenerator;
        public VendtechExtensionSales(VendtechEntities context, TransactionIdGenerator idGenerator)
        {
            _client = new HttpClient()
            {
                Timeout = TimeSpan.FromMinutes(2)
            };
            _context = context;
            this.idGenerator = idGenerator;
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

            model.UpdateRequestModel(meter == null ? "" : meter?.Number);

            var pendingTrx = await getLastMeterPendingTransaction(model.MeterNumber);

            var isDuplicate = model.IsRequestADuplicate(pendingTrx);

            var transaction = isDuplicate ? pendingTrx : trax;

            try
            {
                trax = await ProcessTransaction(isDuplicate, model, transaction);
            }
            catch (ArgumentException ex)
            {
                response.ReceiptStatus.Message = ex.Message;
                response.ReceiptStatus.Status = "unsuccessful";
                return response;
            }
            catch (Exception ex)
            {
                response.ReceiptStatus.Message = ex.Message;
                response.ReceiptStatus.Status = "unsuccessful";
                return response;
            }

            var receipt = BuildRceipt(isDuplicate ? pendingTrx : trax);
            PushNotification(user, model, trax.TransactionDetailsId);

            return receipt;
        }

        public async Task<TransactionDetail> ProcessTransaction(bool isDuplicate, RechargeMeterModel model,
            TransactionDetail transactionDetail, bool treatAsPending = false, bool billVendor = true)
        {
            VtechExtensionResponse vendResponse = null;
            VendtechExtSalesResult vendResponseResult = new VendtechExtSalesResult();
            if (!isDuplicate)
            {
                if (!treatAsPending)
                    transactionDetail = await CreateRecordBeforeVend(model);

                model.UpdateRequestModel(transactionDetail);

                vendResponse = await MakeRechargeRequest(model, transactionDetail);

                if (vendResponse == null)
                {
                    Utilities.LogExceptionToDatabase(new Exception($"{vendResponse}"));
                    throw new ArgumentException("Unable to process transaction");
                }

                vendResponseResult = vendResponse?.Result;

                if (vendResponse.Status.ToLower() == "pending")
                {
                    int count = 0;
                    do
                    {
                        vendResponse = await QueryStatusRequest(model, transactionDetail);
                        vendResponseResult = vendResponse.Result;

                        transactionDetail.VendStatus = vendResponseResult.FailedResponse.ErrorMessage;
                        transactionDetail.VendStatusDescription = vendResponseResult?.FailedResponse?.ErrorDetail;
                        transactionDetail.QueryStatusCount = count;
                        count += 1;
                        _context.TransactionDetails.AddOrUpdate(transactionDetail);
                        await _context.SaveChangesAsync();
                        ReadErrorMessage(vendResponse.Message, transactionDetail);
                    } while (vendResponse.Status.ToLower() == "pending");
                }
                else if (vendResponse.Status.ToLower() != "success")
                {
                    transactionDetail.VendStatus = vendResponseResult.FailedResponse.ErrorMessage;
                    transactionDetail.VendStatusDescription = vendResponseResult?.FailedResponse?.ErrorDetail;
                    await UpdateTransactionOnFailed(vendResponse?.Result, transactionDetail);
                    _context.TransactionDetails.AddOrUpdate(transactionDetail);
                    await _context.SaveChangesAsync();
                    ReadErrorMessage(vendResponse.Message, transactionDetail);
                }

                vendResponseResult = vendResponse?.Result;
                if (vendResponse?.Result?.SuccessResponse == null)
                {
                    Utilities.LogExceptionToDatabase(new Exception($"{vendResponse}"));
                    await UpdateTransactionOnFailed(vendResponse?.Result, transactionDetail);
                    var msg = vendResponse?.Result?.FailedResponse?.ErrorMessage;
                    throw new ArgumentException(msg);
                }

                POS pos = transactionDetail.User.POS.FirstOrDefault(d => d.POSId == transactionDetail.POSId);
                transactionDetail = await UpdateTransactionOnSuccess(vendResponseResult, transactionDetail, pos);

                Common.PushNotification.Instance
                    .IncludeAdminWidgetSales()
                    .IncludeUserBalanceOnTheWeb(transactionDetail.UserId)
                    .Send();

                return transactionDetail;
            }
            else
            {
                model.UpdateRequestModel(transactionDetail);
                vendResponse = await QueryStatusRequest(model, transactionDetail);
                
                if(vendResponse?.Result == null)
                {
                    Utilities.LogExceptionToDatabase(new Exception($"{vendResponse}"));
                    throw new ArgumentException("Unable to process request");
                }

                vendResponseResult = vendResponse.Result;
                if (vendResponse?.Result?.SuccessResponse == null)
                {
                    Utilities.LogExceptionToDatabase(new Exception($"{vendResponse}"));
                    await UpdateTransactionOnFailed(vendResponse?.Result, transactionDetail);
                    throw new ArgumentException(vendResponse?.Result.FailedResponse.ErrorMessage);
                }

                POS pos = transactionDetail.User.POS.FirstOrDefault(d => d.POSId == transactionDetail.POSId);
                transactionDetail = await UpdateTransactionOnSuccess(vendResponseResult, transactionDetail, pos);

                Common.PushNotification.Instance
                        .IncludeAdminWidgetSales()
                        .IncludeUserBalanceOnTheWeb(transactionDetail.UserId)
                        .Send();
                return transactionDetail;
            }
        }

        private void ReadErrorMessage(string message, TransactionDetail tx)
        {
            if (message == "The request timed out with the Ouc server.")
            {
                FlagTransaction(tx, RechargeMeterStatusEnum.Failed);
                throw new ArgumentException(message);
            }
            if (message == "Error: Vending is disabled")
            {
                DisablePlatform(PlatformTypeEnum.ELECTRICITY);
                FlagTransaction(tx, RechargeMeterStatusEnum.Failed);
                NotifyAdmin();
                throw new ArgumentException(message);
            }

            if (message == "-9137 : InCMS-BL-CB001607. Purchase not allowed, not enought vendor balance")
            {
                DisablePlatform(PlatformTypeEnum.ELECTRICITY);
                FlagTransaction(tx, RechargeMeterStatusEnum.Failed);
                NotifyAdmin();
                throw new ArgumentException("Due to some technical resolutions involving EDSA, the system is unable to vend");
            }

            if (message == "InCMS-BL-CO000846. The amount is too low for recharge")
            {
                FlagTransaction(tx, RechargeMeterStatusEnum.Failed);
                throw new ArgumentException("The amount is too low for recharge");
            }

            if (message == "Unexpected error in OUC VendVoucher")
            {
                FlagTransaction(tx, RechargeMeterStatusEnum.Pending);
                throw new ArgumentException(message);
            }

            if (message == "CB001600 : InCMS-BL-CB001600. Error serial number, contracted service not found or not active.")
            {
                FlagTransaction(tx, RechargeMeterStatusEnum.Failed);
                throw new ArgumentException("Error serial number, contracted service not found or not active");
            }
            if (message == "There was an error when determining if the request for the given meter number can be processed.")
            {
                FlagTransaction(tx, RechargeMeterStatusEnum.Failed);
                throw new ArgumentException(message);
            }
            if (message == "Input string was not in a correct format.")
            {
                FlagTransaction(tx, RechargeMeterStatusEnum.Failed);
                throw new ArgumentException("Amount tendered is too low");
            }
            if (message == "-47 : InCMS-BL-CB001273. Error, purchase units less than minimum.")
            {
                FlagTransaction(tx, RechargeMeterStatusEnum.Failed);
                throw new ArgumentException("Purchase units less than minimum.");
            }
            if(message == "The specified TransactionID already exists for this terminal.")
            {

                FlagTransaction(tx, RechargeMeterStatusEnum.Failed);
                throw new ArgumentException("Please try again!!");
            }
        }

        private void FlagTransaction(TransactionDetail tx, RechargeMeterStatusEnum status)
        {
            tx.Status = (int)status;
            _context.TransactionDetails.AddOrUpdate(tx);
            _context.SaveChanges();
        }
        private void DisablePlatform(PlatformTypeEnum pl)
        {
            var plt = _context.Platforms.FirstOrDefault(d => d.PlatformType == (int)pl);
            if (plt != null)
            {
                plt.DisablePlatform = true;
                _context.Platforms.AddOrUpdate(plt);
                _context.SaveChanges();
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
           await _context.TransactionDetails.OrderByDescending(p => p.TransactionId).FirstOrDefaultAsync(p => p.Status ==
           (int)RechargeMeterStatusEnum.Pending && p.MeterNumber1.ToLower() == MeterNumber.ToLower());

        async Task<Dictionary<string, IcekloudQueryResponse>> QueryVendStatus(RechargeMeterModel model, TransactionDetail transDetail)
        {

            Utilities.LogExceptionToDatabase(new Exception($"QueryVendStatus starts at {DateTime.UtcNow} for traxId {model.TransactionId}"), $"model : {JsonConvert.SerializeObject(model)}");
            Dictionary<string, IcekloudQueryResponse> response = new Dictionary<string, IcekloudQueryResponse>();
            try
            {
                var queryRequest = model.StackStatusRequestModel(model);
                var url = WebConfigurationManager.AppSettings["IcekloudURL"].ToString();

                var icekloudResponse = await _client.PostAsJsonAsync(url, queryRequest);

                var stringsResult = await icekloudResponse.Content.ReadAsStringAsync();

                var statusResponse = JsonConvert.DeserializeObject<IcekloudQueryResponse>(stringsResult);

                transDetail.Request = JsonConvert.SerializeObject(queryRequest);
                transDetail.Response = stringsResult;

                if (statusResponse.Content.StatusDescription == "The specified Transaction does not exist.")
                {
                    response.Add("failed", statusResponse);
                    Utilities.LogExceptionToDatabase(new Exception($"QueryVendStatus failed 1 ends at {DateTime.UtcNow} for traxId {model.TransactionId}"), $"statusResponse: {JsonConvert.SerializeObject(statusResponse)}");
                    return response;
                }
                else if (statusResponse.Content.StatusDescription == "The specified Transaction does not exist.")
                {
                    response.Add("failed", statusResponse);
                    Utilities.LogExceptionToDatabase(new Exception($"QueryVendStatus failed 2 ends at {DateTime.UtcNow} for traxId {model.TransactionId}"), $"statusResponse :{JsonConvert.SerializeObject(statusResponse)}");
                    return response;
                }
                else if (statusResponse.Content.StatusDescription == "Transaction completed with error")
                {
                    _context.SaveChanges();
                    response.Add("failed", statusResponse);
                    Utilities.LogExceptionToDatabase(new Exception($"QueryVendStatus failed 3 ends at {DateTime.UtcNow} for traxId {model.TransactionId}"), $"statusResponse :{JsonConvert.SerializeObject(statusResponse)}");
                    return response;
                }
                else if (!statusResponse.Content.Finalised && statusResponse.Content.StatusRequestCount <= 5)
                {
                    Utilities.LogExceptionToDatabase(new Exception($"QueryVendStatus 3 ends at {DateTime.UtcNow} for traxId {model.TransactionId}"), $"statusResponse : {JsonConvert.SerializeObject(statusResponse)}");
                    return await QueryVendStatus(model, transDetail);
                }
                else
                {
                    transDetail.QueryStatusCount = (int)statusResponse.Content.StatusRequestCount;
                    if (string.IsNullOrEmpty(statusResponse.Content.VoucherPin))
                    {
                        Utilities.LogExceptionToDatabase(new Exception($"QueryVendStatus 4 ends at {DateTime.UtcNow} for traxId {model.TransactionId}"), $"statusResponse: {JsonConvert.SerializeObject(statusResponse)}");
                        await _context.SaveChangesAsync();
                        response.Add("failed", statusResponse);
                        return response;
                    }
                    else
                    {
                        await _context.SaveChangesAsync();
                        Utilities.LogExceptionToDatabase(new Exception($"QueryVendStatus 5 ends at {DateTime.UtcNow} for traxId {model.TransactionId}"), $"statusResponse: {JsonConvert.SerializeObject(statusResponse)}");
                        response.Add("success", statusResponse);
                        return response;
                    }
                }
            }
            catch (DbUpdateException ex)
            {
                Utilities.LogExceptionToDatabase(new Exception($"QueryVendStatus 6 ends for DbUpdateException at {DateTime.UtcNow} for traxId {model.TransactionId}", ex), $"DbUpdateException: {JsonConvert.SerializeObject(ex)}");
                response.Add("failed", null);
                return response;
            }
            catch (HttpException ex)
            {
                Utilities.LogExceptionToDatabase(new Exception($"QueryVendStatus 6 ends at {DateTime.UtcNow} for traxId {model.TransactionId}", ex), $"HttpException: {JsonConvert.SerializeObject(model)}");
                response.Add("failed", null);
                return response;
            }
            catch (NullReferenceException ex)
            {
                Utilities.LogExceptionToDatabase(ex, $"model: {JsonConvert.SerializeObject(model)}");
                response.Add("failed", null);
                return response;
            }
            catch (WebException ex)
            {
                Utilities.LogExceptionToDatabase(new Exception($"QueryVendStatus  WebException 2 ends at {DateTime.UtcNow} for traxId {model.TransactionId}"), $"Exception: {ex.ToString()}");
                response.Add("failed", null);
                return response;
            }
            catch (Exception)
            {
                Utilities.LogExceptionToDatabase(new Exception($"QueryVendStatus 8 ends at {DateTime.UtcNow} for traxId {model.TransactionId}"), $"Unexpected Exception");
                response.Add("failed", null);
                return response;
            }
        }

        public async Task<ReceiptModel> GetStatusFromVendtechExtension(string trxId, bool billVendor)
        {
            var response = new ReceiptModel { ReceiptStatus = new ReceiptStatus { Status = "", Message = "" } };
            try
            {
                var pendingTrax = _context.TransactionDetails.FirstOrDefault(e => e.TransactionId == trxId);

                if (pendingTrax == null)
                {
                    response.ReceiptStatus.Status = "unsuccessful";
                    response.ReceiptStatus.Message = "Unable to find transaction";
                    return response;
                }

                var requestModel = new RechargeMeterModel
                {
                    UserId = pendingTrax.UserId,
                    TransactionId = Convert.ToInt64(pendingTrax.TransactionId),
                };

                var verifiedTrax = await ProcessTransaction(true, requestModel, pendingTrax, true, billVendor);

                if (verifiedTrax != null)
                {
                    var receipt = BuildRceipt(verifiedTrax);
                    receipt.ShouldShowSmsButton = (bool)verifiedTrax.POS.WebSms;
                    receipt.ShouldShowPrintButton = (bool)verifiedTrax.POS.WebPrint;
                    receipt.mobileShowSmsButton = (bool)verifiedTrax.POS.PosSms;
                    receipt.mobileShowPrintButton = (bool)verifiedTrax.POS.PosPrint;
                    return receipt;
                }

                return response;
            }
            catch (ArgumentException ex)
            {
                response.ReceiptStatus.Status = "unsuccessful";
                response.ReceiptStatus.Message = ex.Message;
                return response;
            }
        }

        public ReceiptModel BuildRceipt(TransactionDetail model)
        {
            if (model.POS == null) model.POS = new POS();
            var receipt = new ReceiptModel();
            receipt.AccountNo = model?.AccountNumber;
            receipt.POS = model?.POS?.SerialNumber;
            receipt.CustomerName = model?.Customer;
            receipt.ReceiptNo = model?.ReceiptNumber;
            receipt.Address = model?.CustomerAddress;
            receipt.Tarrif = model.Tariff != "" ? Utilities.FormatAmount(Convert.ToDecimal(model.Tariff)) : "0";
            receipt.DeviceNumber = model?.MeterNumber1;
            receipt.DebitRecovery = Convert.ToDecimal(model.DebitRecovery);
            var amt = model?.TenderedAmount.ToString("N");
            receipt.Amount = amt.Contains('.') ? amt.TrimEnd('0').TrimEnd('.') : amt;
            receipt.Charges = model.ServiceCharge != "" ? Utilities.FormatAmount(Convert.ToDecimal(model.ServiceCharge)) : "0";
            receipt.Commission = string.Format("{0:N0}", 0.00);
            receipt.Unit = model.Units != "" ? Utilities.FormatAmount(Convert.ToDecimal(model.Units)) : "0";
            receipt.UnitCost = model.CostOfUnits != "" ? Utilities.FormatAmount(Convert.ToDecimal(model.CostOfUnits)) : "0";
            receipt.SerialNo = model?.SerialNumber;
            receipt.Pin1 = Utilities.FormatThisToken(model?.MeterToken1) ?? string.Empty;
            receipt.Pin2 = Utilities.FormatThisToken(model?.MeterToken2) ?? string.Empty;
            receipt.Pin3 = Utilities.FormatThisToken(model?.MeterToken3) ?? string.Empty;
            receipt.Discount = string.Format("{0:N0}", 0);
            receipt.Tax = model.TaxCharge != "" ? Utilities.FormatAmount(Convert.ToDecimal(model.TaxCharge)) : "0";
            receipt.TransactionDate = model.CreatedAt.ToString("dd/MM/yyyy hh:mm");
            receipt.VendorId = model.User?.Vendor;
            receipt.EDSASerial = model.SerialNumber;
            receipt.VTECHSerial = model.TransactionId;
            receipt.PlatformId = model.PlatFormId;

            receipt.ShouldShowSmsButton = (bool)model.POS.WebSms;
            receipt.ShouldShowPrintButton = (bool)model.POS.WebPrint;
            receipt.mobileShowSmsButton = (bool)model.POS.PosSms;
            receipt.mobileShowPrintButton = (bool)model.POS.PosPrint;
            receipt.CurrentBallance = model?.POS?.Balance ?? 0;
            return receipt;
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

        private async Task UpdateTransactionOnFailed(VendtechExtSalesResult response_data, TransactionDetail trans)
        {
            trans.Status = (int)RechargeMeterStatusEnum.Failed;
            trans.Finalised = false;
            trans.VendStatus = response_data?.Status;
            trans.VendStatusDescription = response_data?.Status;
            trans.StatusResponse = JsonConvert.SerializeObject(response_data);

            await _context.SaveChangesAsync();
            return;

        }
        private async Task<TransactionDetail> UpdateTransactionOnSuccess(VendtechExtSalesResult response_data, TransactionDetail trans, POS pos)
        {
            try
            {
                var voucher = response_data.SuccessResponse.Voucher;
                trans.CostOfUnits = voucher.CostOfUnits ?? "0";
                trans.MeterToken1 = voucher.MeterToken1?.ToString() ?? string.Empty;
                trans.MeterToken2 = voucher.MeterToken2?.ToString() ?? string.Empty;
                trans.MeterToken3 = voucher?.MeterToken3?.ToString() ?? string.Empty;
                trans.Status = (int)RechargeMeterStatusEnum.Success;
                trans.AccountNumber = voucher?.AccountNumber ?? string.Empty;
                trans.Customer = voucher?.Customer ?? string.Empty;
                trans.ReceiptNumber = voucher?.ReceiptNumber ?? string.Empty;
                trans.SerialNumber = response_data.SuccessResponse.VendtechTransactionId ?? string.Empty;
                trans.RTSUniqueID = response_data.SuccessResponse.VendtechTransactionId;
                trans.ServiceCharge = voucher?.ServiceCharge;
                trans.CurrentDealerBalance = Convert.ToDecimal(response_data?.SuccessResponse?.WalleBalance?? "0");
                trans.Tariff = voucher?.Tariff;
                trans.TaxCharge = voucher?.TaxCharge;
                trans.Units = voucher?.Units;
                trans.CustomerAddress = voucher?.CustomerAddress;
                trans.Finalised = true;
                trans.VProvider = "";
                trans.StatusRequestCount = 0;
                trans.Sold = true;
                trans.VoucherSerialNumber = "success";
                trans.VendStatus = "";
                //BALANCE DEDUCTION
                await Deductbalace(trans, pos);
            }
            catch (Exception ex)
            {
                Utilities.LogExceptionToDatabase(new Exception($"UpdateTransact at {DateTime.UtcNow} for traxId {trans.TransactionId} user: {trans.UserId}"), $"Exception: {JsonConvert.SerializeObject(ex)}");
            }

            return trans;
        }

        private async Task Deductbalace(TransactionDetail trans, POS pos, bool billVendor = true)
        {
            //BALANCE DEDUCTION
            using(var ctx = new VendtechEntities())
            {
                if (billVendor)
                {
                    trans.BalanceBefore = pos.Balance ?? 0;
                    pos.Balance = (pos.Balance - trans.Amount);
                    trans.CurrentVendorBalance = pos.Balance ?? 0;
                }

                ctx.TransactionDetails.AddOrUpdate(trans);
                ctx.POS.AddOrUpdate(pos);
                try
                {

                    await ctx.SaveChangesAsync();
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var error in ex.EntityValidationErrors)
                    {
                        foreach (var validationError in error.ValidationErrors)
                        {
                            Console.WriteLine($"Property: {validationError.PropertyName}, Error: {validationError.ErrorMessage}");
                        }
                    }
                }
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

            string transactionId = await idGenerator.GenerateNewTransactionId();
            trans.TransactionId = transactionId;

            _context.TransactionDetails.Add(trans);
            await _context.SaveChangesAsync();

            return trans;
        }

        private static VtechElectricitySaleRequest Buid_new_request_object(RechargeMeterModel model)
        {
            return new VtechElectricitySaleRequest
            {
                Amount = model.Amount,
                MeterNumber = model.MeterNumber,
                TransactionId = model.TransactionId.ToString(),
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
            Utilities.LogExceptionToDatabase(new Exception($"MakeRechargeRequest START {DateTime.UtcNow} for traxId {model.TransactionId}"), $"model: {JsonConvert.SerializeObject(model)}");
            string url = WebConfigurationManager.AppSettings["VendtechExtentionServer"].ToString() + "sales/v1/buy";

            try
            {
                VtechElectricitySaleRequest request_model = Buid_new_request_object(model);

                var apiKey = WebConfigurationManager.AppSettings["ApiKey"].ToString();
                _client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
                HttpResponseMessage icekloud_response = await _client.PostAsJsonAsync(url, request_model);

                string strings_result = await icekloud_response.Content.ReadAsStringAsync();

                transactionDetail.Request = JsonConvert.SerializeObject(request_model);
                transactionDetail.Response = strings_result;

                VtechExtensionResponse response = JsonConvert.DeserializeObject<VtechExtensionResponse>(strings_result);

                Utilities.LogExceptionToDatabase(new Exception($"MakeRechargeRequest END {DateTime.UtcNow} for traxId {model.TransactionId}"), $"strings_result: {strings_result}");
                return response;
            }
            catch (HttpException ex)
            {
                Utilities.LogExceptionToDatabase(new Exception($"HttpException ERROR {DateTime.UtcNow} for traxId {model.TransactionId}"), $"Exception: {ex.Message}");
                throw new ArgumentException("Unable to Access service");
            }
            catch (Exception)
            {
                throw;
            }

        }

        private async Task<VtechExtensionResponse> QueryStatusRequest(RechargeMeterModel model, TransactionDetail transactionDetail)
        {
            Utilities.LogExceptionToDatabase(new Exception($"QueryStatusRequest START {DateTime.UtcNow} for traxId {model.TransactionId}"), $"model: {JsonConvert.SerializeObject(model)}");
            string url = WebConfigurationManager.AppSettings["VendtechExtentionServer"].ToString() + "sales/v1/status";

            try
            {
                VtechElectricitySaleStatus request_model = Buid_new_status_object(model);

                var apiKey = WebConfigurationManager.AppSettings["ApiKey"].ToString();
                if (!_client.DefaultRequestHeaders.Contains("X-Api-Key"))
                {
                    _client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
                }
                HttpResponseMessage icekloud_response = await _client.PostAsJsonAsync(url, request_model);

                string strings_result = await icekloud_response.Content.ReadAsStringAsync();

                transactionDetail.Request = JsonConvert.SerializeObject(request_model);
                transactionDetail.Response = strings_result;


                VtechExtensionResponse response = JsonConvert.DeserializeObject<VtechExtensionResponse>(strings_result);

                Utilities.LogExceptionToDatabase(new Exception($"QueryStatusRequest END {DateTime.UtcNow} for traxId {model.TransactionId}"), $"strings_result: {strings_result}");
                return response;
            }
            catch (HttpException ex)
            {
                Utilities.LogExceptionToDatabase(new Exception($"HttpException ERROR {DateTime.UtcNow} for traxId {model.TransactionId}"), $"Exception: {ex.Message}");
                throw new ArgumentException("Unable to Access service");
            }
            catch (Exception)
            {
                throw;
            }

        }

    }
}