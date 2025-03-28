﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;
using VendTech.BLL.Common;
using VendTech.DAL;

namespace VendTech.BLL.Models
{
    public class BalanceSheetReportExcelModel
    {
        public string DATE_TIME { get; set; }
        public string TRANSACTIONID { get; set; }
        public string TYPE { get; set; } 
        public string REFERENCE { get; set; }
        public string BALANCEBEFORE { get; set; }
        public string DEPOSITAMOUNT { get; set; }
        public string SALEAMOUNT { get; set; }
        public string BALANCE { get; set; } 
    }
    public class BalanceSheetModel
    {
        public List<BalanceSheetListingModel> Sales { get; set; }
        public List<BalanceSheetListingModel> Deposits { get; set; }
    }
    public class BalanceSheetListingModel
    {
        public DateTime DateTime { get; set; }
        public string TransactionId { get; set; }
        public string TransactionType { get; set; } 
        public string Reference { get; set; }
        public decimal DepositAmount { get; set; } = 0;
        public decimal SaleAmount { get; set; } = 0;
        public decimal Balance { get; set; } = 0; 
        public long? POSId { get; set; }
        public decimal? BalanceBefore { get; set; } = 0;
    }
    public class BalanceSheetListingModel2
    {
        public string DateTime { get; set; }
        public string TransactionId { get; set; }
        public string TransactionType { get; set; }
        public string Reference { get; set; }
        public string DepositAmount { get; set; }
        public string SaleAmount { get; set; }
        public string Balance { get; set; }
        public long? POSId { get; set; }
        public string BalanceBefore { get; set; }
        public BalanceSheetListingModel2(BalanceSheetListingModel x)
        {
            DateTime = x.DateTime != null ? x.DateTime.ToString("dd/MM/yyyy hh:mm"): "";
            TransactionId = x.TransactionId;
            TransactionType = x.TransactionType;
            Reference = x.Reference;
            DepositAmount = Utilities.FormatAmount(x.DepositAmount);
            SaleAmount = Utilities.FormatAmount(x.SaleAmount);
            Balance = Utilities.FormatAmount(x.Balance);
            BalanceBefore = Utilities.FormatAmount(x.BalanceBefore);
            POSId = x.POSId;
        }
    }

    public class DashboardBalanceSheetModel
    {  
        public string Vendor { get; set; }
        public decimal DepositAmount { get; set; } = 0;
        public decimal SaleAmount { get; set; } = 0;
        public decimal Balance { get; set; } = 0;
        public decimal POSBalance { get; set; } = 0;
        public string Status { get; set; }
        public long UserId { get; set; }
    }
    public class DepositListingModel
    {
        public string UserName { get; set; }
        public string VendorName { get; set; }
        public string ChkNoOrSlipId { get; set; }
        public string Type { get; set; }
        public string Comments { get; set; }
        public string Bank { get; set; }
        public string Status { get; set; }
        public string PosNumber { get; set; }
        public string CreatedAt { get; set; }
        public string TransactionId { get; set; }
        public string Amount { get; set; }
        public decimal Balance { get; set; }
        public string NewBalance { get; set; }
        public string PercentageAmount { get; set; }
        public long DepositId { get; set; }
        public string Payer { get; set; }
        public string IssuingBank { get; set; }
        public string ValueDate { get; set; }
        public string NameOnCheque { get; set; }
        public decimal PercentageCommission { get; set; }

