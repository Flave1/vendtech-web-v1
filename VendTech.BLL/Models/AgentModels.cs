﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VendTech.BLL.Common;
using VendTech.DAL;

namespace VendTech.BLL.Models
{
    public class AgencyListingModel
    {
        public long AgencyId { get; set; }
        public string AgencyName { get; set; }
        public string Admin { get; set; }
        public string AgentType { get; set; }
        public decimal Percentage { get; set; }
        public string SerialNumber { get; set; }
        public string AgencyAdminDisplayName { get; set; }
        public long? AgencyAdminPosId { get; set; }
        public string Balance { get; set; }
        public long UserId { get; set; }
        public int VendorsCount { get; set; }
        public AgencyListingModel()
        {
                
        }
        public AgencyListingModel(Agency obj)
        {
            var pos = obj?.User?.POS.FirstOrDefault();
            AgencyId = obj.AgencyId;
            AgencyName = obj.AgencyName;
            Admin = obj?.User?.Name + " " + obj?.User?.SurName;
            //AgentType = ((AgentTypeEnum)obj.AgentType).ToString();
            Percentage = obj?.Commission?.Percentage?? 0;
            SerialNumber = pos?.SerialNumber;
            AgencyAdminDisplayName = obj?.User?.Vendor + " - " + pos?.SerialNumber;
            AgencyAdminPosId = pos?.POSId;
            Balance = Utilities.FormatAmount(pos?.Balance);
            //VendorsCount = obj.Users.Where(p => p.Status != (int)UserStatusEnum.Block 
            //    && p.UserRole.Role == UserRoles.Vendor 
            //    && p.Status != (int)UserStatusEnum.Deleted 
            //    && !string.IsNullOrEmpty(p.POS.FirstOrDefault()?.SerialNumber)
            //    && !p.POS.FirstOrDefault().SerialNumber.StartsWith("AGT")).Count();
        }
    }

    public class AgentListingModel
    {
        public long POSID { get; set; }
        public string SerialNumber { get; set; }
        public string AgencyName { get; set; }
        public string CellPhone { get; set; }
        public string AgentName { get; set; }
        public bool Enabled { get; set; }
        public string TodaySales { get; set; }
        public string Balance { get; set; }  
        public string Vendor { get; set; }
        public long VendorId { get; set; }
        public string VendorEmail { get; set; }
        public AgentListingModel(POS obj)
        {
            
            POSID = obj.POSId;
            SerialNumber = obj.SerialNumber;
            AgencyName = obj?.User?.Agency?.AgencyName;
            CellPhone = "+232" + obj.User.Phone;
            AgentName = $"{obj?.User?.Name} {obj?.User?.SurName}";
            Balance = Utilities.FormatAmount(obj?.Balance);
            Enabled = (bool)obj.Enabled;
            Vendor = obj?.User?.Vendor;
            VendorId = obj.User.UserId;
            VendorEmail = obj?.User?.Email;
        }
        public AgentListingModel(POS obj, long id)
        {
            POSID = obj.POSId;
            SerialNumber = obj.SerialNumber;
            AgencyName = obj?.User?.Agency?.AgencyName;
            CellPhone = "+232" + obj.Phone;
            AgentName = $"{obj?.User?.Name} {obj?.User?.SurName}";
            Enabled = (bool)obj.Enabled;
            Balance = Utilities.FormatAmount(obj?.Balance);
            Vendor = obj?.User?.Vendor;
            VendorId = obj?.User?.UserId ?? 0;
            VendorEmail = obj?.User?.Email;
        }
        public AgentListingModel()
        {
            
        }
    }
    public class AddAgentModel : SaveAgentModel
    {

        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
    }

    public class SaveAgentModel
    {
        public string Company { get; set; }
        [Required(ErrorMessage = "Required")]
        public string AgencyName { get; set; }
        [RegularExpression(@"^[A-Za-z0-9](([_\.\-]?[a-zA-Z0-9]+)*)@([A-Za-z0-9]+)(([\.\-??]?[a-zA-Z0-9]+)*)\.([A-Za-z]{2,})$", ErrorMessage = "Invalid Email")]
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public string Phone { get; set; }
        public string CountryCode { get; set; }
        public int AgentType { get; set; }
        public int Percentage { get; set; }
        public long? Representative { get; set; }

        public string SerialNumber { get; set; }
        public long POSId { get; set; }
        public long AgencyId { get; set; }
        public IList<Checkbox> ModuleList { get; set; }
        public IList<WidgetCheckbox> WidgetList { get; set; }
        public List<int> SelectedWidgets { get; set; }
        public List<int> SelectedModules { get; set; }
    }


    public class DepositToAdmin
    {
        public long PosId { get; set; }
        public int BankAccountId { get; set; }
        public int PaymentType { get; set; }
        public string ChkOrSlipNo { get; set; }
        public decimal Amount { get; set; }
        public string ValueDate { get; set; }
        public string Bank { get; set; }
        public string NameOnCheque { get; set; }
        public string OTP { get; set; }
    }
}
