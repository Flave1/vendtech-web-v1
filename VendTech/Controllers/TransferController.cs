using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using VendTech.Attributes;
using VendTech.BLL.Common;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;

namespace VendTech.Controllers
{
    public class TransferController : AppUserBaseController
    {
        #region Variable Declaration
        private readonly ITransferManager _transferManager;
        private readonly IDepositManager _depositManager;
        private readonly IPOSManager _posManager;
        private readonly IEmailTemplateManager _templateManager;
        private readonly ISMSManager _smsManager;
        #endregion

        public TransferController(IErrorLogManager errorLogManager, ITransferManager transferManager, IDepositManager depositManager, IPOSManager posManager, IEmailTemplateManager templateManager, ISMSManager smsManager)
            : base(errorLogManager)
        {
            _transferManager = transferManager;
            _depositManager = depositManager;
            _posManager = posManager;
            _templateManager = templateManager;
            _smsManager = smsManager;
        }

        #region User Management

        public ActionResult Index()
        {
            ViewBag.SelectedTab = SelectedAdminTab.Transfer;
            if(LOGGEDIN_USER == null)
            {
                SignOut();
                return View("Index", "Home");
            }
            var agencyPos = _posManager.ReturnAgencyAdminPOS(LOGGEDIN_USER.UserID);
            if(ModulesModel.Any())
            {
                return View(new TransferViewModel 
                {
                    CanTranferToOwnVendors = ModulesModel.Any(s => s.ControllerName.Contains("32")),
                    CanTranferToOtherVendors = ModulesModel.Any(s => s.ControllerName.Contains("33")),
                    Vendor = LOGGEDIN_USER?.AgencyName,
                    AdminBalance = Utilities.FormatAmount(agencyPos.Balance),
                    AdminName = LOGGEDIN_USER?.AgencyName, //+ " - " + agencyPos.SerialNumber, //agencyPos.User.Name + " " + agencyPos.User.SurName,
                    AdminPos = agencyPos.SerialNumber,
                    AdminPosId = agencyPos.POSId
                });
            }

            return View(new TransferViewModel());

        }

        [AjaxOnly, HttpPost]
        public JsonResult GetAllAgencyAdminVendors(FetchItemsModel request)
        {
            var vendorList = _transferManager
                .GetAllAgencyAdminVendors(
                PagingModel.DefaultModel("User.Agency.AgencyName", "Desc"), 
                LOGGEDIN_USER.AgencyId, LOGGEDIN_USER.UserID);
            return Json(new { result = JsonConvert.SerializeObject(vendorList.List) });
        }

        [AjaxOnly, HttpPost]
        public JsonResult GetAllOtherVendors(FetchItemsModel request)
        {
            var vendorList = _transferManager.GetOtherVendors(PagingModel.DefaultModel("User.Agency.AgencyName", "Desc"), LOGGEDIN_USER.AgencyId);
            return Json(new { result = JsonConvert.SerializeObject(vendorList.List) });
        }


        [AjaxOnly, HttpPost]
        public async Task<JsonResult> TransferCash(CashTransferModel request)
        {
            try
            {
                if (LOGGEDIN_USER.UserID == 0 || LOGGEDIN_USER == null)
                {
                    SignOut();
                }
                var reference = Utilities.GenerateByAnyLength(6).ToUpper();
                var frompos = _posManager.ReturnAgencyAdminPOS(LOGGEDIN_USER.UserID);
                var depositDr = new DepositDTOV2
                {
                    Amount = decimal.Negate(request.Amount),
                    POSId = frompos.POSId,
                    BankAccountId = 1,
                    CheckNumberOrSlipId = reference,
                    PaymentType = (int)DepositPaymentTypeEnum.AdminTransferOut,
                    ChequeBankName = "OWN ACC TRANSFER - (AGENCY TRANSFER)",
                    IsAudit = true
                };

                var debitDeposit = await _depositManager.CreateDepositDebitTransfer(depositDr, LOGGEDIN_USER.UserID, request.otp, request.ToPosId, frompos.POSId);

                
                if(debitDeposit != null)
                {
                    var depositCr = new DepositDTOV2
                    {
                        Amount = request.Amount,
                        POSId = request.ToPosId,
                        BankAccountId = 1,
                        CheckNumberOrSlipId = reference,
                        ChequeBankName = "OWN ACC TRANSFER - (AGENCY TRANSFER)",
                        PaymentType = (int)DepositPaymentTypeEnum.VendorFloatIn,
                        FirstDepositTransactionId = debitDeposit.TransactionId,
                        IsAudit = true
                    };
                    var creditDeposit = await _depositManager.CreateDepositCreditTransfer(depositCr, LOGGEDIN_USER.UserID, frompos);

                    if (creditDeposit.Status == ActionStatus.Successfull)
                    {
                        SendEmailOnDeposit(frompos.POSId, request.ToPosId);
                        await SendSmsOnDeposit(frompos.POSId, request.ToPosId, request.Amount);
                    }
                    return JsonResult(new ActionOutput { Message = creditDeposit.Message, Status = creditDeposit.Status });
                }
            }
            catch(ArgumentException ex)
            {
                return JsonResult(new ActionOutput { Message = ex?.Message, Status = ActionStatus.Error });
            }
            catch (Exception ex)
            {
                return JsonResult(new ActionOutput { Message = $"Error Occurred!!! please contact administrator: {ex?.Message}", Status = ActionStatus.Error });
            }
            return JsonResult(new ActionOutput { Message = $"Error Occurred!!! please contact administrator", Status = ActionStatus.Error });
        }