        public string NotType { get; set; }
        public decimal Commission { get; set; }
        public POS POS { get; set; } = new POS();
        public DepositListingModel() { }
        public DepositListingModel(Deposit obj, bool changeStatusForApi = false)
        {
            var log = obj.DepositLogs.FirstOrDefault(s => s.DepositId == obj.DepositId);
            var approver = log?.User?.Name + " " + log?.User?.SurName;
            NotType = "deposit";
            Type = obj.PaymentType1.Name;
            UserName = approver;
            PosNumber = obj.POS != null ? obj.POS.SerialNumber : "";
            VendorName = obj.POS.User.Vendor;
            ChkNoOrSlipId = obj.CheckNumberOrSlipId; 
            Comments = obj.Comments;
            Bank = obj.BankAccount == null ? "GTBANK" : obj.BankAccount.BankName;
            if (!changeStatusForApi)
                Status = ((DepositPaymentStatusEnum)obj.Status).ToString();
            else
            {
                if (obj.Status == (int)DepositPaymentStatusEnum.Pending)
                    Status = "ProcessPending";
                else if (obj.Status == (int)DepositPaymentStatusEnum.RejectedByAccountant || obj.Status == (int)DepositPaymentStatusEnum.Rejected)
                    Status = "Rejected";
                else if (obj.Status == (int)DepositPaymentStatusEnum.ApprovedByAccountant)
                    Status = "Processing";
                else if (obj.Status == (int)DepositPaymentStatusEnum.Released)
                    Status = "Approved";
            }
            Amount = Utilities.FormatAmount(obj.Amount);
            NewBalance = obj.NewBalance == null ? Utilities.FormatAmount(obj.Amount) : Utilities.FormatAmount(obj.NewBalance.Value);
            PercentageAmount = Utilities.FormatAmount(obj.PercentageAmount);
            CreatedAt = obj.CreatedAt.ToString("dd/MM/yyyy hh:mm");//ToString("dd/MM/yyyy HH:mm");
            TransactionId = obj.TransactionId;
            DepositId = obj.DepositId;
            //Balance = obj.User.Balance == null ? 0 : obj.User.Balance.Value;
            Payer = !string.IsNullOrEmpty(obj.NameOnCheque) ? obj.NameOnCheque : "";
            IssuingBank = obj.ChequeBankName; //!= null ? obj.ChequeBankName + '-' + obj.BankAccount.AccountNumber.Replace("/", string.Empty).Substring(obj.BankAccount.AccountNumber.Replace("/", string.Empty).Length - 3) : "";
            ValueDate = obj.ValueDate == null ? obj.CreatedAt.ToString("dd/MM/yyyy hh:mm") : obj.ValueDate;
            PercentageCommission = obj.POS.Commission.Percentage;
            ValueDate = obj.ValueDateStamp == null ? ValueDate : obj.ValueDateStamp.Value.ToString("dd/MM/yyyy hh:mm");
            Commission = obj.PercentageAmount.Value - obj.Amount;
        }

        public DepositListingModel(PendingDeposit obj)
        {
            NotType = "deposit";
            Type = obj.PaymentType1.Name;
            UserName = "";
            PosNumber = obj.POS != null ? obj.POS.SerialNumber : "";
            VendorName = obj.POS.User.Vendor;
            ChkNoOrSlipId = obj.CheckNumberOrSlipId; 
            Comments = obj.Comments;
            // Bank = obj.PendingBankAccount == null ? "GTBANK" : obj.BankAccount.BankName;
            Status = "ProcessPending";
            Amount = Utilities.FormatAmount(obj.Amount);
            NewBalance = obj.NewBalance == null ? Utilities.FormatAmount(obj.Amount) : Utilities.FormatAmount(obj.NewBalance.Value);
            PercentageAmount = Utilities.FormatAmount(obj.PercentageAmount);
            CreatedAt = obj.CreatedAt.ToString("dd/MM/yyyy hh:mm");//ToString("dd/MM/yyyy HH:mm");
            TransactionId = obj.TransactionId;
            DepositId = obj.PendingDepositId;
            //Balance = obj.User.Balance == null ? 0 : obj.User.Balance.Value;
            Payer = !string.IsNullOrEmpty(obj.NameOnCheque) ? obj.NameOnCheque : "";
            IssuingBank = obj.ChequeBankName; //!= null ? obj.ChequeBankName + '-' + obj.BankAccount.AccountNumber.Replace("/", string.Empty).Substring(obj.BankAccount.AccountNumber.Replace("/", string.Empty).Length - 3) : "";
            ValueDate = obj.ValueDate == null ? obj.CreatedAt.ToString("dd/MM/yyyy hh:mm") : obj.ValueDate;
            PercentageCommission = obj.POS.Commission.Percentage;
            ValueDate = obj.ValueDateStamp == null ? ValueDate : obj.ValueDateStamp.Value.ToString("dd/MM/yyyy hh:mm");
        }
    }

