﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VendTech.BLL.Models;
using VendTech.DAL;

namespace VendTech.BLL.Interfaces
{
    public interface IDepositManager
    {
        /// <summary>
        /// Dummy Method for testing purpose:  Get Welcome Message
        /// </summary>
        /// <returns></returns>
        string GetWelcomeMessage();

        /// <summary>
        /// This will be used to get user listing model
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        PagingResult<DepositListingModel> GetDepositPagedList(PagingModel model, bool getForRelease = false, long vendorId = 0,string status="");
        PagingResult<DepositListingModel> GetLastTenDepositPagedList(PagingModel model, long posId = 0);
        PagingResult<DepositListingModel> GetAllPendingDepositPagedList(PagingModel model, bool getForRelease = false, long vendorId = 0, string status = "");
        PagingResult<DepositLogListingModel> GetDepositLogsPagedList(PagingModel model);
        decimal GetPendingDepositTotal();
        Task<ActionOutput> ChangeDepositStatus(long depositId, DepositPaymentStatusEnum status, bool isAutoApprove = false); 
        ActionOutput<string> SendOTP();
        ActionOutput<PendingDeposit> SaveDepositRequest(DepositModel model, bool forAgents = false);
        Task<ActionOutput<List<long>>> ChangeMultipleDepositStatus(ReleaseDepositModel model, long userId);
        PagingResult<DepositListingModel> GetUserDepositList(int pageNo, int pageSize, long userId);
        ActionOutput<DepositListingModel> GetDepositDetail(long depositId, bool isAdmin = false);
        PagingResult<DepositListingModel> GetReportsPagedList(ReportSearchModel model, bool callFromAdmin = false, long agentId = 0);
        PagingResult<DepositAuditModel> GetAuditReportsPagedList(ReportSearchModel model, bool callFromAdmin = false);
        PagingResult<DepositAuditModel> GetDepositAuditReports(ReportSearchModel model, bool callFromAdmin = false);
        DepositAuditModel SaveDepositAuditRequest(DepositAuditModel depositAuditModel);
        PagingResult<DepositListingModel> GetReportsPagedHistoryList(ReportSearchModel model, bool callFromAdmin = false, long agentId = 0);
        PagingResult<DepositExcelReportModel> GetReportsExcelDeposituser(ReportSearchModel model, bool callFromAdmin = false, long agentId = 0);
        PagingResult<DepositExcelReportModel> GetReportExcelData(ReportSearchModel model, long agentId = 0);
        PagingResult<DepositAuditExcelReportModel> GetAuditReportExcelData(ReportSearchModel model);
        PagingResult<DepositListingModel> GetReleasedDepositPagedList(PagingModel model, bool getForRelease, long vendorId = 0);
        ActionOutput ChangeMultipleDepositStatusOnReverse(ReverseDepositModel model, long userId);
        ActionOutput ReverseDepositStatus(long depositId, DepositPaymentStatusEnum status, long currentUserId);
        List<Deposit> GetUnclearedDeposits();
        void UpdateNextReminderDate(Deposit deposit);
        DepositAuditModel UpdateDepositAuditRequest(DepositAuditModel depositAuditModel);
        Task<List<PendingDeposit>> GetListOfDeposits(List<long> depositIds);
        decimal ReturnPendingDepositsTotalAmount(DepositModel model);
        decimal TakeCommisionsAndReturnAgentsCommision(long posId, decimal amt);
        //Deposit SaveApprovedDeposit(PendingDeposit model);
        IQueryable<BalanceSheetListingModel> GetBalanceSheetReportsPagedList(ReportSearchModel model, bool callFromAdmin, long agentId);
        IQueryable<DashboardBalanceSheetModel> GetDashboardBalanceSheetReports(DateTime date);
        PagingResult<AgentRevenueListingModel> GetAgentRevenueReportsPagedList(ReportSearchModel model, bool callFromAdmin= false, long agentId = 0);
        PagingResult<AgencyRevenueExcelReportModel> GetAgentRevenueReportsExcelDeposituser(ReportSearchModel model, bool callFromAdmin = false, long agentId = 0);
        void DeletePendingDeposits(List<PendingDeposit> deposits);
        Task<ActionOutput> CreateDepositCreditTransfer(DepositDTOV2 dbDeposit, long currentUserId, POS fromPos);
        Task<Deposit> CreateDepositDebitTransfer(DepositDTOV2 dbDeposit, long currentUserId, string otp, long toPos, long fromPos);
        Task<ActionOutput> DepositToAgencyAdminAccount(DepositDTOV2 dbDeposit, long currentUserId, string OTP);
        ActionOutput<string> CancelDeposit(CancelDepositModel model);
        PagingResult<DepositListingModelMobile> GetReportsMobilePagedList(ReportSearchModel model, bool callFromAdmin = false, long agentId = 0);
        List<DepositListingModel> GetPendingDepositForCustomer(long UserId, long agencyId);
        ActionOutput<DepositListingModel> GetPendingDepositDetail(long pdepositId);
        void CreateCommissionCreditEntry(POS toPos, decimal amount, string reference, long currentUserId);
        PagingResult<AgentRevenueListingModel> GetCommissionsReportsPagedList(ReportSearchModel model, bool callFromAdmin = false, long agentId = 0);
        Deposit GetSingleTransaction(long transactionId);
        PendingDeposit GetDeposit(long depositId);
        Task DeletePendingDeposits(PendingDeposit deposit);
        bool IsOtpValid(string otp);
        List<PendingDeposit> GetPendingDeposits(List<long> pdepositIds);
        Task<PendingDeposit> GetPendingDepositByPOS(long posId, decimal amount);
        Deposit GetMainDeposit(long depositId);
    }

}
