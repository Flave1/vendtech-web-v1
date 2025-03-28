﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using VendTech.BLL.Common;
using VendTech.DAL;

namespace VendTech.BLL.Models
{
    public class POSListingModel
    {
        public long POSId { get; set; }
        public string VendorName { get; set; }
        public string Products { get; set; }

        public string Agency { get; set; }
        public string SerialNumber { get; set; }
        public string Phone { get; set; }
        public string VendorType { get; set; }
        public decimal Balance { get; set; }
        public bool Enabled { get; set; }
        public bool EmailNotificationSales { get; set; }
        public bool SMSNotificationSales { get; set; }

        public bool EmailNotificationDeposit { get; set; }
        public bool SMSNotificationDeposit { get; set; }
        public long UserId { get; set; } 
        public int POSCount { get; set; }
        public decimal? Percentage { get; set; } = 0;
        public bool WebSms { get; set; }
        public bool PosSms { get; set; }
        public bool PosPrint { get; set; }
        public bool WebPrint { get; set; }
        public bool WebBarcode { get; set; }
        public bool PosBarcode { get; set; }
        public string MobileAppVersion { get; set; }
        public POSListingModel(POS obj)
        {
            POSId = obj.POSId;
            VendorName = (obj.User == null ? "" : obj.User.Vendor);
            SerialNumber = obj.SerialNumber;
            Phone = obj?.User?.Phone ?? "";
            VendorType = obj.VendorType == null ? "" : ((PosTypeEnum)obj.VendorType).ToString();
            Enabled = obj.Enabled == null ? false : obj.Enabled.Value;
            Balance = obj.Balance == null ? 0 : obj.Balance.Value;
            SMSNotificationDeposit = Convert.ToBoolean(obj.SMSNotificationDeposit); // == null ? 0 : obj.SMSNotificationDeposit.Value;
            EmailNotificationDeposit = Convert.ToBoolean(obj.EmailNotificationDeposit); // == null ? 0 : obj.SMSNotificationDeposit.Value;
            EmailNotificationSales = Convert.ToBoolean(obj.EmailNotificationSales); // == null ? 0 : obj.SMSNotificationDeposit.Value;
            SMSNotificationSales = Convert.ToBoolean(obj.SMSNotificationSales); // == null ? 0 : obj.SMSNotificationDeposit.Value;
            Balance = obj.Balance == null ? 0 : obj.Balance.Value;
            UserId = obj?.User?.UserId??0;
            POSCount = obj?.User?.Meters?.Count(d => d.IsDeleted == false && d.IsSaved == true && d.IsVerified == true && d.NumberType == (int)NumberTypeEnum.MeterNumber) ??0;
            Percentage = obj.Commission.Percentage;
            WebSms = obj?.WebSms ?? false;
            PosSms = obj?.PosSms ?? false;
            PosPrint = obj?.PosPrint ?? false;
            WebPrint = obj?.WebPrint ?? false;
            WebBarcode = obj?.WebBarcode ?? false;
            PosBarcode = obj?.PosBarcode ?? false;
            Agency = obj?.User?.Agency?.AgencyName;
            MobileAppVersion = obj.User?.MobileAppVersion ?? "2.2";
            Products = string.Join(", ", obj?.POSAssignedPlatforms.Select(x => x.Platform.ShortName).ToList());
        } 
    }
    public class PosAPiListingModel
    {
        public string SerialNumber { get; set; }
        public long PosId { get; set; }
        public string Balance { get; set; }
        public decimal Percentage { get; set; }
        public PosAPiListingModel(POS obj)
        {
            PosId = obj.POSId;
            SerialNumber = obj.SerialNumber;
            Balance = Utilities.FormatAmount(obj.Balance);
            Percentage = obj.Commission.Percentage;
        }
    }
    public class SavePosModel
    {
        [Required(ErrorMessage = "Required")]
        public string SerialNumber { get; set; }

        [Required(ErrorMessage = "Required This Field Must Be Unique")]
        [Index(IsUnique = true)]
        public long POSId { get; set; }
        public long? VendorId { get; set; }

        [Required(ErrorMessage = "Required")]
        [MaxLength(10)]
        public string Phone { get; set; }
        public string CountryCode { get; set; }
        public string Type { get; set; }
        public bool Enabled { get; set; }
        public IList<PlatformCheckbox> PlatformList { get; set; }
        public List<int> SelectedPlatforms { get; set; }
        public bool EmailNotificationDeposit { get; set; }
        public bool SMSNotificationDeposit { get; set; }
        public bool SMSNotificationSales { get; set; }
        public bool EmailNotificationSales { get; set; }
        public int? Percentage { get; set; }
        public string Email { get; set; }
        public string PassCode { get; set; }

        public bool WebSms { get; set; }
        public bool PosSms { get; set; }
        public bool WebPrint { get; set; }
        public bool PosPrint { get; set; }
        public bool WebBarcode { get; set; }
        public bool PosBarcode { get; set; }
        public IEnumerable<SelectListItem> PosTypes { get; set; }
        public IEnumerable<SelectListItem> Commmissions { get; set; }
    }

    public class SavePassCodeModel
    {
        public long POSId { get; set; }
        public long? VendorId { get; set; }
        public string Phone { get; set; }
        public string CountryCode { get; set; }
        public string Email { get; set; }
        public string PassCode { get; set; }
        public string PosNumber { get; set; }
        public string Name { get; set; }
    }

    public class GeneratePasscode
    {
        public long POSId { get; set; } 
        public string CountryCode { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public long VendorId { get; set; }
    }

    public class PosSelectItem
    {
        public string Text { get; set; }
        public string Value { get; set; }
        public int? Percentage { get; set; }
    }
    public class RestPassCodeModel
    {  
        public string Email { get; set; } 
    }

    public class SavePassCodeApiModel
    {
        public long UserId { get; set; }
        [Required]
        public string Phone { get; set; }
        public string CountryCode { get; set; }
        public string Email { get; set; }
        public string PassCode { get; set; }
    }
}
