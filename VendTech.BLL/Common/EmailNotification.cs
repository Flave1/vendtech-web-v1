using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Managers;
using VendTech.BLL.Models;
using VendTech.DAL;

namespace VendTech.BLL.Common
{
    public class EmailNotification: BaseManager
    {
        private readonly IDepositManager _depositManager;
        private readonly IUserManager _userManager;
        private readonly IEmailTemplateManager _templateManager;
        private readonly IPOSManager _posManager;

        public EmailNotification(IEmailTemplateManager templateManager, IUserManager userManager, IDepositManager depositManager, IPOSManager posManager)
        {
            _templateManager = templateManager;
            _userManager = userManager;
            _depositManager = depositManager;
            _posManager = posManager;
        }

        public void SendEmailToUserOnDepositApproval(PendingDeposit deposit)
        {
            if (deposit.POS.EmailNotificationDeposit ?? true)
            {
                var user = _userManager.GetUserDetailsByUserId(deposit.UserId);
                if (user != null)
                {
                    var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.DepositApprovedNotification);
                    if (emailTemplate.TemplateStatus)
                    {
                        string body = emailTemplate.TemplateContent;
                        body = body.Replace("%USER%", user.FirstName);
                        Utilities.SendEmail(user.Email, emailTemplate.EmailSubject, body);
                    }
                }
            }
        }
      
        public void SendEmailToAdminOnDepositAutoApproval(PendingDeposit dep, long trxId)
        {
            var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.DepositOTP);
            if (dep.POS != null)
            {
                emailTemplate.Receivers.ForEach(d =>
                {
                    var user = _userManager.GetUserDetailByEmail(d);
                    string receiverName = "";
                    if (user == null)
                        receiverName = "";
                    else
                        receiverName = user.Name;
                    string body = $"<p>Greetings {receiverName}, </p>" +
                                  $"<b>This is to inform you that a deposit has been AUTO APPROVED for</b> </br>" +
                                  "</br>" +
                                  $"Vendor Name: <b>{dep.POS.User.Vendor}</b> </br></br>" +
                                  $"POSID: <b>{dep.POS.SerialNumber}</b>  </br></br>" +
                                  $"DEPOSIT ID: <b>{trxId}</b> </br></br>" +
                                  $"REF#: <b>{dep.CheckNumberOrSlipId}</b> </br></br>" +
                                  $"Amount: <b>{Utilities.GetCountry().CurrencyCode} {Utilities.FormatAmount(dep.Amount)}</b> </br>" +
                                  $"</br>" +
                                  $"Thank You" +
                                  $"<br/>" +
                                  $"<p>{Utilities.EMAILFOOTERTEMPLATE}</p>";

                    Utilities.SendEmail(d, "VENDTECH SUPPORT | DEPOSIT AUTO APPROVAL EMAIL", body);
                });

            }
        }
        public void SendEmailOTPToAdmin(long depositId, long adminUserId)
        {
            var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.DepositOTP);
            if (emailTemplate.Receivers.Count > 0)
            {
                emailTemplate.Receivers.ForEach(d =>
                {
                    if (emailTemplate.TemplateStatus)
                    {
                        var user = _userManager.GetUserDetailByEmail(d);
                        string receiverName = "";
                        if(user == null)
                            receiverName = "";
                        else
                            receiverName = user.Name;

                        var otp = Utilities.GenerateRandomNo().ToString();
                        var releaseBtn = $"<a href='{Utilities.DomainUrl}/Admin/ReleaseDeposit/ManageDepositRelease?depositids={string.Join(",", depositId)}&otp={otp}&taskId={adminUserId}'>Release deposit</a>";
                        string body = emailTemplate.TemplateContent;
                        body = body.Replace("%otp%", otp);
                        body = body.Replace("%RELEASEOTP%", releaseBtn);
                        body = body.Replace("%USER%", receiverName);
                        Utilities.SendEmail(d, emailTemplate.EmailSubject, body);
                    }
                });
            }

        }
        public bool SendSmsToUserOnDepositApproval(PendingDeposit deposit)
        {
            if (deposit.POS.SMSNotificationDeposit ?? true)
            {
                var requestmsg = new SendSMSRequest
                {
                    Recipient = Utilities.GetCountry().CountryCode + deposit.POS.Phone,
                    Payload = $"Greetings {deposit.POS.User.Name} \n" +
                   "Your last deposit has been approved\n" +
                   "Please confirm the amount deposited reflects in your wallet correctly.\n" +
                   $"{Utilities.GetCountry().CurrencyCode}: {Utilities.FormatAmount(deposit.Amount)} \n" +
                   "VENDTECH"
                };
                return Utilities.SendSms(requestmsg).Result;
            }
            return false;
        }
    
    
        public void SendEmailToAdminOnDepositRequest(PendingDeposit pd)
        {
            var pos = _posManager.GetSinglePos(pd.POSId);
            if (pos != null)
            {

                var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.DepositRequestNotification);
                if (emailTemplate.Receivers.Count > 0)
                    foreach (var email in emailTemplate.Receivers)
                    {
                        var user = _userManager.GetUserDetailByEmail(email);
                        string receiverName = "";
                        if (user == null)
                            receiverName = "";
                        else
                            receiverName = user.Name;
                        if (emailTemplate != null)
                        {
                            if (emailTemplate.TemplateStatus)
                            {
                                string body = emailTemplate.TemplateContent;
                                body = body.Replace("%AdminUserName%", receiverName);
                                body = body.Replace("%VendorName%", pos.User.Vendor);
                                body = body.Replace("%POSID%", pos.SerialNumber);
                                body = body.Replace("%REF%", pd.CheckNumberOrSlipId);
                                body = body.Replace("%Amount%", Utilities.FormatAmount(pd.Amount));
                                body = body.Replace("%CurrencyCode%", Utilities.GetCountry().CurrencyCode);
                                Utilities.SendEmail(email, emailTemplate.EmailSubject, body);
                            }

                        }
                    }
            }
        }
    }
}
