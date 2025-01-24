using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using PagedList;
using System.Linq.Dynamic;
using System.Data.Entity;
using VendTech.DAL;
using Newtonsoft.Json;
using VendTech.BLL.PlatformApi;
using VendTech.BLL.Common;
using System.Threading.Tasks;
using static VendTech.BLL.PlatformApi.PlatformApi_Sochitel;
using System.Web.Helpers;

namespace VendTech.BLL.Managers
{
    public class PlatformTransactionManager : BaseManager, IPlatformTransactionManager
    {
        private IPlatformApiManager _platformApiManager;
        private IPlatformManager _platformManager;
        private IErrorLogManager _errorLog;
        private readonly TransactionIdGenerator _transactionIdGenerator;
        private readonly VendtechEntities _context;
        public PlatformTransactionManager(IPlatformApiManager platformApiManager,
            IPlatformManager platformManager,
            IErrorLogManager errorLog,
            TransactionIdGenerator transactionIdGenerator,
            VendtechEntities context)
        {
            _platformApiManager = platformApiManager;
            _platformManager = platformManager;
            _errorLog = errorLog;
            _transactionIdGenerator = transactionIdGenerator;
            _context = context;
        }

        public PlatformTransactionModel New(long userId, int platformId, long posId, decimal amount, string beneficiary, string currency, int? apiConnId)
        {
            //TODO - validate input


            PlatformTransaction platformTransaction = new VendTech.DAL.PlatformTransaction
            {
                UserId = userId,
                PlatformId = platformId,
                Amount = amount,
                Beneficiary = beneficiary,
                Currency = currency,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PosId = posId,
                Status = (int) TransactionStatus.InProgress,
                ApiConnectionId = apiConnId,
                LastPendingCheck = 0,
            };

            _context.PlatformTransactions.Add(platformTransaction);
            _context.SaveChanges();

            return _context.PlatformTransactions.Where(x => x.Id == platformTransaction.Id).Select(PlatformTransactionModel.Projection).FirstOrDefault();
        }

        public async Task<bool> ProcessAirtimeTransactionViaApi(long transactionId)
        {
            if (transactionId > 0)
            {
                PlatformTransaction tranx = await GetPendingTransactionById(transactionId);
                PlatformModel platform;
                if (tranx != null)
                {
                    if (tranx.ApiConnectionId < 1)
                    {
                        platform = await _platformManager.GetPlatformById(tranx.PlatformId);
                        if (platform != null && platform.PlatformId > 0 && platform.PlatformApiConnId > 0)
                        {
                            //update the connection of the transaction
                            tranx.ApiConnectionId = platform.PlatformApiConnId;
                            tranx.UpdatedAt = DateTime.UtcNow;

                            _context.SaveChanges();
                        }
                    }
                    
                    if (tranx.ApiConnectionId < 1)
                    {
                        FlagTransactionWithStatus(tranx, TransactionStatus.Error);
                        return false;
                    }

                    PlatformApiConnection platformApiConnection = 
                            _context.PlatformApiConnections.Where(p => p.Id == tranx.ApiConnectionId).FirstOrDefault();

                    if (platformApiConnection == null)
                    {
                        FlagTransactionWithStatus(tranx, TransactionStatus.Error);
                        return false;
                    }
                    
                    ExecutionContext executionContext = new ExecutionContext();

                    //PlatformApi config
                    string config = platformApiConnection.PlatformApi.Config;
                    executionContext.PlatformApiConfig = JsonConvert.DeserializeObject<Dictionary<string, string>>(config);

                    //Get the Per Platform API Conn Params
                    PlatformPacParams platformPacParams = await _platformApiManager.GetPlatformPacParams(tranx.PlatformId, (int) tranx.ApiConnectionId);
                    if (platformPacParams != null && platformPacParams.ConfigDictionary != null)
                    {
                         executionContext.PerPlatformParams = platformPacParams.ConfigDictionary;
                    }

                    executionContext.Amount= tranx.Amount;
                    executionContext.PlatformType = PlatformTypeEnum.AIRTIME;

                    //For now we will configure both MSISDN and Account ID
                    //TODO: we should know by platform type what field we should populate
                    executionContext.Msisdn = tranx.Beneficiary;
                    executionContext.AccountId = tranx.Beneficiary;

                    IPlatformApi api = _platformApiManager.GetPlatformApiInstanceByTypeId(platformApiConnection.PlatformApi.ApiType);
                    ExecutionResponse execResponse = await api.Execute(executionContext);

                    //Save the logs
                    string logJSON = JsonConvert.SerializeObject(execResponse);
                    PlatformApiLog log = new PlatformApiLog
                    {
                        TransactionId = tranx.Id,
                        LogType = (int)ApiLogType.InitialRequest,
                        ApiLog = logJSON,
                        LogDate = DateTime.UtcNow
                    };

                    _context.PlatformApiLogs.Add(log);
                    await _context.SaveChangesAsync();

                    //Fetch from DB
                    tranx = await GetPendingTransactionById(transactionId);
                    tranx.Status = execResponse.Status;
                    tranx.UserReference = execResponse.UserReference;
                    tranx.OperatorReference = execResponse.OperatorReference;
                    tranx.PinNumber = execResponse.PinNumber;
                    tranx.PinSerial = execResponse.PinSerial;
                    tranx.PinInstructions = execResponse.PinInstructions;
                    tranx.ApiTransactionId = execResponse.ApiTransactionId;
                    tranx.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    return true;
                }
            }

            return false;
        }

