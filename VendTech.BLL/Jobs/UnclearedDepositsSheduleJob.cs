using Quartz;
using System;
using System.Linq;
using System.Web.Mvc;
using VendTech.BLL.Common;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;

namespace VendTech.BLL.Jobs
{
    public class UnclearedDepositsSheduleJob : IJob
    {
     
        public void Execute(IJobExecutionContext context)
        {
            var _errorManager = DependencyResolver.Current.GetService<IErrorLogManager>();
            var _depositManager = DependencyResolver.Current.GetService<IDepositManager>();
            var _emailManager = DependencyResolver.Current.GetService<IEmailTemplateManager>();


            try
            {
                var uncleardDeposits = _depositManager.GetUnclearedDeposits();

                if (uncleardDeposits.Any())
                {
                    var emailTemplate = _emailManager.GetEmailTemplateByTemplateType(TemplateTypes.UnclearedDepositNotification);
                    if (emailTemplate.TemplateStatus)
                    {
                        foreach (var deposit in uncleardDeposits)
                        {
                            string body = emailTemplate.TemplateContent;
                            body = body.Replace("%USER%", deposit.POS.User.Name + " " + deposit.POS.User.SurName);
                            body = body.Replace("%POSID%", deposit.POS.SerialNumber);
                            body = body.Replace("%VENDOR%", deposit.User.Vendor);
                            body = body.Replace("%AMOUNT%", Utilities.FormatAmount(deposit.Amount));
                            body = body.Replace("%DEPOSITAPPROVEDDATE%", deposit.DepositLogs.FirstOrDefault()?.CreatedAt.ToString("f"));
                            body = body.Replace("%TODAY%", DateTime.UtcNow.ToString("f"));
                            //Utilities.SendEmail(deposit.User.Email, emailTemplate.EmailSubject, body); 
                            Utilities.SendEmail("favour@vendtechsl.com", emailTemplate.EmailSubject, body);
                            Utilities.SendEmail("vblell@vendtechsl.com", emailTemplate.EmailSubject, body);
                            _depositManager.UpdateNextReminderDate(deposit);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _errorManager.LogExceptionToDatabase(new System.Exception("Error ocuured trying to send uncleared deposits emails", ex));
            }
        }
    }
}