    public class DepositListingModelMobile
    {
        public string UserName { get; set; }
        public string VendorName { get; set; }
        public string ChkNoOrSlipId { get; set; }
        public string Type { get; set; }
        public string Comments { get; set; }
        public string Bank { get; set; }
        public string Status { get; set; }
        public string PosNumber { get; set; }
        public string CreatedAt { get; set; }
        public string TransactionId { get; set; }
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public decimal NewBalance { get; set; }
        public decimal? PercentageAmount { get; set; }
        public long DepositId { get; set; }
        public string Payer { get; set; }
        public string IssuingBank { get; set; }
        public string ValueDate { get; set; }
        public string NameOnCheque { get; set; }
        public decimal PercentageCommission { get; set; }
        public POS POS { get; set; } = new POS();
        public DepositListingModelMobile() { }
        public DepositListingModelMobile(Deposit obj, bool changeStatusForApi = false)
        {
            Type = obj.PaymentType1.Name;
            UserName = "";
            PosNumber = obj.POS != null ? obj.POS.SerialNumber : "";
            VendorName = obj.POS.User.Vendor;
            ChkNoOrSlipId = obj.CheckNumberOrSlipId;
            Comments = obj.Comments;
            Bank = obj.BankAccount == null ? "GTBANK" : obj.BankAccount.BankName;
            if (!changeStatusForApi)
                Status = ((DepositPaymentStatusEnum)obj.Status).ToString();
            else
            {
                if (obj.Status == (int)DepositPaymentStatusEnum.Pending)
                    Status = "ProcessPending";
                else if (obj.Status == (int)DepositPaymentStatusEnum.RejectedByAccountant || obj.Status == (int)DepositPaymentStatusEnum.Rejected)
                    Status = "Rejected";
                else if (obj.Status == (int)DepositPaymentStatusEnum.ApprovedByAccountant)
                    Status = "Processing";
                else if (obj.Status == (int)DepositPaymentStatusEnum.Released)
                    Status = "Approved";
            }
            Amount = obj.Amount;
            NewBalance = obj.NewBalance == null ? obj.Amount : obj.NewBalance.Value;
            PercentageAmount = obj.PercentageAmount;
            CreatedAt = obj.CreatedAt.ToString("dd/MM/yyyy hh:mm");//ToString("dd/MM/yyyy HH:mm");
            TransactionId = obj.TransactionId;
            DepositId = obj.DepositId;
            //Balance = obj.User.Balance == null ? 0 : obj.User.Balance.Value;
            Payer = !string.IsNullOrEmpty(obj.NameOnCheque) ? obj.NameOnCheque : "";
            IssuingBank = obj.ChequeBankName; //!= null ? obj.ChequeBankName + '-' + obj.BankAccount.AccountNumber.Replace("/", string.Empty).Substring(obj.BankAccount.AccountNumber.Replace("/", string.Empty).Length - 3) : "";
            ValueDate = obj.ValueDate == null ? obj.CreatedAt.ToString("dd/MM/yyyy hh:mm") : obj.ValueDate;
            PercentageCommission = obj.POS.Commission.Percentage;
            ValueDate = obj.ValueDateStamp == null ? ValueDate : obj.ValueDateStamp.Value.ToString("dd/MM/yyyy hh:mm");
        }

