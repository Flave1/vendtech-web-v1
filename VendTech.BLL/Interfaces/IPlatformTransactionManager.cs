using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VendTech.BLL.Models;

namespace VendTech.BLL.Interfaces
{
    public interface IPlatformTransactionManager
    {
        PlatformTransactionModel New(long UserId, int platformId, long posId, Decimal amount, string beneficiary, string currency, int? apiConnId);
        DataTableResultModel<PlatformTransactionModel> GetPlatformTransactionsForDataTable(DataQueryModel query);
        Task<PlatformTransactionModel> GetPlatformTransactionById(DataQueryModel query, long id);
        Task<bool> ProcessTransactionViaApi(long transactionId);
        Task<List<PlatformApiLogModel>> GetTransactionLogs(long transactionId);
        Task CheckPendingTransaction();
        PagingResult<MeterRechargeApiListingModel> GetUserAirtimeRechargeTransactionDetailsHistory(ReportSearchModel model, bool callFromAdmin = false);
        Task<AirtimeReceiptModel> RechargeAirtime(PlatformTransactionModel model);
        AirtimeReceiptModel GetAirtimeReceipt(string traxId);
        ReceiptModel ReturnAirtimeReceipt(string rechargeId);
        Task<NetflixReceiptModel> RechargeNetflix(NetflixPlatformTransactionModel model);
    }
}