        public async Task<bool> ProcessNetflixTransactionViaApi(long transactionId)
        {
            if (transactionId > 0)
            {
                PlatformTransaction tranx = await GetPendingTransactionById(transactionId);
                PlatformModel platform;
                if (tranx != null)
                {
                    if (tranx.ApiConnectionId < 1)
                    {
                        platform = await _platformManager.GetPlatformById(tranx.PlatformId);
                        if (platform != null && platform.PlatformId > 0 && platform.PlatformApiConnId > 0)
                        {
                            //update the connection of the transaction
                            tranx.ApiConnectionId = platform.PlatformApiConnId;
                            tranx.UpdatedAt = DateTime.UtcNow;

                            _context.SaveChanges();
                        }
                    }

                    if (tranx.ApiConnectionId < 1)
                    {
                        FlagTransactionWithStatus(tranx, TransactionStatus.Error);
                        return false;
                    }

                    PlatformApiConnection platformApiConnection =
                            _context.PlatformApiConnections.Where(p => p.Id == tranx.ApiConnectionId).FirstOrDefault();

                    if (platformApiConnection == null)
                    {
                        FlagTransactionWithStatus(tranx, TransactionStatus.Error);
                        return false;
                    }

                    ExecutionContext executionContext = new ExecutionContext();

                    //PlatformApi config
                    string config = platformApiConnection.PlatformApi.Config;
                    executionContext.PlatformApiConfig = JsonConvert.DeserializeObject<Dictionary<string, string>>(config);

                    //Get the Per Platform API Conn Params
                    PlatformPacParams platformPacParams = await _platformApiManager.GetPlatformPacParams(tranx.PlatformId, (int)tranx.ApiConnectionId);
                    if (platformPacParams != null && platformPacParams.ConfigDictionary != null)
                    {
                        executionContext.PerPlatformParams = platformPacParams.ConfigDictionary;
                    }

                    executionContext.Amount = tranx.Amount;
                    executionContext.PlatformType = PlatformTypeEnum.NETFLIX;


                    IPlatformApi api = _platformApiManager.GetPlatformApiInstanceByTypeId(platformApiConnection.PlatformApi.ApiType);
                    ExecutionResponse execResponse = await api.Execute(executionContext);

                    //Save the logs
                    string logJSON = JsonConvert.SerializeObject(execResponse);
                    PlatformApiLog log = new PlatformApiLog
                    {
                        TransactionId = tranx.Id,
                        LogType = (int)ApiLogType.InitialRequest,
                        ApiLog = logJSON,
                        LogDate = DateTime.UtcNow
                    };

                    _context.PlatformApiLogs.Add(log);
                    await _context.SaveChangesAsync();

                    //Fetch from DB
                    tranx = await GetPendingTransactionById(transactionId);
                    tranx.Status = execResponse.Status;
                    tranx.UserReference = execResponse.UserReference;
                    tranx.OperatorReference = execResponse.OperatorReference;
                    tranx.PinNumber = execResponse.PinNumber;
                    tranx.PinSerial = execResponse.PinSerial;
                    tranx.PinInstructions = execResponse.PinInstructions;
                    tranx.ApiTransactionId = execResponse.ApiTransactionId;
                    tranx.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    return true;
                }
            }

            return false;
        }