        public DepositListingModelMobile(PendingDeposit obj, bool changeStatusForApi = false)
        {
            Type = obj.PaymentType1.Name;
            UserName = "";
            PosNumber = obj.POS != null ? obj.POS.SerialNumber : "";
            VendorName = obj.POS.User.Vendor;
            ChkNoOrSlipId = obj.CheckNumberOrSlipId;
            Comments = obj.Comments;
            // Bank = obj.PendingBankAccount == null ? "GTBANK" : obj.BankAccount.BankName;
            Status = "ProcessPending";
            Amount = obj.Amount;
            NewBalance = obj.NewBalance == null ? obj.Amount : obj.NewBalance.Value;
            PercentageAmount = obj.PercentageAmount;
            CreatedAt = obj.CreatedAt.ToString("dd/MM/yyyy hh:mm");//ToString("dd/MM/yyyy HH:mm");
            TransactionId = obj.TransactionId;
            DepositId = obj.PendingDepositId;
            //Balance = obj.User.Balance == null ? 0 : obj.User.Balance.Value;
            Payer = !string.IsNullOrEmpty(obj.NameOnCheque) ? obj.NameOnCheque : "";
            IssuingBank = obj.ChequeBankName; //!= null ? obj.ChequeBankName + '-' + obj.BankAccount.AccountNumber.Replace("/", string.Empty).Substring(obj.BankAccount.AccountNumber.Replace("/", string.Empty).Length - 3) : "";
            ValueDate = obj.ValueDate == null ? obj.CreatedAt.ToString("dd/MM/yyyy hh:mm") : obj.ValueDate;
            PercentageCommission = obj.POS.Commission.Percentage;
            ValueDate = obj.ValueDateStamp == null ? ValueDate : obj.ValueDateStamp.Value.ToString("dd/MM/yyyy hh:mm");
        }
    }

    public class DepositExcelReportModel
    {
        [DisplayName("Date/Time")]
        public string DATE_TIME { get; set; }
        public string VALUEDATE { get; set; }
        public string POSID { get; set; }
        public string VENDOR { get; set; }
        public string USERNAME { get; set; }
        public string DEPOSIT_TYPE { get; set; }
        public string BANK { get; set; }
        [DisplayName("TRANSACTION ID")]
        public string TRANSACTION_ID { get; set; }
        public string DEPOSIT_REF_NO { get; set; }
        public string AMOUNT { get; set; }
        [DisplayName("%")]
        public string PERCENT { get; set; }
        public string NEW_BALANCE { get; set; }
        public DepositExcelReportModel(Deposit obj, bool changeStatusForApi = false)
        {
            var approver = obj.DepositLogs.FirstOrDefault(d => d.DepositId == obj.DepositId);
            DATE_TIME = obj.CreatedAt.ToString("dd/MM/yyyy hh:mm");      //ToString("dd/MM/yyyy HH:mm");
            VENDOR = obj.POS.User.Vendor;
            USERNAME = approver.User.Name + " " + approver.User.SurName;
            POSID = obj.POS != null ? obj.POS.SerialNumber : "";
            DEPOSIT_REF_NO = obj.CheckNumberOrSlipId;
            DEPOSIT_TYPE = obj.PaymentType1.Name;
            BANK = obj.BankAccount == null ? "GTBANK" : obj.BankAccount.BankName;
            AMOUNT = Utilities.FormatAmount(obj.Amount);
            NEW_BALANCE = obj.NewBalance == null ? Utilities.FormatAmount(obj.Amount) : Utilities.FormatAmount(obj.NewBalance.Value);
            PERCENT = Utilities.FormatAmount(obj.PercentageAmount);
            TRANSACTION_ID = obj?.TransactionId;
            VALUEDATE = obj.ValueDateStamp == null ? obj.CreatedAt.ToString("dd/MM/yyyy hh:mm") : obj.ValueDateStamp.Value.ToString("dd/MM/yyyy hh:mm");
            //Balance = obj.User.Balance == null ? 0 : obj.User.Balance.Value;

        }

        public DepositExcelReportModel()
        {
        }
    }

