﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Web.Configuration;
using VendTech.BLL.Common;
using VendTech.DAL;

namespace VendTech.BLL.Models
{
    public class MeterModel
    {
        public long UserId { get; set; }
        public long? MeterId { get; set; }
        [Required(ErrorMessage = "Meter name is required")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Meter # is required"), MaxLength(11, ErrorMessage = "Meter # must be of 11 digits"), MinLength(11, ErrorMessage = "Meter # must be of 11 digits")]
        public string Number { get; set; }
        public string Address { get; set; }
        public string MeterMake { get; set; }
        public string Alias { get; set; }
        public bool isVerified { get; set; } = true;
        public bool IsSaved { get; set; }
        public int NumberType { get; set; }
        public bool IsDisable { get; set; }
    }

    public class NumberModel
    {
        public long UserId { get; set; }
        public long MeterId { get; set; }
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Number # is required"), MaxLength(8, ErrorMessage = "Number # must be of 8 digits"), MinLength(8, ErrorMessage = "Number # must be of 8 digits")]
        public string Number { get; set; }
        public string Address { get; set; }
        public string MeterMake { get; set; }
        public string Alias { get; set; }
        public bool isVerified { get; set; } = true;
        public bool IsSaved { get; set; }
        public int NumberType { get; set; }
        public bool IsDisable { get; set; }
    }

    public class MeterAPIListingModel : MeterModel
    { 
        public string UserName { get; set; }
        public DateTime CreatedAt { get; set; }
        public long POSId { get; set; }
        public string POSSerialNumber { get; set; }
        public string Balance { get; set; }
        public bool PlatformDisabled { get; set; }
        public int PlatformId { get; set; }
        public string Vendor { get; set; } = "";
        public MeterAPIListingModel() { }
        public MeterAPIListingModel(Meter obj)
        {
            var pos = obj.User.POS.FirstOrDefault();
            UserId = obj.UserId;
            UserName = obj.User.Name + " " + obj.User.SurName;
            Name = obj.Name;
            MeterId = obj.MeterId;
            MeterMake = obj.MeterMake;
            CreatedAt = obj.CreatedAt;
            Address = obj.Address;
            Number = obj.Number;
            Alias = obj.Allias;
            isVerified = (bool)obj.IsVerified;
            POSId = pos.POSId;
            POSSerialNumber = pos.SerialNumber;
            Balance = Utilities.FormatAmount(pos.Balance);
            NumberType = obj.NumberType;
            Vendor = obj.User.Vendor;
        }
    }



    public class RechargeMeterModel
    {
        public bool IsSaved { get; set; }
        public long UserId { get; set; }
        public long? MeterId { get; set; }
        public long? PlatformId { get; set; }
        [Required(ErrorMessage = "Pos Id is Required")]
        public long POSId { get; set; }
        [Required(ErrorMessage = "Amount is Required")]
        public decimal Amount { get; set; }

        public string UserClickId { get; set; }
        public string MeterToken1 { get; set; }
        public string MeterToken2 { get; set; }
        public string MeterToken3 { get; set; }

        //[MaxLength(11, ErrorMessage = "Meter Number must be of 11 digits"), MinLength(11, ErrorMessage = "Meter Number must be of 11 digits")]
        public string MeterNumber { get; set; }
        public bool SaveAsNewMeter { get; set; }
        public long TransactionId { get; set; }
        public bool IsSame_Request { get; set; } = false;
        public List<MeterRechargeApiListingModel> History { get; set; }
        public void UpdateRequestModel(string number = null)
        {
            if (string.IsNullOrEmpty(UserClickId))
                UserClickId = "web";

            if (MeterId != null)
            {
                MeterNumber = number;
            }
            else
                IsSaved = false;
        }

        public void UpdateRequestModel(TransactionDetail trx)
        {
            TransactionId = Convert.ToInt64(trx.TransactionId);
        }

        public bool IsRequestADuplicate(TransactionDetail trx)
        {
            if (trx == null) return false;
            if(MeterNumber == trx.MeterNumber1 && Amount == trx.Amount)
            {
                return true;
            }
            return false;
        }
        public string validateRequest(User user, POS pos)
        {
            Platform platf = new Platform();
            if (PlatformId == null)
            {
                PlatformId = 1;
            }

            if (platf.DisablePlatform)
            {
                return platf.DisabledPlatformMessage;
            }

            if (user == null)
            {
                return "User does not exist";
            }

            if (pos == null)
            {
                return "POS NOT FOUND!! Please Contact Administrator.";
            }

            if (pos.Balance == null)
            {
                return "INSUFFICIENT BALANCE FOR THIS TRANSACTION.";
            }

            if (Amount > pos.Balance || pos.Balance.Value < Amount)
            {
                return "INSUFFICIENT BALANCE FOR THIS TRANSACTION.";
            }
            return "clear";
        }

