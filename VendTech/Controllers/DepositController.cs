#region Default Namespaces
using System;
using System.Linq;
using System.Web.Mvc;
#endregion

#region Custom Namespaces
using VendTech.Attributes;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.BLL.Common;
using VendTech.DAL;
using System.Threading.Tasks;
#endregion

namespace VendTech.Controllers
{
    /// <summary>
    /// Home Controller 
    /// Created On: 10/04/2015
    /// </summary>
    public class DepositController : AppUserBaseController
    {
        #region Variable Declaration
        private new readonly IUserManager _userManager;
        private readonly IAuthenticateManager _authenticateManager;
        private readonly IVendorManager _vendorManager;
        private readonly ICMSManager _cmsManager;
        private readonly IDepositManager _depositManager;
        private readonly IMeterManager _meterManager;
        private readonly IBankAccountManager _bankAccountManager;
        private readonly IPOSManager _posManager;
        private readonly IEmailTemplateManager _templateManager;
        private readonly IPaymentTypeManager _paymentTypeManager;
        private readonly EmailNotification emailNotification;
        private readonly MobileNotification mobileNotification;


        #endregion

        public DepositController(IUserManager userManager,
            IErrorLogManager errorLogManager,
            IAuthenticateManager authenticateManager,
            ICMSManager cmsManager, IDepositManager depositManager,
            IMeterManager meterManager, IVendorManager vendorManager,
            IBankAccountManager bankAccountManager, IPOSManager posManager,
            IEmailTemplateManager templateManager, IPaymentTypeManager paymentTypeManager, EmailNotification emailNotification, MobileNotification mobileNotification)
            : base(errorLogManager)
        {
            _userManager = userManager;
            _authenticateManager = authenticateManager;
            _cmsManager = cmsManager;
            _depositManager = depositManager;
            _meterManager = meterManager;
            _vendorManager = vendorManager;
            _bankAccountManager = bankAccountManager;
            _posManager = posManager;
            _templateManager = templateManager;
            _paymentTypeManager = paymentTypeManager;
            this.emailNotification = emailNotification;
            this.mobileNotification = mobileNotification;
        }

        /// <summary>
        /// Index View 
        /// </summary>
        /// <returns></returns>
        public ActionResult Index(string posId = "")
        {
            try
            {
                ViewBag.SelectedTab = SelectedAdminTab.Deposits;
                var model = new DepositModel();
                ViewBag.UserId = LOGGEDIN_USER.UserID;
                ViewBag.IsPlatformAssigned = _platformManager.GetUserAssignedPlatforms(LOGGEDIN_USER.UserID).Count > 0;
                ViewBag.DepositTypes = _paymentTypeManager.GetPaymentTypeSelectList();

                var history_model = new ReportSearchModel
                {
                    SortBy = "CreatedAt",
                    SortOrder = "Desc",
                    VendorId = LOGGEDIN_USER.UserID
                };

                var deposits = new PagingResult<DepositListingModel>();
                deposits = _depositManager.GetReportsPagedHistoryList(history_model, false, LOGGEDIN_USER.AgencyId);
                var depositsPendLIst = _depositManager.GetPendingDepositForCustomer(LOGGEDIN_USER.UserID, LOGGEDIN_USER.AgencyId);
                ViewBag.WalletHistory = depositsPendLIst.Concat(deposits.List).ToList();

                ViewBag.ChkBankName = new SelectList(_bankAccountManager.GetBankNames_API().ToList(), "BankName", "BankName");
                var posList = _posManager.GetPOSWithNameSelectList(LOGGEDIN_USER.UserID, LOGGEDIN_USER.AgencyId, true).OrderBy(f => f.Value).ToList();
                ViewBag.userPos = posList;
                if (string.IsNullOrEmpty(posId) && posList.Count > 0)
                {
                    //posId = Convert.ToInt64(posList[0].Value);
                    ViewBag.posId = posList.FirstOrDefault(d => d.Text.Contains("AGT-")).Value;
                    ViewBag.Percentage = _posManager.GetPosCommissionPercentage(long.Parse(ViewBag.posId));
                    ViewBag.balance = _posManager.GetPosBalance(long.Parse(ViewBag.posId));
                }
                else
                {
                    ViewBag.posId = posId;
                    ViewBag.Percentage = _posManager.GetPosCommissionPercentage(long.Parse(posId));
                    ViewBag.balance = _posManager.GetPosBalance(long.Parse(posId));
                }

                var bankAccounts = _bankAccountManager.GetBankAccounts();

                ViewBag.bankAccounts = bankAccounts.ToList().Select(p => new SelectListItem { Text = "(" + p.BankName + " - " + Utilities.FormatBankAccount(p.AccountNumber) + ")", Value = p.BankAccountId.ToString() }).ToList();

                ViewBag.walletBalance = _userManager.GetUserWalletBalance(LOGGEDIN_USER.UserID, LOGGEDIN_USER.AgencyId);

                return View(model);
            }
            catch (NullReferenceException)
            {
                SignOut();
                return View();
            }
        }
    