    public class AgencyRevenueExcelReportModel
    { 
        public string DATE_TIME { get; set; } 
        public string POSID { get; set; }
        public string VENDOR { get; set; } 
        public string DEPOSIT_TYPE { get; set; }  
        public string TRANSACTION_ID { get; set; }
        public string DEPOSIT_REF_NO { get; set; }
        public string AMOUNT { get; set; } 
        public string VENDORPERCENT { get; set; }
        public string AGENTPERCENT { get; set; }
        public AgencyRevenueExcelReportModel(Deposit obj, bool changeStatusForApi = false)
        {
            var approver = obj.DepositLogs.FirstOrDefault(d => d.DepositId == obj.DepositId);
            DATE_TIME = obj.CreatedAt.ToString("dd/MM/yyyy hh:mm");      //ToString("dd/MM/yyyy HH:mm");
            VENDOR = obj.POS.User.Vendor; 
            POSID = obj.POS != null ? obj.POS.SerialNumber : "";
            DEPOSIT_REF_NO = obj.CheckNumberOrSlipId;
            DEPOSIT_TYPE = obj.PaymentType1.Name;
            AMOUNT = Utilities.FormatAmount(obj.Amount); 
            TRANSACTION_ID = obj?.TransactionId;  
            VENDORPERCENT = Utilities.FormatAmount(obj.PercentageAmount);
            AGENTPERCENT = Utilities.FormatAmount(obj.AgencyCommission);
        }

        public AgencyRevenueExcelReportModel()
        {
        }
    }

    public class DepositAuditExcelReportModel
    {
        [DisplayName("Date/Time")]
        public string DATE_TIME { get; set; }
        public string VALUEDATE { get; set; }
        public string TRANSACTIONID { get; set; }
        public string POSID { get; set; }
        [DisplayName("DepositBy")]
        public string DEPOSIT_BY { get; set; }
        [DisplayName("DepositType")]
        public string DEPOSIT_TYPE { get; set; }
        public string GTBANK { get; set; }
        [DisplayName("PayerBank")]
        public string ISSUINGBANK { get; set; }
        public string PAYER { get; set; }
        [DisplayName("DepositRef#")]
        public string DEPOSIT_REF_NO { get; set; }
        public string AMOUNT { get; set; }
        public string STATUS { get; set; }

        public DepositAuditExcelReportModel(Deposit obj, bool changeStatusForApi = false)
        {
            DATE_TIME = obj.CreatedAt.ToString("dd/MM/yyyy hh:mm");      //ToString("dd/MM/yyyy HH:mm");
            VALUEDATE = obj.ValueDateStamp == null ? obj.ValueDate : obj.ValueDateStamp.Value.ToString("dd/MM/yyyy hh:mm");
            POSID = obj.POS != null ? obj.POS.SerialNumber : "";
            DEPOSIT_BY = obj.POS.User.Vendor;
            DEPOSIT_TYPE = obj.PaymentType1.Name;
            PAYER = obj.NameOnCheque == null ? "" : obj.NameOnCheque;
            ISSUINGBANK = !string.IsNullOrEmpty(obj.ChequeBankName) ? obj.ChequeBankName.IndexOf('-') == -1 ? obj.ChequeBankName : obj.ChequeBankName.Substring(0, obj.ChequeBankName.IndexOf("-")) : "";
            DEPOSIT_REF_NO = obj.CheckNumberOrSlipId;
            GTBANK = obj.BankAccount.BankName;
            AMOUNT = Utilities.FormatAmount(obj.Amount);
            STATUS = Convert.ToBoolean(obj.isAudit) ? "Cleared" : "Open";
            TRANSACTIONID = obj.TransactionId;
        }

        public DepositAuditExcelReportModel()
        {
        }
    }

    public class DepositLogListingModel
    {
        public string UserName { get; set; }
        public string DepositerName { get; set; }
        public string PreviousStatus { get; set; }
        public string NewStatus { get; set; }
        public string CreatedAt { get; set; }
        public string VendorName { get; set; }
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public decimal? PercentageAmount { get; set; }
        public long DepositId { get; set; }
        public string PosNumber { get; set; }