        public IcekloudRequestmodel StackStatusRequestModel(RechargeMeterModel model)
        {
            var username = WebConfigurationManager.AppSettings["IcekloudUsername"].ToString();
            var password = WebConfigurationManager.AppSettings["IcekloudPassword"].ToString();
             
            return new IcekloudRequestmodel
            {
                Auth = new IcekloudAuth
                {
                    Password = password,
                    UserName = username
                },
                Request = "ProcessPrePaidVendingV1",
                Parameters = new object[]
                                     {
                        new
                        {
                            UserName = username,
                            Password = password,
                            System = "SL"
                        }, "apiV1_GetTransactionStatus", model.TransactionId
                       },
            };

        }

    }

    public class RechargeDetailPDFData
    {
        public string MeterNumber { get; set; }
        public decimal Amount { get; set; }
        public string CreatedAt { get; set; }
        public string Status { get; set; }
        public string TransactionId { get; set; }
        public string UserName { get; set; }

    }

    public class LastMeterTransaction
    {
        public string RequestDate { get; set; }
        public string LastDealerBalance { get; set; }
        public string TotalSales { get; set; }
        public string WalletBalance { get; set; }
    }
    public class MeterRechargeApiListingModel
    {
        public long RechargeId { get; set; }
        public string MeterNumber { get; set; }
        public string ProductShortName { get; set; }
        public string RechargePin { get; set; }
        public string POSId { get; set; }
        public string UserName { get; set; }
        public string VendorName { get; set; }
        public long VendorId { get; set; }
        public string Amount { get; set; }
        public string CreatedAt { get; set; }
        public string Status { get; set; }
        public string TransactionId { get; set; }
        public long MeterRechargeId { get; set; }
        public long? MeterId { get; set; }
        public long TransactionDetailsId { get; set; }
        public int? PlatformId { get; set; }
        public string PlatformName { get; set; }
        public string CreatedAtDate { get; set; }
        public string NotType { get; set; }
        public string Paymentstatus { get; set; }
        public MeterRechargeApiListingModel() { }
        public MeterRechargeApiListingModel(TransactionDetail x)
        {
            TransactionDetailsId = x.TransactionDetailsId;
            Amount = Utilities.FormatAmount(x.Amount);
            PlatformId = (int)x.PlatFormId;
            ProductShortName = x.Platform.Title;
            CreatedAt = x.CreatedAt.ToString("dd/MM/yyyy hh:mm");
            MeterNumber = x.Meter == null ? x.MeterNumber1 : x.Meter.Number;
            POSId = x.POSId == null ? "" : x.POS.SerialNumber;
            Status = ((RechargeMeterStatusEnum)x.Status).ToString();
            TransactionId = x.TransactionId;
            MeterRechargeId = x.TransactionDetailsId;
            RechargeId = x.TransactionDetailsId;
            UserName = x.User?.Name + (!string.IsNullOrEmpty(x.User.SurName) ? " " + x.User.SurName : "");
            VendorName = x.POS.User == null ? "" : x.POS.User.Vendor;
            RechargePin = x.Platform.PlatformType == 4 ? Utilities.FormatThisToken(x.MeterToken1) : x.MeterNumber1 + "/" + x.TransactionId;
            PlatformName = x.Platform.Title;
            NotType = "sale";
            if (x.PaymentStatus == (int)PaymentStatus.Pending)
                Paymentstatus = "PENDING";
            if (x.PaymentStatus == (int)PaymentStatus.Deducted)
                Paymentstatus = "DEDUCTED";
            if (x.PaymentStatus == (int)PaymentStatus.Refunded)
                Paymentstatus = "REFUNDED";
            if (x.PaymentStatus == (int)PaymentStatus.Failed)
                Paymentstatus = "FAILED";
        }

