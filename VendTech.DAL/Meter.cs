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
    
    public partial class Meter
    {
        public long MeterId { get; set; }
        public long UserId { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string MeterMake { get; set; }
        public bool IsDeleted { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public Nullable<System.DateTime> UpdatedAt { get; set; }
        public Nullable<bool> IsVerified { get; set; }
        public string Allias { get; set; }
        public bool IsSaved { get; set; }
        public int NumberType { get; set; }
    
        public virtual User User { get; set; }
    }
}