        public string TransactionId { get; set; }
        public long DepositLogId { get; set; }
        public string NameOnCheque { get; set; }
        public DepositLogListingModel(DepositLog obj)
        {
            UserName = obj.User.Name + " " + obj.User.SurName;
            PreviousStatus = ((DepositPaymentStatusEnum)obj.PreviousStatus).ToString();
            NewStatus = ((DepositPaymentStatusEnum)obj.NewStatus).ToString();
            VendorName = obj.Deposit.POS.User.User1 == null ? "" : obj.Deposit.POS.User.User1.Vendor;
            Amount = obj.Deposit.Amount;
            PercentageAmount = obj.Deposit.PercentageAmount;
            TransactionId = obj.Deposit.TransactionId;
            CreatedAt = obj.CreatedAt.ToString("dd/MM/yyyy hh:mm");//ToString("dd/MM/yyyy hh:mm:ss tt"); ;
            DepositId = obj.DepositId;
            DepositLogId = obj.DepositLogId;
            DepositerName = obj.Deposit.User.Name + " " + obj.Deposit.User.SurName;
            //Balance = obj.Deposit.User.Balance == null ? 0 : obj.Deposit.User.Balance.Value;
            NameOnCheque = obj.Deposit.NameOnCheque;
        }
    }

    public class DepositModel
    {

        public long UserId { get; set; }
        public long VendorId { get; set; }
        [Required(ErrorMessage = "Please select POS ID")]
        public long PosId { get; set; }
        public int BankAccountId { get; set; }
        public DepositPaymentTypeEnum DepositType { get; set; }
        [Required(ErrorMessage = "Cheque Or Slip No is required")]
        public string ChkOrSlipNo { get; set; }
        public string ChkBankName { get; set; }
        public string NameOnCheque { get; set; }
        [Required]
        public decimal Amount { get; set; }

        public int Percentage { get; set; }
        public decimal TotalAmountWithPercentage { get; set; }
        public string Comments { get; set; }
        public string ValueDate { get; set; }
        public long ContinueDepoit { get; set; } = 0;

        public string SmartKorporResponse { get; set; } = "";
        public List<DepositListingModel> History { get; set; } = new List<DepositListingModel>();
    }
    public class ReleaseDepositModel
    {
        public List<long> ReleaseDepositIds { get; set; }
        public List<long> CancelDepositIds { get; set; }
        public string OTP { get; set; }
    }

    public class ReleaseDepositModel2
    {
        public List<long> ReleaseDepositIds { get; set; }
    }
    public class SignalRMessageBody
    {
        public SignalRMessageBody()
        {
            UserId = string.Empty;
            Message = string.Empty;
        }
        public string UserId { get; set; }
        public string Message { get; set; }
    }
    public class CancelDepositModel
    {
        public long CancelDepositId { get; set; }
    }

    public class AutoRelease
    {
        public string Message { get; set; }
        public bool Success { get; set; }
    }

    public class ReverseDepositModel
    {
        public List<long> ReverseDepositIds { get; set; }
        public List<long> CancelDepositIds { get; set; }
        public string OTP { get; set; }
    }

    public class MeterAndDepositListingModel
    {
        public List<DepositListingModel> Deposits { get; set; }
        public List<MeterRechargeApiListingModel> Recharges { get; set; }
    }

    public class ReportSearchModel
    {
        public ReportSearchModel()
        {
            Meter = "";
            if (PageNo <= 1)
            {
                PageNo = 1;
            }
            if (RecordsPerPage <= 0)
            {
                RecordsPerPage = AppDefaults.PageSize;
            }
        }
        public long? VendorId { get; set; }
        public long? AgencyId { get; set; }
        public string ReportType { get; set; }
        public string ProductShortName { get; set; }
        public string Product { get; set; }
        public string ProductId { get; set; }
        public long? PosId { get; set; } = 0;
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime? From { get; set; } = null;
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime? To { get; set; } = null;
        public string Meter { get; set; } = "";
        public string RefNumber { get; set; }
        public string TransactionId { get; set; } = "";
        public string RechargeToken { get; set; }
        public int? Bank { get; set; }
        public int? DepositType { get; set; }
        public int PageNo { get; set; }
        public int RecordsPerPage { get; set; }
        public string SortBy { get; set; }
        public string SortOrder { get; set; }
        public string Payer { get; set; }
        public string IssuingBank { get; set; }
        public string Amount { get; set; }
        public bool IsAudit { get; set; }
        public string Status { get; set; }
        public bool IsInitialLoad { get; set; } = false; 
        public string MiniSaleRpType { get; set; }
        public int PlatformId { get;set; }
        public int PlatformType { get; set; }
    }