        public MeterRechargeApiListingModel(TransactionDetail x, int v)
        {
            Amount = Utilities.FormatAmount(x.Amount);
            TransactionId = x.TransactionId;
            MeterRechargeId = x.TransactionDetailsId;
            RechargeId = x.TransactionDetailsId;
            UserName = x.User.Name + (!string.IsNullOrEmpty(x.User.SurName) ? " " + x.User.SurName : "");
            PlatformId = (int)x.PlatFormId;
            ProductShortName = x.Platform.Title;
            CreatedAt = x.CreatedAt.ToString("dd/MM/yyyy hh:mm");//ToString("dd/MM/yyyy HH:mm"),
            MeterNumber = x.Meter == null ? x.MeterNumber1 : x.Meter.Number;
            POSId = x.POSId == null ? "" : x.POS.SerialNumber;
            Status = ((RechargeMeterStatusEnum)x.Status).ToString();
            VendorName = x.POS.User == null ? "" : x.POS.User.Vendor;
            RechargePin = x.Platform.PlatformType == (int)PlatformTypeEnum.ELECTRICITY ? Utilities.FormatThisToken(x.MeterToken1) : x.MeterNumber1 + "/" + x.TransactionId;
            
            if (!string.IsNullOrEmpty(x.MeterToken1) && !string.IsNullOrEmpty(x.MeterToken2) && !string.IsNullOrEmpty(x.MeterToken3))
            {
                RechargePin = x.MeterToken1 + " (+2)";
            }
            else if (x.Status == (int)RechargeMeterStatusEnum.Pending)
                    RechargePin = "";

            CreatedAtDate = x.CreatedAt.ToString("dd/MM/yyyy hh:mm");
            PlatformName = x.Platform.Title;
            NotType = "sale";
            if (x.PaymentStatus == (int)PaymentStatus.Pending)
                Paymentstatus = "PENDING";
            if (x.PaymentStatus == (int)PaymentStatus.Deducted)
                Paymentstatus = "DEDUCTED";
            if (x.PaymentStatus == (int)PaymentStatus.Refunded)
                Paymentstatus = "REFUNDED";
            if (x.PaymentStatus == (int)PaymentStatus.Failed)
                Paymentstatus = "FAILED";
        }
    }

    public class MeterRechargeApiListingModelMobile
    {
        public long RechargeId { get; set; }
        public string MeterNumber { get; set; }
        public string ProductShortName { get; set; }
        public string RechargePin { get; set; }
        public string RechargePin2 { get; set; }
        public string RechargePin3 { get; set; }
        public string POSId { get; set; }
        public string UserName { get; set; }
        public string VendorName { get; set; }
        public long VendorId { get; set; }
        public decimal Amount { get; set; }
        public string CreatedAt { get; set; }
        public string Status { get; set; }
        public string TransactionId { get; set; }
        public long MeterRechargeId { get; set; }
        public long? MeterId { get; set; }
        public long TransactionDetailsId { get; set; }
        public int? PlatformId { get; set; }
        public string PlatformName { get; set; }
        public DateTime  CreatedAtDate { get; set; }
        public string GST { get; set; }
        public string Units { get; set; }
        public string CostOfUnits { get; set; }
        public string Customer { get; set; }
        public MeterRechargeApiListingModelMobile() { }
        public MeterRechargeApiListingModelMobile(TransactionDetail x)
        {
            TransactionDetailsId = x.TransactionDetailsId;
            Amount = x.Amount;
            PlatformId = (int)x.PlatFormId;
            ProductShortName = x.Platform.Title;
            CreatedAt = x.CreatedAt.ToString("dd/MM/yyyy hh:mm");
            MeterNumber = x.Meter == null ? x.MeterNumber1 : x.Meter.Number;
            POSId = x.POSId == null ? "" : x.POS.SerialNumber;
            Status = ((RechargeMeterStatusEnum)x.Status).ToString();
            TransactionId = x.TransactionId;
            MeterRechargeId = x.TransactionDetailsId;
            RechargeId = x.TransactionDetailsId;
            UserName = x.User?.Name + (!string.IsNullOrEmpty(x.User.SurName) ? " " + x.User.SurName : "");
            VendorName = x.POS.User == null ? "" : x.POS.User.Vendor;
            RechargePin = x.Platform.PlatformType == 4 ? Utilities.FormatThisToken(x.MeterToken1) : x.MeterNumber1 + "/" + x.TransactionId;
            PlatformName = x.Platform.Title;
            
        }

        public MeterRechargeApiListingModelMobile(TransactionDetail x, int v)
        {
            Amount = x.Amount;
            TransactionId = x.TransactionId;
            MeterRechargeId = x.TransactionDetailsId;
            RechargeId = x.TransactionDetailsId;
            UserName = x.User.Name + (!string.IsNullOrEmpty(x.User.SurName) ? " " + x.User.SurName : "");
            PlatformId = (int)x.PlatFormId;
            ProductShortName = x.Platform.Title;
            CreatedAt = x.CreatedAt.ToString("dd/MM/yyyy hh:mm");//ToString("dd/MM/yyyy HH:mm"),
            MeterNumber = x.Meter == null ? x.MeterNumber1 : x.Meter.Number;
            POSId = x.POSId == null ? "" : x.POS.SerialNumber;
            Status = ((RechargeMeterStatusEnum)x.Status).ToString();
            VendorName = x.POS.User == null ? "" : x.POS.User.Vendor;
            RechargePin = x.Platform.PlatformType == 4 ? Utilities.FormatThisToken(x.MeterToken1) : x.MeterNumber1 + "/" + x.TransactionId;
            CreatedAtDate = x.CreatedAt;
            PlatformName = x.Platform.Title;
        }
    }

