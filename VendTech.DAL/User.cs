//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace VendTech.DAL
{
    using System;
    using System.Collections.Generic;
    
    public partial class User
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public User()
        {
            this.AccountVerificationOTPs = new HashSet<AccountVerificationOTP>();
            this.Agencies = new HashSet<Agency>();
            this.ContactUs = new HashSet<ContactU>();
            this.DepositLogs = new HashSet<DepositLog>();
            this.Deposits = new HashSet<Deposit>();
            this.EmailConfirmationRequests = new HashSet<EmailConfirmationRequest>();
            this.ForgotPasswordRequests = new HashSet<ForgotPasswordRequest>();
            this.Meters = new HashSet<Meter>();
            this.Notifications = new HashSet<Notification>();
            this.PendingDeposits = new HashSet<PendingDeposit>();
            this.PlatformTransactions = new HashSet<PlatformTransaction>();
            this.POS = new HashSet<POS>();
            this.ReferralCodes = new HashSet<ReferralCode>();
            this.TokensManagers = new HashSet<TokensManager>();
            this.TransactionDetails = new HashSet<TransactionDetail>();
            this.UserAssignedModules = new HashSet<UserAssignedModule>();
            this.UserAssignedPlatforms = new HashSet<UserAssignedPlatform>();
            this.UserAssignedWidgets = new HashSet<UserAssignedWidget>();
            this.Users1 = new HashSet<User>();
        }
    
        public long UserId { get; set; }
        public string Name { get; set; }
        public string SurName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string CountryCode { get; set; }
        public string Password { get; set; }
        public Nullable<System.DateTime> DOB { get; set; }
        public int UserType { get; set; }
        public bool IsEmailVerified { get; set; }
        public int Status { get; set; }
        public Nullable<int> AppType { get; set; }
        public Nullable<int> AppUserType { get; set; }
        public string Address { get; set; }
        public Nullable<int> CityId { get; set; }
        public Nullable<int> CountryId { get; set; }
        public string ProfilePic { get; set; }
        public string DeviceToken { get; set; }
        public string UserName { get; set; }
        public string CompanyName { get; set; }
        public Nullable<System.DateTime> AppLastUsed { get; set; }
        public Nullable<long> AgentId { get; set; }
        public Nullable<int> VendorType { get; set; }
        public Nullable<int> VendorCommissionPercentage { get; set; }
        public string Vendor { get; set; }
        public Nullable<long> FKVendorId { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public Nullable<System.DateTime> UpdatedAt { get; set; }
        public string PassCode { get; set; }
        public Nullable<int> TotalPendingAppUser { get; set; }
        public Nullable<int> TotalPendingDepositRelease { get; set; }
        public Nullable<int> TotalPendingData { get; set; }
        public Nullable<bool> IsCompany { get; set; }
        public Nullable<long> UserSerialNo { get; set; }
        public string MobileAppVersion { get; set; }
        public Nullable<int> IsRedominated { get; set; }
        public Nullable<bool> AutoApprove { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<AccountVerificationOTP> AccountVerificationOTPs { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Agency> Agencies { get; set; }
        public virtual Agency Agency { get; set; }
        public virtual City City { get; set; }
        public virtual Commission Commission { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<ContactU> ContactUs { get; set; }
        public virtual Country Country { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<DepositLog> DepositLogs { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Deposit> Deposits { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<EmailConfirmationRequest> EmailConfirmationRequests { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<ForgotPasswordRequest> ForgotPasswordRequests { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Meter> Meters { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Notification> Notifications { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<PendingDeposit> PendingDeposits { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<PlatformTransaction> PlatformTransactions { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<POS> POS { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<ReferralCode> ReferralCodes { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<TokensManager> TokensManagers { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<TransactionDetail> TransactionDetails { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<UserAssignedModule> UserAssignedModules { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<UserAssignedPlatform> UserAssignedPlatforms { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<UserAssignedWidget> UserAssignedWidgets { get; set; }
        public virtual UserRole UserRole { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<User> Users1 { get; set; }
        public virtual User User1 { get; set; }
    }
}