    public class DepositAuditModel
    {
        public long DepositId { get; set; }
        public string DateTime { get; set; }
        public string PosId { get; set; }
        public string DepositBy { get; set; }
        public string Payer { get; set; }
        public string IssuingBank { get; set; }
        public string DepositRef { get; set; }
        public string GTBank { get; set; }
        public decimal Amount { get; set; }
        public string VendorName { get; set; }
        public string Type { get; set; }
        public int PaymentType { get; set; }
        public string Status { get; set; }
        public string CreatedAt { get; set; }
        public string TransactionId { get; set; }
        public long Id { get; set; }
        public bool isAudit { get; set; }
        public long UserId { get; set; }
        public string Price { get; set; }
        public DateTime ValueDate { get; set; }
        public string ValueDateModel { get; set; }
        public string Comment { get; set; }

        public DepositAuditModel() { }
        public DepositAuditModel(Deposit obj, bool changeStatusForApi = false)
        {
            Id = obj.DepositId;
            isAudit = Convert.ToBoolean(obj.isAudit);
            UserId = obj.UserId;
            DepositBy = obj.POS.User.Vendor.Trim();
            PosId = obj.POS != null ? obj.POS.SerialNumber : "";
            VendorName = !string.IsNullOrEmpty(obj.User.Vendor) ? obj.User.Vendor : obj.User.Name + " " + obj.User.SurName;
            DepositRef = obj.CheckNumberOrSlipId;
             
            Type = obj.PaymentType1.Name;
            PaymentType = obj.PaymentType;

            GTBank = obj.BankAccount.BankName;
            Payer = string.IsNullOrEmpty(obj.NameOnCheque) ? "" : obj.NameOnCheque; //obj.PaymentType != 4 ? !string.IsNullOrEmpty(obj.NameOnCheque) ? obj.NameOnCheque : "": obj.User.Agency.User.Vendor;
            IssuingBank = obj.ChequeBankName != null ? obj.ChequeBankName + '-' + obj.BankAccount.AccountNumber.Replace("/", string.Empty).Substring(obj.BankAccount.AccountNumber.Replace("/", string.Empty).Length - 3) : "GTB - (GUARANTEE TRUST BANK)";
            Amount = obj.Amount;
            CreatedAt = obj.CreatedAt.ToString("dd/MM/yyyy hh:mm");
            TransactionId = obj.TransactionId;
            if (obj.ValueDate != " 12:00")
                ValueDateModel = obj.ValueDate == null ? new DateTime().ToString("dd/MM/yyyy hh:mm") : obj.ValueDate;
            Comment = obj.Comments;
            ValueDateModel = obj.ValueDate == null ? ValueDateModel : obj.ValueDate;
            //ValueDate = obj.ValueDateStamp == null ? ValueDateModel : obj.ValueDateStamp.Value.ToString("dd/MM/yyyy hh:mm");
        }
    } 

    public class DepositAuditLiteDto
    {
        public SelectList Vendor { get; set; }
        public SelectList IssuingBank { get; set; }
        public SelectList DepositType { get; set; }
    }

