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
    
    public partial class EmailTemplate
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; }
        public string EmailSubject { get; set; }
        public string TemplateContent { get; set; }
        public bool TemplateStatus { get; set; }
        public System.DateTime CreatedOn { get; set; }
        public Nullable<System.DateTime> UpdatedOn { get; set; }
        public int TemplateType { get; set; }
        public Nullable<int> sortOrder { get; set; }
        public Nullable<bool> IsActive { get; set; }
        public string Desription { get; set; }
        public string TargetUser { get; set; }
        public string Receiver { get; set; }
    }
}