        [AjaxOnly, HttpPost]
        public async Task<JsonResult> AddDeposit(DepositModel model)
        {
            ActionOutput<PendingDeposit> pd = null;
            if (model.PosId == 0)
            {
                return JsonResult(new ActionOutput { Message = "POS Required", Status = ActionStatus.Error });
            }

            if (model.ContinueDepoit == 0)
            {
                var pendingDeposits = _depositManager.ReturnPendingDepositsTotalAmount(model);
                if (pendingDeposits > 0)
                {
                    return JsonResult(new ActionOutput { Message = Utilities.FormatAmount(pendingDeposits), Status = ActionStatus.Successfull });
                }
            }
            pd = _depositManager.SaveDepositRequest(model);


            string mesg = pd.Message;
            if (pd.Object.User.AutoApprove.Value)
            {
                ActionOutput result = await _depositManager.ChangeDepositStatus(pd.Object.PendingDepositId, DepositPaymentStatusEnum.Released, true);

                var deposit = _depositManager.GetDeposit(pd.Object.PendingDepositId);
                emailNotification.SendEmailToUserOnDepositApproval(deposit);
                emailNotification.SendEmailToAdminOnDepositAutoApproval(deposit, result.ID);
                emailNotification.SendSmsToUserOnDepositApproval(deposit);

                await _depositManager.DeletePendingDeposits(deposit);

                mobileNotification.PushNotificationToMobile(deposit.ApprovedDepId);
                PushNotification.Instance
                   .IncludeAdminNotificationCount()
                   .IncludeUserBalanceOnTheWeb(pd.Object.UserId)
                   .IncludeAdminWidgetDeposits()
                   .IncludeAdminUnreleasedDeposits()
                   .Send();

            }
            else
            {
                mobileNotification.Notify(pd.Object.PendingDepositId, pd.Object.UserId, "Deposit Requested");
                emailNotification.SendEmailToAdminOnDepositRequest(pd.Object);
                PushNotification.Instance
                   .IncludeAdminNotificationCount()
                   .IncludeUserBalanceOnTheWeb(pd.Object.UserId)
                   .IncludeAdminWidgetDeposits()
                   .IncludeAdminUnreleasedDeposits()
                   .Send();

             
            }

            
            return JsonResult(new ActionOutput { Message = mesg, Status = pd.Status });
        }

        [AjaxOnly]
        public JsonResult GetBankAccountDetail(int bankAccountId)
        {
            return Json(new { bankAccount = _bankAccountManager.GetBankAccountDetail(bankAccountId) }, JsonRequestBehavior.AllowGet);
        }

        [AjaxOnly, HttpPost, Public]
        public ActionResult GetDepositDetails(RequestObject tokenobject)
        {
            var result = _depositManager.GetDepositDetail(Convert.ToInt64(tokenobject.token_string));
            if (result.Object == null)
                return Json(new { Success = false, Code = 302, Msg = result.Message });
            return PartialView("_depositReceipt", result.Object);
        }
        [AjaxOnly, HttpPost, Public]
        public ActionResult GetPendingDepositDetails(RequestObject tokenobject)
        {
            var result = _depositManager.GetPendingDepositDetail(Convert.ToInt64(tokenobject.token_string));
            if (result.Object == null)
                return Json(new { Success = false, Code = 302, Msg = result.Message });
            return PartialView("_depositReceipt", result.Object);
        }

        string LogExceptionToDatabase(Exception exc)
        {
            var context = new VendtechEntities();
            ErrorLog errorObj = new ErrorLog();
            errorObj.Message = exc.Message;
            errorObj.StackTrace = exc.StackTrace;
            errorObj.InnerException = exc.InnerException == null ? "" : exc.InnerException.Message;
            errorObj.LoggedInDetails = "";
            errorObj.LoggedAt = DateTime.UtcNow;
            errorObj.UserId = 0;
            context.ErrorLogs.Add(errorObj);
            // To do
            context.SaveChanges();
            return errorObj.ErrorLogID.ToString();
        }
    }
}