    public class AgentRevenueListingModel
    {
        public string UserName { get; set; }
        public string VendorName { get; set; }
        public string ChkNoOrSlipId { get; set; }
        public string Type { get; set; } 
        public string PosNumber { get; set; }
        public string CreatedAt { get; set; }
        public string TransactionId { get; set; } 
        public decimal Amount { get; set; } 
        public decimal? AgentPercentageAmount { get; set; } 
        public decimal? VendorPercentageAmount { get; set; }
        public decimal? AgentPercentage { get; set; }
        public decimal? AgencyCommission { get; set; }
        public decimal? VendorPercentage { get; set; }
        public long DepositId { get; set; }   
        public AgentRevenueListingModel(Deposit obj)
        {
            Type = obj.PaymentType1.Name;
            UserName = obj.DepositLogs.Any() ?
                obj.DepositLogs.FirstOrDefault(s => s.DepositId == obj.DepositId)?.User?.Name + " " + obj.DepositLogs.FirstOrDefault(s => s.DepositId == obj.DepositId)?.User?.SurName :
                obj.User.Name + " " + obj.User.SurName;
            PosNumber = obj.POS != null ? obj.POS.SerialNumber : "";
            VendorName = obj.POS.User.Vendor;
            ChkNoOrSlipId = obj.CheckNumberOrSlipId; 
            CreatedAt = obj.CreatedAt.ToString("dd/MM/yyyy hh:mm");//ToString("dd/MM/yyyy HH:mm");
            TransactionId = obj.TransactionId;
            DepositId = obj.DepositId;
            Amount = obj.Amount;
            AgentPercentageAmount = obj.AgencyCommission;
            VendorPercentageAmount = obj.PercentageAmount;
            VendorPercentage = obj?.User?.Commission?.Percentage;
            AgentPercentage = obj?.User?.Agency?.Commission?.Percentage;
            AgencyCommission = obj.AgencyCommission;
        }

       
    }

    public class VendorStatus
    {
        public long userid { get; set; }
        public string vendor { get; set; }
        public decimal? totaldeposits { get; set; } = 0;
        public decimal? totalsales { get; set; } = 0;
        public decimal? runningbalance { get; set; } = 0;
        public decimal? POSBalance { get; set; }
        public decimal? overage { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal? PercentageAmount { get; set; } = 0;
    }

    public class Dep1
    {
        public DateTime CreatedAt { get; set; }
        public string TransactionId { get; set; }
        public long UserId { get; set; }
        public decimal? NewBalance { get; set; }
        public decimal? PercentageAmount { get; set; }
        public decimal? Totalsales { get; set; }
        public decimal? AgencyCommission { get; set; }
    }


    public class DepTrans1
    {
        public long UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TransactionId { get; set; }
        public decimal? CurrentVendorBalance { get; set; }
        public decimal? AgencyCommission { get; internal set; }
        public decimal? NewBalance { get; internal set; }
    }

    public partial class DepositDTOV2
    {
        public DepositDTOV2()
        {
            IsAudit = false;
            Comments = "";
            ValueDate = Utilities.formatDate(DateTime.UtcNow);
            UpdatedAt = DateTime.UtcNow;
            Status = (int)DepositPaymentStatusEnum.Released;
            NextReminderDate = DateTime.UtcNow.AddDays(15);
            ValueDateStamp = DateTime.UtcNow;
            Approver = 40249;
        }
        public long DepositId { get; set; }
        public long UserId { get; set; }
        public long POSId { get; set; }
        public DateTime CreatedAt { get; set; }
        //public string TransactionId { get; set; }
        public int PaymentType { get; set; }
        public decimal? BalanceBefore { get; set; }
        public decimal Amount { get; set; }
        public string FirstDepositTransactionId { get; set; }
        public string CheckNumberOrSlipId { get; set; }
        public string Comments { get; set; }
        public int Status { get; set; }
        public string ChequeBankName { get; set; }
        public string NameOnCheque { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int BankAccountId { get; set; }
        public bool IsAudit { get; set; }
        public string ValueDate { get; set; }
        public long Approver { get; set; } = 40249;
        public DateTime? NextReminderDate { get; set; }
        public DateTime? ValueDateStamp { get; set; }
    }
}
