﻿using VendTech.BLL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using static VendTech.BLL.Managers.POSManager;
using VendTech.DAL;
using static VendTech.BLL.Jobs.BalanceLowSheduleJob;

namespace VendTech.BLL.Interfaces
{
    public interface IPOSManager
    {
        PagingResult<POSListingModel> GetPOSPagedList(PagingModel model, long agentId = 0, long vendorId = 0, bool callForGetVendorPos = false);
        KeyValuePair<string, string> GetVendorDetail(long posId);
        SavePosModel GetPosDetail(long posId);
        SavePosModel GetPosDetails(string passCode);
        UserModel GetUserPosDetails(string posSerialNumber);
        ActionOutput SavePos(SavePosModel model);
        ActionOutput SavePasscodePos(SavePassCodeModel savePassCodeModel);
        ActionOutput SavePasscodePosApi(SavePassCodeModel savePassCodeModel);
        IList<PlatformCheckbox> GetAllPlatforms(long posId);
        ActionOutput DeletePos(long posId);
        ActionOutput ChangePOSStatus(int posId, bool value);
        decimal GetPosCommissionPercentage(long posId);
        decimal GetPosBalance(long posId);
        decimal GetPosCommissionPercentageByUserId(long userId); 
        List<SelectListItem> GetPOSSelectList(long userId = 0, long agentId = 0);
        List<PosSelectItem> GetVendorPos(long userId);
        List<PosAPiListingModel> GetPOSSelectListForApi(long userId = 0);
        PagingResult<POSListingModel> GetUserPosPagingListForApp(int pageNo, int pageSize, long userId);
        UserModel GetUserPosDetailApi(string posSerialNumber);
        decimal GetPosPercentage(long posId);
        POS GetSinglePos(long pos);
        List<PosSelectItem> GetAgencyPos(long userId);
        POS ReturnAgencyAdminPOS(long userId);
        PagingResultWithDefaultAmount<BalanceSheetListingModel2> CalculateBalancesheet(List<BalanceSheetListingModel> result);
        List<SelectListItem> GetPOSWithNameSelectList(long userId, long agentId, bool includeAdminPos = false);
        List<UserScheduleDTO> GetAllUserRunningLow();
        bool BalanceLowMessageIsSent(long userId, UserScheduleTypes type);
        void SaveUserSchedule(long userId, string balance);
        bool BalanceLowScheduleExist(long userId, UserScheduleTypes type);
        List<UserScheduleDTO> GetUserSchedule();
        void UpdateUserSchedule(long userId, UserScheduleStatus status);
        void RemoveFromSchedule(long userId);
        bool IsWalletFunded(long userId);
        POS GetVendorPos2(long vendorId);
        void UpdatePasscode(long VendorId);
        Task<TransactionDetail> RefundDeductedBalanceAsync(long posId, TransactionDetail trans);
        Task<TransactionDetail> DeductBalanceAsync(long posId, TransactionDetail trans);
    }

}