    public class GSTRechargeApiListingModel
    { 
        public string CreatedAt { get; set; } 
        public string TransactionId { get; set; }   
        public string Receipt { get; set; }
        public string MeterNumber { get; set; }    
        public decimal? Amount { get; set; }
        public decimal? ServiceCharge { get; set; }
        public decimal? Gst { get; set; }
        public decimal? UnitsCost { get; set; }
        public decimal? Tarrif { get; set; }
        public double Units { get; set; }
        public int? PlatformId { get; set; }
        public GSTRechargeApiListingModel() { }
        public GSTRechargeApiListingModel(TransactionDetail x)
        {
            CreatedAt = x.CreatedAt.ToString("dd/MM/yyyy hh:mm");//ToString("dd/MM/yyyy HH:mm"),
            MeterNumber = x.MeterNumber1; 
            TransactionId = x.TransactionId;
            Receipt = x.ReceiptNumber;
            ServiceCharge = x.ServiceCharge != null ? Convert.ToDecimal(x.ServiceCharge): 0 ;
            Gst = x.TaxCharge != "" ? Convert.ToDecimal(x.TaxCharge) : 0;
            UnitsCost = x.CostOfUnits != "" ? Convert.ToDecimal(x.CostOfUnits) : 0;
            Tarrif = x.Tariff != null ? Convert.ToDecimal(x.Tariff) : 0;
            Units = x.Units != "" ? Convert.ToDouble(x.Units): 0;
            Amount = x.Amount;
            PlatformId = x.PlatFormId;
        }
    }
    public class SalesReportExcelModel
    {
        public string Date_TIME { get; set; }
        public string PRODUCT_TYPE { get; set; }
        public string TRANSACTIONID { get; set; }
        public string METER_NO { get; set; }
        public string VENDORNAME { get; set; }
        public string POSID { get; set; }
        //public string Request { get; set; }
        //public string Response { get; set; }
        public string PIN { get; set; }
        public string AMOUNT { get; set; }
        public SalesReportExcelModel() { }
        public SalesReportExcelModel(TransactionDetail x)
        {
            Date_TIME = x.CreatedAt.ToString("dd/MM/yyyy HH:mm");
            PRODUCT_TYPE = x.Platform.Title;
            if (x.PlatFormId == 1)
            {
                PIN = Utilities.FormatThisToken(x.MeterToken1);
                if(!string.IsNullOrEmpty(x.MeterToken1) && !string.IsNullOrEmpty(x.MeterToken2) && !string.IsNullOrEmpty(x.MeterToken3))
                {
                    PIN = x.MeterToken1 + "\n" + x.MeterToken2 + "\n" + x.MeterToken3;
                }
            }
            else if (x.PlatFormId == 2)
                PIN = x.MeterNumber1;
            else if (x.PlatFormId == 3)
                PIN = x.MeterNumber1;
            else if (x.PlatFormId == 4)
                PIN = x.MeterNumber1;
            AMOUNT = Utilities.FormatAmount(x.Amount);
            TRANSACTIONID = x.TransactionId;
            METER_NO = x.Meter == null ? x.MeterNumber1 : x.Meter.Number;
            VENDORNAME = x.POS.User == null ? "" : x.POS.User.Vendor;
            POSID = x.POSId == null ? "" : x.POS.SerialNumber;
        }
    }

    //public class GSTSalesReportExcelModel
    //{
    //    public string MeterNumber { get; set; }
    //    public decimal Amount { get; set; }
    //    public string CreatedAt { get; set; }
    //    public string TransactionId { get; set; }
    //    public string Receipt { get; set; }
    //    public string ServiceCharge { get; set; }
    //    public decimal Gst { get; set; }
    //    public decimal UnitsCost { get; set; }
    //    public decimal Tarrif { get; set; }
    //    public decimal Units { get; set; } 
    //}

    public class MiniSalesReport
    {
        public string DateTime { get; set; }
        public string TAmount { get; set; }
    }

    public class RequestObject
    {
        public string token_string { get; set; }
        public string active { get; set; } = "active";
        public bool billVendor { get; set; } = true;
    }

    public class RequestObject1
    {
        public string Id { get; set; }
    }
}
