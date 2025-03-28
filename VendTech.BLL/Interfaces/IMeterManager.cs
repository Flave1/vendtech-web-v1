﻿using VendTech.BLL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using VendTech.DAL;

namespace VendTech.BLL.Interfaces
{
    public interface IMeterManager
    {
        ActionOutput SaveMeter(MeterModel model);
        ActionOutput DeleteMeter(long meterId,long userId);
        PagingResult<MeterAPIListingModel> GetMeters(long userID, int pageNo, int pageSize, bool isActive);
        //ActionOutput RechargeMeter(RechargeMeterModel model);
        PagingResult<MeterRechargeApiListingModel> GetUserMeterRecharges(long userID, int pageNo, int pageSize);
        RechargeDetailPDFData GetRechargePDFData(long rechargeId);
        ActionOutput<MeterRechargeApiListingModel> GetRechargeDetail(long rechargeId);
        MeterModel GetMeterDetail(long meterId);
        PagingResult<MeterRechargeApiListingModel> GetUserMeterRechargesReportAsync(ReportSearchModel model,bool callFromAdmin=false, long agentId = 0);
        PagingResult<MeterRechargeApiListingModel> GetUserMeterRechargesHistory(ReportSearchModel model, bool callFromAdmin = false, PlatformTypeEnum platform = 0);
        List<SelectListItem> GetMetersDropDown(long userID);
        PagingResult<SalesReportExcelModel> GetSalesExcelReportData(ReportSearchModel model, bool callFromAdmin, long agentId = 0);
        //ReceiptModel RechargeMeterReturn(RechargeMeterModel model);
        ReceiptModel ReturnVoucherReceipt(string token);
        RequestResponse ReturnRequestANDResponseJSON(string token);
        TransactionDetail GetLastTransaction();
        TransactionDetail GetSingleTransaction(string transactionId);
        IQueryable<BalanceSheetListingModel> GetBalanceSheetReportsPagedList(ReportSearchModel model, bool callFromAdmin, long agentId);
        PagingResult<GSTRechargeApiListingModel> GetUserGSTRechargesReport(ReportSearchModel model, bool callFromAdmin, long agentId = 0);
        IQueryable<DashboardBalanceSheetModel> GetDashboardBalanceSheetReports(DateTime date);
        void RedenominateBalnces();
        decimal ReturnElectricityMinVend();
        PagingResult<VendorStatus> GetVendorStatus();
        PagingResult<MiniSalesReport> GetMiniSalesReport(ReportSearchModel model, bool callFromAdmin, long agentId, string type);
        PagingResult<MeterAPIListingModel> GetPhoneNumbers(long userID, int pageNo, int pageSize, bool isActive);
        NumberModel GetPhoneNumberDetail(long Id);
        ActionOutput SavePhoneNUmber(NumberModel model);
        ActionOutput DeletePhoneNumber(long id, long userId);
        ReceiptModel Build_receipt_model_from_dbtransaction_detail(TransactionDetail model);
        void LogSms(TransactionDetail td, string phone);
        PagingResult<VendorStatus> RunStoredProcParams();
        PagingResult<MeterRechargeApiListingModelMobile> GetUserMeterRechargesReportMobileAsync(ReportSearchModel model, bool callFromAdmin = false, long agentId = 0);
        ActionOutput<MeterRechargeApiListingModelMobile> GetMobileRechargeDetail(long rechargeId);
        decimal ReturnAirtimeMinVend();
        bool IsModuleLocked(int moduleId, long userId);
        Task<ReceiptModel> RechargeMeterReturnIMPROVED(RechargeMeterModel model);
        Task<ReceiptModel> ReturnTraxStatusReceiptAsync(string trxId, bool billVendor = true);
        string BuildElectricityEmailBody(string body, UserModel vendor, TransactionDetail td);
    }
    
}