        private void SendEmailOnDeposit(long fromPos, long toPosId)
        {
            var frmPos = _posManager.GetSinglePos(fromPos);
            var toPos = _posManager.GetSinglePos(toPosId);

            if (frmPos != null & frmPos?.EmailNotificationDeposit ?? false)
            {
                var user = _userManager.GetUserDetailsByUserId(frmPos?.VendorId ?? 0);
                if (user != null)
                {
                    var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.TransferFromNotification);
                    if (emailTemplate.TemplateStatus)
                    {
                        string body = emailTemplate.TemplateContent;
                        body = body.Replace("%USER%", user.FirstName);
                        Utilities.SendEmail(user.Email, emailTemplate.EmailSubject, body);
                    }
                }
            }

            if (toPos != null & toPos?.EmailNotificationDeposit ?? false)
            {
                var user = _userManager.GetUserDetailsByUserId(toPos?.VendorId ?? 0);
                if (user != null)
                {
                    var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.TransferToNotification);
                    if (emailTemplate.TemplateStatus)
                    {
                        string body = emailTemplate.TemplateContent;
                        body = body.Replace("%USER%", user.FirstName);
                        Utilities.SendEmail(user.Email, emailTemplate.EmailSubject, body);
                    }
                }
            }
        }
        private async Task SendSmsOnDeposit(long fromPos, long toPosId, decimal amt)
        {

            var frmPos = _posManager.GetSinglePos(fromPos);
            var toPos = _posManager.GetSinglePos(toPosId);

            if (frmPos != null & frmPos.SMSNotificationDeposit ?? true)
            {
                var requestmsg = new SendSMSRequest
                {
                    Recipient = "232" + frmPos.Phone,
                    Payload = $"Greetings {frmPos.User.Name} \n" +
                   $"Your wallet has been credited of NLe: {Utilities.FormatAmount(amt)}.\n" +
                   "Please confirm the amount transferred reflects in your wallet.\n" +
                   "VENDTECH"
                };

                await Task.Run(() => Utilities.SendSms(requestmsg));
            }

            if (toPos != null & toPos.SMSNotificationDeposit ?? true)
            {
                var requestmsg = new SendSMSRequest
                {
                    Recipient = "232" + toPos.Phone,
                    Payload = $"Greetings {toPos.User.Name} \n" +
                   $"Your wallet has been debited of NLe: {Utilities.FormatAmount(amt)}.\n" +
                   "VENDTECH"
                };

                Utilities.SendSms(requestmsg);
            }
        }


        [AjaxOnly, HttpPost]
        public async Task<JsonResult> SendOTP()
        {
            if (LOGGEDIN_USER.UserID == 0 || LOGGEDIN_USER == null)
            {
                SignOut();
                return JsonResult(new ActionOutput { Message = "Session Expired!", Status = ActionStatus.Unauthorized });
            }
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            var result = _depositManager.SendOTP();
            if (result.Status == ActionStatus.Successfull)
            {

                var user = _userManager.GetAppUserProfile(LOGGEDIN_USER.UserID);
                var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.DepositOTP);
                if (emailTemplate.TemplateStatus)
                {
                    string body = emailTemplate.TemplateContent;
                    body = body.Replace("%otp%", result.Object);
                    body = body.Replace("%USER%", LOGGEDIN_USER.FirstName);
                    var currentUser = LOGGEDIN_USER.UserID;
                    Utilities.SendEmail(user.Email, emailTemplate.EmailSubject, body);
                }
                if(user != null)
                {
                    var requestmsg = new SendSMSRequest
                    {
                        Recipient = "232" + user.Phone,
                        Payload = $"Greetings {user.Name} \n" +
                                  $"To Approve deposits, please use the following OTP (One Time Passcode). {result.Object}\n" +
                                  "VENDTECH"
                    };
                    await Task.Run(() => Utilities.SendSms(requestmsg));
                }
                
            }
            return JsonResult(new ActionOutput { Message = result.Message, Status = result.Status });
        }
        #endregion

    }


}