        public async Task CheckPendingTransaction()
        { 
            using (var DbCtx = new VendtechEntities())
            {
                //Last pending check done 1 min ago. This is to avoid checking the status within too short intervals
                long lastPendingCheck = Utilities.ToUnixTimestamp(DateTime.UtcNow) - 60;
                PlatformTransaction pendingTranx = null;
                if (lastPendingCheck > 0)
                {
                    try
                    {
                        pendingTranx = DbCtx.PlatformTransactions
                                        .Where(t => t.Status == (int)TransactionStatus.Pending)
                                        .Where(t => t.LastPendingCheck < lastPendingCheck)
                                        .OrderByDescending(d => d.Id)
                                        .FirstOrDefault();
                    }
                    catch (Exception ex)
                    {
                        _errorLog.LogExceptionToDatabase(new Exception("pendingTranx", ex));
                        return;
                    }
                    
                    try
                    {
                        if (pendingTranx != null && pendingTranx.ApiConnectionId > 0)
                        {
                            pendingTranx.LastPendingCheck = Utilities.ToUnixTimestamp(DateTime.UtcNow);
                            await DbCtx.SaveChangesAsync();

                            ExecutionContext executionContext = new ExecutionContext
                            {
                                UserReference = pendingTranx.UserReference,
                                ApiTransactionId = pendingTranx.ApiTransactionId
                            };

                            PlatformApiConnection platformApiConnection =
                                    DbCtx.PlatformApiConnections.Where(p => p.Id == pendingTranx.ApiConnectionId).FirstOrDefault();

                            //PlatformApi config
                            string config = platformApiConnection.PlatformApi.Config;
                            executionContext.PlatformApiConfig = JsonConvert.DeserializeObject<Dictionary<string, string>>(config);

                            //Get the Per Platform API Conn Params
                            PlatformPacParam platformPacParam = DbCtx.PlatformPacParams.FirstOrDefault(
                            p => p.PlatformId == pendingTranx.PlatformId && p.PlatformApiConnectionId == pendingTranx.ApiConnectionId);

                            PlatformPacParams platformPacParams = PlatformPacParams.From(platformPacParam);
                            if (platformPacParams != null && platformPacParams.ConfigDictionary != null)
                            {
                                executionContext.PerPlatformParams = platformPacParams.ConfigDictionary;
                            }

                            IPlatformApi api = _platformApiManager.GetPlatformApiInstanceByTypeId(platformApiConnection.PlatformApi.ApiType);
                            ExecutionResponse execResponse = await api.CheckStatus(executionContext);

                            //Save the logs
                            string logJSON = JsonConvert.SerializeObject(execResponse);
                            PlatformApiLog log = new PlatformApiLog
                            {
                                TransactionId = pendingTranx.Id,
                                LogType = (int)ApiLogType.PendingCheckRequest,
                                ApiLog = logJSON,
                                LogDate = DateTime.UtcNow
                            };

                            DbCtx.PlatformApiLogs.Add(log);
                            await DbCtx.SaveChangesAsync();

                            //Fetch from DB
                            pendingTranx = DbCtx.PlatformTransactions.Where(t => t.Id == pendingTranx.Id).FirstOrDefault();
                            if (execResponse.Status == (int)OuterTransactionStatus.Successful)
                            {
                                var response = JsonConvert.DeserializeObject<Response>(execResponse.ApiCalls[0].Response);
                                if(response.Result.Status.TypeName == "Failure")
                                {
                                    pendingTranx.Status = (int)TransactionStatus.Failed;
                                }
                                else if(response.Result.Status.TypeName == "Success")
                                {
                                    pendingTranx.Status = (int)TransactionStatus.Successful;
                                }
                                else
                                {
                                    pendingTranx.Status = (int)TransactionStatus.Failed;
                                }
                                pendingTranx.OperatorReference = execResponse.OperatorReference;
                                pendingTranx.PinNumber = execResponse.PinNumber;
                                pendingTranx.PinSerial = execResponse.PinSerial;
                                pendingTranx.PinInstructions = execResponse.PinInstructions;
                                pendingTranx.ApiTransactionId = execResponse.ApiTransactionId;
                                pendingTranx.UpdatedAt = DateTime.UtcNow;

                                await DbCtx.SaveChangesAsync();

                                if (pendingTranx.Status == (int)TransactionStatus.Successful)
                                {
                                    PlatformTransactionModel tranxModel = DbCtx.PlatformTransactions
                                                                            .Where(t => t.Id == pendingTranx.Id)
                                                                            .Select(PlatformTransactionModel.Projection)
                                                                            .FirstOrDefault();

                                    TransactionDetail transactionDetail = CreateTransactionDetail(tranxModel, (int)RechargeMeterStatusEnum.Success);
                                    List<PlatformApiLogModel> logs = DbCtx.PlatformApiLogs
                                                                            .Select(PlatformApiLogModel.Projection)
                                                                            .Where(l => l.TransactionId == pendingTranx.Id)
                                                                            .OrderBy(l => l.LogDate)
                                                                            .ToList();

                                    Logs tranxLogs = await CreateLogs(logs);

                                    transactionDetail.Request = tranxLogs.Request.ToString();
                                    transactionDetail.Response = tranxLogs.Response.ToString();
                                    transactionDetail.TransactionId = await _transactionIdGenerator.GenerateNewTransactionId();

                                    DbCtx.TransactionDetails.Add(transactionDetail);
                                    PlatformTransaction tranx = DbCtx.PlatformTransactions.Where(t => t.Id == tranxModel.Id).FirstOrDefault();
                                    tranx.TransactionDetailId = transactionDetail.TransactionDetailsId;
                                    await DbCtx.SaveChangesAsync();
                                }
                                //Transaction failed so reverse balance
                                else
                                {
                                    var pos = DbCtx.POS.FirstOrDefault(p => p.POSId == pendingTranx.PosId);
                                    ReverseBalanceDeduction(DbCtx, pos, pendingTranx.Amount);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
                 

                
                
            }
            //SELECT DATEDIFF(SECOND,'1970-01-01', GETUTCDATE())
        }

        public async Task<List<PlatformApiLogModel>> GetTransactionLogs(long transactionId)
        {
            return await _context.PlatformApiLogs
                .Where(o => o.TransactionId == transactionId)
                .Select(PlatformApiLogModel.Projection)
                .OrderBy(o => o.LogDate)
                .ToListAsync();
        }

        public PagingResult<MeterRechargeApiListingModel> GetUserAirtimeRechargeTransactionDetailsHistory(ReportSearchModel model, bool callFromAdmin)
        {
            //if (model.RecordsPerPage != 20)
            //{
            //    model.RecordsPerPage = 10;
            //}
            var result = new PagingResult<MeterRechargeApiListingModel>();
            var query = _context.TransactionDetails.OrderByDescending(d => d.CreatedAt)
                .Where(p => !p.IsDeleted && p.Finalised == true && p.POSId != null && p.Platform.PlatformType == (int) PlatformTypeEnum.AIRTIME && p.PlatFormId == model.PlatformId);

            if (model.VendorId > 0)
            {
                var user = _context.Users.FirstOrDefault(p => p.UserId == model.VendorId);
                var posIds = new List<long>();
                if (callFromAdmin)
                    posIds = _context.POS.Where(p => p.VendorId == model.VendorId).Select(p => p.POSId).ToList();
                else
                    posIds = _context.POS.Where(p => p.VendorId != null && (p.VendorId == user.FKVendorId)).Select(p => p.POSId).ToList();
                query = query.Where(p => posIds.Contains(p.POSId.Value));
            }

            var list = query.Take(10).OrderByDescending(x => x.CreatedAt).AsEnumerable().Select(x => new MeterRechargeApiListingModel
            {
                //TransactionDetailsId = x.TransactionDetailsId,
                Amount = Utilities.FormatAmount(x.Amount),
                //PlatformId = (int)x.PlatFormId,
                ProductShortName = x.Platform.Title,
                CreatedAt = x.CreatedAt.ToString("dd/MM/yyyy hh:mm"),
                MeterNumber = x.Meter == null ? x.MeterNumber1 : x.Meter.Number,
                POSId = x.POSId == null ? "" : x.POS.SerialNumber,
                Status = ((RechargeMeterStatusEnum)x.Status).ToString(),
                TransactionId = x.TransactionId,
                //MeterRechargeId = x.TransactionDetailsId,
                //RechargeId = x.TransactionDetailsId,
                //UserName = x.User?.Name + (!string.IsNullOrEmpty(x.User.SurName) ? " " + x.User.SurName : ""),
                //VendorName = x.POS.User == null ? "" : x.POS.User.Vendor,
                RechargePin = x.Platform.PlatformType == 4 ? Utilities.FormatThisToken(x.MeterToken1) : x.MeterNumber1 + "/" + x.TransactionId,
                //PlatformName = x.Platform.Title,
                NotType = "sale",
            }).ToList();

            result.List = list;
            result.Status = ActionStatus.Successfull;
            result.Message = "Airtime recharges fetched successfully.";
            return result;
        }

        public PagingResult<MeterRechargeApiListingModel> GetUserNetflixRechargeTransactionDetailsHistory(ReportSearchModel model, bool callFromAdmin)
        {
           
            var result = new PagingResult<MeterRechargeApiListingModel>();
            var query = _context.TransactionDetails.OrderByDescending(d => d.CreatedAt)
                .Where(p => !p.IsDeleted && p.Finalised == true && p.POSId != null && p.Platform.PlatformType == (int)PlatformTypeEnum.NETFLIX && p.PlatFormId == model.PlatformId);

            if (model.VendorId > 0)
            {
                var user = _context.Users.FirstOrDefault(p => p.UserId == model.VendorId);
                var posIds = new List<long>();
                if (callFromAdmin)
                    posIds = _context.POS.Where(p => p.VendorId == model.VendorId).Select(p => p.POSId).ToList();
                else
                    posIds = _context.POS.Where(p => p.VendorId != null && (p.VendorId == user.FKVendorId)).Select(p => p.POSId).ToList();
                query = query.Where(p => posIds.Contains(p.POSId.Value));
            }

            var list = query.Take(10).OrderByDescending(x => x.CreatedAt).AsEnumerable().Select(x => new MeterRechargeApiListingModel
            {
                Amount = Utilities.FormatAmount(x.Amount),
                ProductShortName = x.Platform.Title,
                CreatedAt = x.CreatedAt.ToString("dd/MM/yyyy hh:mm"),
                MeterNumber = x.Meter == null ? x.MeterNumber1 : x.Meter.Number,
                POSId = x.POSId == null ? "" : x.POS.SerialNumber,
                Status = ((RechargeMeterStatusEnum)x.Status).ToString(),
                TransactionId = x.TransactionId,
                RechargePin = x.Platform.PlatformType == 5 ? Utilities.FormatThisToken(x.MeterToken1) : x.MeterNumber1 + "/" + x.TransactionId,
                NotType = "sale",
            }).ToList();

            result.List = list;
            result.Status = ActionStatus.Successfull;
            result.Message = "Airtime recharges fetched successfully.";
            return result;
        }

        public DataTableResultModel<PlatformTransactionModel> GetPlatformTransactionsForDataTable(DataQueryModel query)
        {
            var result = DataTableResultModel<PlatformTransactionModel>.NewResultModel();

            if (query != null)
            {
                result.PagedList = _context.PlatformTransactions
                    .Include(p => p.Platform)
                    .Where(p => query.IsAdmin || (p.UserId == query.UserId))
                    .Where(p => (query.PlatformId < 1) ?  true : p.PlatformId == query.PlatformId)
                    .Where(p => string.IsNullOrEmpty(query.Reference) ? true : p.OperatorReference.ToLower() == query.Reference.ToLower())
                    .Where(p => string.IsNullOrEmpty(query.Beneficiary) ? true : p.Beneficiary.ToLower() == query.Beneficiary.ToLower())
                    .Where(p => query.FromDate == null || p.CreatedAt >= query.FromDate)
                    .Where(p => query.ToDate == null || p.CreatedAt <= query.ToDate)
                    .Where(p => query.Status < 0 || p.Status == query.Status)
                    .Where(p => query.ApiConnId <= 0 || p.ApiConnectionId == query.ApiConnId)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(PlatformTransactionModel.Projection).ToPagedList(query.Page, query.PageSize);   
            }

            return result;
        }
        private void FlagTransactionWithStatus(VendTech.DAL.PlatformTransaction tranx, TransactionStatus status)
        {
            if (tranx != null)
            {
                tranx.Status = (int)status;
                tranx.UpdatedAt = DateTime.UtcNow;
                _context.SaveChanges();
            }
        }

        private async Task<PlatformTransaction> GetPendingTransactionById(long id)
        {
            PlatformTransaction tranx = await _context.PlatformTransactions
                .Where(p => 
                    p.Id == id &&
                    (p.Status == (int)TransactionStatus.Pending || p.Status == (int)TransactionStatus.InProgress)
                 )
                .FirstOrDefaultAsync();
            return tranx;
        }

        public async Task<PlatformTransactionModel> GetPlatformTransactionById(DataQueryModel query, long id)
        {
            if (id > 0 & query != null)
            {
                var tranx = await _context.PlatformTransactions
                                    .Where(p => p.Id == id)
                                    .Where(p => query.IsAdmin ? true : p.UserId == query.UserId)
                                    .Select(PlatformTransactionModel.Projection)
                                    .FirstOrDefaultAsync();
                
                if (tranx != null)
                {
                    return tranx;
                }
            }

            return new PlatformTransactionModel();
        }

        public async Task<AirtimeReceiptModel> RechargeAirtime(PlatformTransactionModel model)
        {
            var response = new AirtimeReceiptModel { ReceiptStatus = new ReceiptStatus() };
            var user = await _context.Users.FirstOrDefaultAsync(p => p.UserId == model.UserId);
            if (user == null)
            {
                response.ReceiptStatus.Status = "unsuccessful";
                response.ReceiptStatus.Message = "User not exist.";
                return response;
            }
            var posid = user.POS.FirstOrDefault().POSId;
            var pos = await _context.POS.FirstOrDefaultAsync(p => p.POSId == posid);

            if (pos.Balance == null || pos.Balance.Value < model.Amount)
            {
                response.ReceiptStatus.Status = "unsuccessful";
                response.ReceiptStatus.Message = "INSUFFICIENT BALANCE FOR THIS TRANSACTION.";
                return response;
            }

            bool balanceDeducted = false;

            try
            {
                //Deduct the amount from the balance so the user does not go and initiate another transaction while this is still in progress
                pos.Balance = pos.Balance.Value - model.Amount;
                await _context.SaveChangesAsync();

                balanceDeducted = true;

                //Is the product configured with a Connection ID
                PlatformApiConnection apiConn = await _context.PlatformApiConnections.Where(x => x.PlatformId == model.PlatformId).FirstOrDefaultAsync();

                PlatformTransactionModel tranxModel = New(model.UserId, model.PlatformId, pos.POSId, model.Amount,
                    model.Beneficiary, model.Currency, apiConn?.Id);

                //Process the transaction via the API and 
                await ProcessAirtimeTransactionViaApi(tranxModel.Id);

                int Status = await _context.PlatformTransactions.Where(t => t.Id == tranxModel.Id).Select(t => t.Status).FirstOrDefaultAsync();

                //If it succeeds, then transfer to TransactionDetail 
                if (Status == (int)TransactionStatus.Successful)
                {
                    TransactionDetail transactionDetail = CreateTransactionDetail(tranxModel, (int)RechargeMeterStatusEnum.Success);

                    List<PlatformApiLogModel> logs = await GetTransactionLogs(tranxModel.Id);
                    Logs tranxLogs = await CreateLogs( logs );

                    transactionDetail.Request = tranxLogs.Request.ToString();
                    transactionDetail.Response = tranxLogs.Response.ToString();
                    transactionDetail.TransactionId = await _transactionIdGenerator.GenerateNewTransactionId();

                    _context.TransactionDetails.Add(transactionDetail);
                    PlatformTransaction tranx = await _context.PlatformTransactions.FirstOrDefaultAsync(t => t.Id == tranxModel.Id);
                    tranx.TransactionDetailId = transactionDetail.TransactionDetailsId;

                    transactionDetail.TenderedAmount = model.Amount;
                    transactionDetail.Amount = model.Amount;
                    transactionDetail.CurrentVendorBalance = pos.Balance;
                    transactionDetail.BalanceBefore = (pos.Balance + model.Amount);
                    await _context.SaveChangesAsync();
                    response = GenerateReceipt(transactionDetail);
                    Push_notification_to_user(user, model, transactionDetail.TransactionDetailsId);
                    return response;
                }
                else if (Status == (int)TransactionStatus.Pending)
                {
                    response.ReceiptStatus.Status = "pending";
                    response.ReceiptStatus.Message = "Airtime recharge is pending";
                    return response;
                }else if(Status == (int)TransactionStatus.Failed)
                {
                    if (balanceDeducted)
                    {
                        ReverseBalanceDeduction(_context, pos, model.Amount);
                    }
                }
                else
                {
                    if (balanceDeducted)
                    {
                        ReverseBalanceDeduction(_context, pos, model.Amount);
                    }
                }

                response.ReceiptStatus.Status = "pending";
                response.ReceiptStatus.Message = "Airtime recharge failed.";
                return response;
            }
            catch(Exception ex)
            {
                _errorLog.LogExceptionToDatabase(ex);
                //If balance was deducted before exception then reverse
                if (balanceDeducted)
                {
                    ReverseBalanceDeduction(_context, pos, model.Amount);
                }
                response.ReceiptStatus.Status = "pending";
                response.ReceiptStatus.Message = "Airtime recharge failed due to an error. Please contact Administrator";
                return response;
            }
        }

        public async Task<NetflixReceiptModel> RechargeNetflix(NetflixPlatformTransactionModel model)
        {
            var response = new NetflixReceiptModel { ReceiptStatus = new ReceiptStatus() };
            var user = await _context.Users.FirstOrDefaultAsync(p => p.UserId == model.UserId);
            if (user == null)
            {
                response.ReceiptStatus.Status = "unsuccessful";
                response.ReceiptStatus.Message = "User do not exist.";
                return response;
            }
            var posid = user.POS.FirstOrDefault().POSId;
            var pos = await _context.POS.FirstOrDefaultAsync(p => p.POSId == posid);

            if (pos.Balance == null || pos.Balance.Value < model.AmountOperator)
            {
                response.ReceiptStatus.Status = "unsuccessful";
                response.ReceiptStatus.Message = "INSUFFICIENT BALANCE FOR THIS TRANSACTION.";
                return response;
            }

            bool balanceDeducted = false;

            try
            {
                //Deduct the amount from the balance so the user does not go and initiate another transaction while this is still in progress
                pos.Balance = pos.Balance.Value - model.AmountOperator;
                await _context.SaveChangesAsync();

                balanceDeducted = true;

                //Is the product configured with a Connection ID
                PlatformApiConnection apiConn = await _context.PlatformApiConnections.Where(x => x.PlatformId == model.PlatformId).FirstOrDefaultAsync();

                PlatformTransactionModel tranxModel = New(model.UserId, model.PlatformId, pos.POSId, model.AmountOperator,
                    "netflix", model.Currency, apiConn?.Id);

                //Process the transaction via the API and 
                await ProcessNetflixTransactionViaApi(tranxModel.Id);

                int Status = await _context.PlatformTransactions.Where(t => t.Id == tranxModel.Id).Select(t => t.Status).FirstOrDefaultAsync();

                //If it succeeds, then transfer to TransactionDetail 
                if (Status == (int)TransactionStatus.Successful)
                {
                    TransactionDetail transactionDetail = CreateTransactionDetail(tranxModel, (int)RechargeMeterStatusEnum.Success);

                    List<PlatformApiLogModel> logs = await GetTransactionLogs(tranxModel.Id);
                    Logs tranxLogs = await CreateLogs(logs);

                    transactionDetail.Request = tranxLogs.Request.ToString();
                    transactionDetail.Response = tranxLogs.Response.ToString();
                    transactionDetail.TransactionId = await _transactionIdGenerator.GenerateNewTransactionId();

                    _context.TransactionDetails.Add(transactionDetail);
                    PlatformTransaction tranx = await _context.PlatformTransactions.FirstOrDefaultAsync(t => t.Id == tranxModel.Id);
                    tranx.TransactionDetailId = transactionDetail.TransactionDetailsId;

                    transactionDetail.TenderedAmount = model.AmountOperator;
                    transactionDetail.Amount = model.AmountOperator;
                    transactionDetail.CurrentVendorBalance = pos.Balance;
                    transactionDetail.BalanceBefore = (pos.Balance + model.AmountOperator);
                    transactionDetail.MeterToken1 = tranx.PinNumber;
                    await _context.SaveChangesAsync();
                    response = GenerateNetflixReceipt(transactionDetail);
                    Push_notification_to_user(user, model, transactionDetail.TransactionDetailsId);
                    return response;
                }
                else if (Status == (int)TransactionStatus.Pending)
                {
                    response.ReceiptStatus.Status = "pending";
                    response.ReceiptStatus.Message = "Airtime recharge is pending";
                    return response;
                }
                else if (Status == (int)TransactionStatus.Failed)
                {
                    if (balanceDeducted)
                    {
                        ReverseBalanceDeduction(_context, pos, model.AmountOperator);
                    }
                }
                else
                {
                    if (balanceDeducted)
                    {
                        ReverseBalanceDeduction(_context, pos, model.AmountOperator);
                    }
                }

                response.ReceiptStatus.Status = "pending";
                response.ReceiptStatus.Message = "Airtime recharge failed.";
                return response;
            }
            catch (Exception ex)
            {
                _errorLog.LogExceptionToDatabase(ex);
                //If balance was deducted before exception then reverse
                if (balanceDeducted)
                {
                    ReverseBalanceDeduction(_context, pos, model.AmountOperator);
                }
                response.ReceiptStatus.Status = "pending";
                response.ReceiptStatus.Message = "Airtime recharge failed due to an error. Please contact Administrator";
                return response;
            }
        }


        public async Task<NetflixReceiptModel> GetNetflixPlans()
        {
            int ApiConnectionId = 14;
            int PlatformId = 7;
            PlatformApiConnection platformApiConnection =
                           _context.PlatformApiConnections.Where(p => p.Id == ApiConnectionId).FirstOrDefault();

            var response = new NetflixReceiptModel { ReceiptStatus = new ReceiptStatus() };
            ExecutionContext executionContext = new ExecutionContext();

            //PlatformApi config
            string config = platformApiConnection.PlatformApi.Config;
            executionContext.PlatformApiConfig = JsonConvert.DeserializeObject<Dictionary<string, string>>(config);

            //Get the Per Platform API Conn Params
            PlatformPacParams platformPacParams = await _platformApiManager.GetPlatformPacParams(PlatformId, ApiConnectionId);
            if (platformPacParams != null && platformPacParams.ConfigDictionary != null)
            {
                executionContext.PerPlatformParams = platformPacParams.ConfigDictionary;
            }


            IPlatformApi api = _platformApiManager.GetPlatformApiInstanceByTypeId(platformApiConnection.PlatformApi.ApiType);
            ExecutionResponse execResponse = await api.Execute(executionContext);

            ApiResponse apiResponse = JsonConvert.DeserializeObject<ApiResponse>(execResponse.ApiCalls[0].Response);
            var res = apiResponse.Result.ToArray()[2];
            //var productData = JsonConvert.DeserializeObject<ProductData>(res.Value);
            return null;
        }


        private void Push_notification_to_user(User user, PlatformTransactionModel model, long MeterRechargeId)
        {
            var deviceTokens = user.TokensManagers.Where(p => p.DeviceToken != null && p.DeviceToken != string.Empty).Select(p => new { p.AppType, p.DeviceToken }).ToList().Distinct(); ;
            var obj = new PushNotificationModel();
            obj.UserId = model.UserId;
            obj.Id = MeterRechargeId;
            obj.Title = "Airtime recharged successfully";
            obj.Message = $"Your phone has successfully recharged with SLe {Utilities.FormatAmount(model.Amount)}";
            obj.NotificationType = NotificationTypeEnum.AirtimeRecharge;
            obj.Balance = user.POS.FirstOrDefault().Balance.Value;
            foreach (var item in deviceTokens)
            {
                obj.DeviceToken = item.DeviceToken;
                obj.DeviceType = item.AppType.Value;
                PushNotification.PushNotificationToMobile(obj);
            }
        }

        private void Push_notification_to_user(User user, NetflixPlatformTransactionModel model, long MeterRechargeId)
        {
            var deviceTokens = user.TokensManagers.Where(p => p.DeviceToken != null && p.DeviceToken != string.Empty).Select(p => new { p.AppType, p.DeviceToken }).ToList().Distinct(); ;
            var obj = new PushNotificationModel();
            obj.UserId = model.UserId;
            obj.Id = MeterRechargeId;
            obj.Title = "Airtime recharged successfully";
            obj.Message = $"Your phone has successfully recharged with SLe {Utilities.FormatAmount(model.AmountOperator)}";
            obj.NotificationType = NotificationTypeEnum.AirtimeRecharge;
            obj.Balance = user.POS.FirstOrDefault().Balance.Value;
            foreach (var item in deviceTokens)
            {
                obj.DeviceToken = item.DeviceToken;
                obj.DeviceType = item.AppType.Value;
                PushNotification.PushNotificationToMobile(obj);
            }
        }

        private AirtimeReceiptModel GenerateReceipt(TransactionDetail trax)
        {
            var receipt = new AirtimeReceiptModel();
            receipt.Phone = trax?.MeterNumber1 ?? "";
            receipt.POS = trax?.POS?.SerialNumber ?? "";
            receipt.CustomerName = trax?.User.Vendor ?? "";
            receipt.ReceiptNo = trax?.ReceiptNumber ?? "";
            var amt = trax?.Amount.ToString("N");
            receipt.Amount = amt.Contains('.') ? amt.TrimEnd('0').TrimEnd('.') : amt;
            receipt.Charges = Utilities.FormatAmount(Convert.ToDecimal(trax.ServiceCharge));
            receipt.Commission = string.Format("{0:N0}", 0.00);
            receipt.Discount = string.Format("{0:N0}", 0);
            receipt.TransactionDate = trax.CreatedAt.ToString("dd/MM/yyyy hh:mm");
            receipt.VendorId = trax.User.Vendor;
            receipt.EDSASerial = trax?.SerialNumber?? "";
            receipt.VTECHSerial = trax.TransactionId ?? "";
            receipt.mobileShowPrintButton = (bool)trax.POS.PosPrint;
            receipt.mobileShowSmsButton = (bool)trax.POS.PosSms;
            receipt.ShouldShowSmsButton = (bool)trax.POS.WebSms;
            receipt.ShouldShowPrintButton = (bool)trax.POS.WebPrint;
            receipt.CurrencyCode = Utilities.GetCountry().CurrencyCode;

            if (trax.PlatFormId == 2)
                receipt.ReceiptTitle = "ORANGE";
            if (trax.PlatFormId == 3)
                receipt.ReceiptTitle = "AFRICELL";
            if (trax.PlatFormId == 4)
                receipt.ReceiptTitle = "QCELL";
            if (trax.PlatFormId == 7)
                receipt.ReceiptTitle = "NETFLIX";

            receipt.IsNewRecharge = true;
            return receipt;
        }
        private NetflixReceiptModel GenerateNetflixReceipt(TransactionDetail trax)
        {
            var receipt = new NetflixReceiptModel();
            receipt.Phone = trax?.MeterNumber1 ?? "";
            receipt.CustomerName = trax?.User.Vendor ?? "";
            receipt.ReceiptNo = trax?.ReceiptNumber ?? "";
            var amt = trax?.Amount.ToString("N");
            receipt.mobileShowPrintButton = (bool)trax.POS.PosPrint;
            receipt.mobileShowSmsButton = (bool)trax.POS.PosSms;
            receipt.ShouldShowSmsButton = (bool)trax.POS.WebSms;
            receipt.ShouldShowPrintButton = (bool)trax.POS.WebPrint;
            receipt.CurrencyCode = Utilities.GetCountry().CurrencyCode;
            receipt.Pin = trax.MeterToken1;
            if (trax.PlatFormId == 2)
                receipt.ReceiptTitle = "ORANGE";
            if (trax.PlatFormId == 3)
                receipt.ReceiptTitle = "AFRICELL";
            if (trax.PlatFormId == 4)
                receipt.ReceiptTitle = "QCELL";
            if (trax.PlatFormId == 7)
                receipt.ReceiptTitle = "NETFLIX";

            receipt.IsNewRecharge = true;
            return receipt;
        }
        private static void ReverseBalanceDeduction(VendtechEntities dbCtx, VendTech.DAL.POS pos, decimal amount)
        {
            pos.Balance = pos.Balance.Value + amount;
            dbCtx.SaveChanges();
        }

        private static TransactionDetail CreateTransactionDetail(PlatformTransactionModel tranxModel, int status)
        {
            if (tranxModel == null)
            {
                throw new ArgumentNullException("PlatformTransaction to convert to TransactionDetail cannot be null");
            }

            var now = DateTime.UtcNow;

            var tranxDetail = new TransactionDetail
            {
                UserId = tranxModel.UserId,
                POSId = tranxModel.PosId,
                MeterNumber1 = tranxModel.Beneficiary,
                Amount = tranxModel.Amount,
                PlatFormId = tranxModel.PlatformId,
                IsDeleted = false,
                Status = status, // (int)RechargeMeterStatusEnum.Success,
                CreatedAt = now,
                RequestDate = now,
                Finalised = true,
                TaxCharge = "",
                Units = "",
                DebitRecovery = "",
                CostOfUnits = "",
            };

            return tranxDetail;
        }

        private static async Task<Logs> CreateLogs(List<PlatformApiLogModel> logs)
        {
            StringBuilder request = new StringBuilder();
            StringBuilder response = new StringBuilder();

            if (logs != null && logs.Count > 0)
            {
                foreach (var log in logs)
                {
                    if (log.LogType == (int)ApiLogType.InitialRequest)
                    {
                        request.Append("Initial Request:\n");
                    }
                    else
                    {
                        request.Append("\n\nPending Request:\n");
                    }

                    ExecutionResponse execRes = log.ApiLogJson;
                    List<ApiRequestInfo> apiRequestInfos = execRes.ApiCalls;
                    if (apiRequestInfos.Count > 0)
                    {
                        foreach (ApiRequestInfo reqInfo in apiRequestInfos)
                        {
                            request.Append("Request Sent => ").Append(reqInfo.RequestSentStr).Append("\n")
                            .Append("Payload => ").Append(reqInfo.Request).Append("\n");

                            response.Append("Response Received => ").Append(reqInfo.ResponseReceivedStr).Append("\n")
                                .Append("Payload => ").Append(reqInfo.Response).Append("\n");
                        }
                    }
                }
            }

            return await Task.Run(() => new Logs { Request = request, Response = response });
        }

        public AirtimeReceiptModel GetAirtimeReceipt(string traxId)
        {
            var trax = _context.TransactionDetails.Where(e => e.TransactionId == traxId).ToList().FirstOrDefault();
            if (trax != null)
            {
                var receipt = GenerateReceipt(trax);
                return receipt;
            }
            return new AirtimeReceiptModel { ReceiptStatus = new ReceiptStatus { Status = "unsuccessful", Message = "Unable to find voucher" } };
        }

        ReceiptModel IPlatformTransactionManager.ReturnAirtimeReceipt(string rechargeId)
        {
            var transaction_by_token = _context.TransactionDetails.Where(e => e.TransactionId == rechargeId).FirstOrDefault();
            if (transaction_by_token != null)
            {
                var receipt = Build_receipt_model_from_dbtransaction_detail(transaction_by_token);
                receipt.ShouldShowSmsButton = (bool)transaction_by_token.POS.WebSms;
                receipt.ShouldShowPrintButton = (bool)transaction_by_token.POS.WebPrint;
                receipt.mobileShowSmsButton = (bool)transaction_by_token.POS.PosSms;
                receipt.mobileShowPrintButton = (bool)transaction_by_token.POS.PosPrint;
                return receipt;
            }
            return new ReceiptModel { ReceiptStatus = new ReceiptStatus { Status = "unsuccessful", Message = "Unable to find voucher" } };
        }

        public ReceiptModel Build_receipt_model_from_dbtransaction_detail(TransactionDetail model)
        {
            if (model.POS == null) model.POS = new POS();
            var receipt = new ReceiptModel();
            receipt.AccountNo = model?.MeterNumber1;
            receipt.POS = model?.POS?.SerialNumber;
            receipt.CustomerName = model?.Customer;
            receipt.ReceiptNo = model?.ReceiptNumber;
            receipt.Amount = Utilities.FormatAmount(Convert.ToDecimal(model.Amount));
            receipt.TransactionDate = model.CreatedAt.ToString("dd/MM/yyyy hh:mm");
            receipt.VendorId = model.User.Vendor;
            receipt.EDSASerial = model.SerialNumber;
            receipt.VTECHSerial = model.TransactionId;
            receipt.PlatformId = model.PlatFormId;
            return receipt;
        }

    }


    internal class Logs
    {
        public StringBuilder Request { get; set; }
        public StringBuilder Response { get; set; }
    }
    
}
