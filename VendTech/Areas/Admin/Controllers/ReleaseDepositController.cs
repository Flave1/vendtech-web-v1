using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using VendTech.Attributes;
using VendTech.BLL.Common;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.DAL;

namespace VendTech.Areas.Admin.Controllers
{
    public class ReleaseDepositController : AdminBaseV2Controller
    {
        #region Variable Declaration
        private new readonly IUserManager _userManager;
        private readonly IEmailTemplateManager _templateManager;
        private readonly IDepositManager _depositManager;
        private readonly EmailNotification emailNotification;
        private readonly MobileNotification mobileNotification;
        #endregion

        public ReleaseDepositController(IUserManager userManager, IErrorLogManager errorLogManager, IEmailTemplateManager templateManager, IDepositManager depositManager, EmailNotification emailNotification, MobileNotification mobileNotification)
            : base(errorLogManager)
        {
            _userManager = userManager;
            _templateManager = templateManager;
            _depositManager = depositManager;
            this.emailNotification = emailNotification;
            this.mobileNotification = mobileNotification;
        }

        #region User Management

        [HttpGet, Public]
        public ActionResult ManageDepositRelease(string status = "", string depositids = "", string otp = "", string taskId = "")
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    var user = _userManager.BackgroundAdminLogin(Convert.ToInt64(taskId));
                    if (user == null)
                        SignOut();
                    else
                    {
                        JustLoggedin = true;
                        var PermissonAndDetailModel = new PermissonAndDetailModelForAdmin();
                        PermissonAndDetailModel.UserDetails = new UserDetailForAdmin(user);
                        PermissonAndDetailModel.ModulesModelList = _userManager.GetAllModulesAtAuthentication(user.UserID)
                            .Where(e => e.ControllerName != "19" && e.ControllerName != "20" && e.ControllerName != "1" && e.ControllerName != "23" && e.ControllerName != "18" && e.ControllerName != "27").ToList();
                        CreateCustomAuthorisationCookie(user.UserName, false, new JavaScriptSerializer().Serialize(PermissonAndDetailModel));

                        ViewBag.LOGGEDIN_USER = user;
                        ViewBag.Data = _userManager.GetNotificationUsersCount(user.UserID);
                        ViewBag.USER_PERMISSONS = PermissonAndDetailModel.ModulesModelList;
                    }
                }
                ViewBag.SelectedTab = SelectedAdminTab.Deposits;
                ViewBag.Balance = _depositManager.GetPendingDepositTotal();
                var deposits = _depositManager.GetAllPendingDepositPagedList(PagingModel.DefaultModel("CreatedAt", "Desc"), true, 0, status);
                return View("ManageDepositReleaseV2", deposits);
            }
            catch (Exception)
            {
                SignOut();
                return View("ManageDepositReleaseV2", new PagingResult<DepositListingModel>());
            }
        }

        [AjaxOnly, HttpPost]
        public JsonResult GetDepositReleasePagingList(PagingModel model)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            var modal = _depositManager.GetDepositPagedList(model, true);
            List<string> resultString = new List<string>();
            resultString.Add(RenderRazorViewToString("Partials/_depositReleaseListing", modal));
            resultString.Add(modal.TotalCount.ToString());
            return JsonResult(resultString);
        }
        [AjaxOnly, HttpPost]
        public async Task<JsonResult> ApproveReleaseDeposit(long depositId)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            var result = await _depositManager.ChangeDepositStatus(depositId, DepositPaymentStatusEnum.Released, false);
            return JsonResult(result);
        }
        [AjaxOnly, HttpPost]
        public async Task<JsonResult> RejectReleaseDeposit(long depositId)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            return JsonResult(await _depositManager.ChangeDepositStatus(depositId, DepositPaymentStatusEnum.Rejected, false));
        }
        [AjaxOnly, HttpPost]
        public JsonResult SendOTP()
        { 
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            var result = _depositManager.SendOTP();
            if (result.Status == ActionStatus.Successfull)
            {
                var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.DepositOTP);
                if (emailTemplate.TemplateStatus)
                {
                    string body = emailTemplate.TemplateContent;
                    body = body.Replace("%otp%", result.Object);
                    body = body.Replace("%USER%", LOGGEDIN_USER.FirstName);
                    var currentUser = LOGGEDIN_USER.UserID;
                    Utilities.SendEmail(User.Identity.Name, emailTemplate.EmailSubject, body);
                    //Utilities.SendEmail(admin.Email, emailTemplate.EmailSubject, body);
                    Utilities.SendEmail("vblell@gmail.com", emailTemplate.EmailSubject, body);
                }
            }
            return JsonResult(new ActionOutput { Message = result.Message, Status = result.Status });
        }


        [AjaxOnly, HttpPost]
        public JsonResult SendOTP2(ReleaseDepositModel2 model)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            var result = _depositManager.SendOTP();
            if (result.Status == ActionStatus.Successfull)
            {
                var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.DepositOTP);
                if (emailTemplate.TemplateStatus)
                {
                    var releaseBtn = $"<a href='{Utilities.DomainUrl}/Admin/ReleaseDeposit/ManageDepositRelease?depositids={string.Join(",", model.ReleaseDepositIds)}&otp={result.Object}&taskId={LOGGEDIN_USER.UserID}'>Release deposit</a>";
                    string body = emailTemplate.TemplateContent;
                    body = body.Replace("%otp%", result.Object);
                    body = body.Replace("%RELEASEOTP%", releaseBtn);
                    body = body.Replace("%USER%", LOGGEDIN_USER.FirstName);
                    var currentUser = LOGGEDIN_USER.UserID; 
                    Utilities.SendEmail(User.Identity.Name, emailTemplate.EmailSubject, body);
                    //Utilities.SendEmail("vblell@gmail.com", emailTemplate.EmailSubject, body);
                }
            }
            return JsonResult(new ActionOutput { Message = result.Message, Status = result.Status });
        }
        [AjaxOnly, HttpPost]
        public async Task<JsonResult> ChangeDepositStatus(ReleaseDepositModel model)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            var result = await _depositManager.ChangeMultipleDepositStatus(model, LOGGEDIN_USER.UserID);

            if (result.Status == ActionStatus.Error)
            {
                return JsonResult(new ActionOutput { Message = result.Message, Status = result.Status });
            }

            if (model.ReleaseDepositIds != null && model.ReleaseDepositIds.Any())
            {
                for (int i = 0; i < model.ReleaseDepositIds.Count; i++)
                {
                    var deposit = _depositManager.GetDeposit(model.ReleaseDepositIds[i]);
                    emailNotification.SendEmailToUserOnDepositApproval(deposit);
                    emailNotification.SendSmsToUserOnDepositApproval(deposit);

                    await _depositManager.DeletePendingDeposits(deposit);

                    mobileNotification.PushNotificationToMobile(deposit.ApprovedDepId);
                    PushNotification.Instance
                       .IncludeAdminNotificationCount()
                       .IncludeUserBalanceOnTheWeb(deposit.UserId)
                       .IncludeAdminWidgetDeposits()
                       .IncludeAdminUnreleasedDeposits()
                       .Send();
                }
            }
            return JsonResult(new ActionOutput { Message = result.Message, Status = result.Status });
        }

        [AjaxOnly, HttpPost]
        public JsonResult CancelDeposit(CancelDepositModel model)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            var deposit = _depositManager.GetDeposit(model.CancelDepositId);
            var result = _depositManager.CancelDeposit(model);
            if (result.Status == ActionStatus.Error)
            {
                return JsonResult(new ActionOutput { Message = result.Message, Status = result.Status });
            }

            mobileNotification.Notify(model.CancelDepositId, deposit.UserId, "Last Deposit Cancelled");
            PushNotification.Instance
                .IncludeAdminNotificationCount()
                .IncludeAdminUnreleasedDeposits()
                .IncludeUserBalanceOnTheWeb(deposit.UserId)
                .Send();

            return JsonResult(new ActionOutput { Message = result.Message, Status = result.Status });
        }


        [AjaxOnly, HttpPost]
        public async Task<JsonResult> AutoRelease(ReleaseDepositModel2 model)
        {
            try
            {
                var pds = _depositManager.GetPendingDeposits(model.ReleaseDepositIds);
                for (int i = 0; i < pds.Count; i++)
                {
                    ActionOutput result = await _depositManager.ChangeDepositStatus(pds[i].PendingDepositId, DepositPaymentStatusEnum.Released, true);

                    var deposit = _depositManager.GetDeposit(pds[i].PendingDepositId);
                    emailNotification.SendEmailToUserOnDepositApproval(deposit);
                    emailNotification.SendEmailToAdminOnDepositAutoApproval(deposit, result.ID);
                    emailNotification.SendSmsToUserOnDepositApproval(deposit);

                    await _depositManager.DeletePendingDeposits(deposit);

                    mobileNotification.PushNotificationToMobile(deposit.ApprovedDepId);
                    PushNotification.Instance
                       .IncludeAdminNotificationCount()
                       .IncludeUserBalanceOnTheWeb(deposit.UserId)
                       .IncludeAdminWidgetDeposits()
                       .IncludeAdminUnreleasedDeposits()
                       .Send();

                }

                return JsonResult(new ActionOutput { Message = $"{model.ReleaseDepositIds.Count} DEPOSIT (S) RELEASED AUTOMATICALLY", Status = ActionStatus.Successfull });
            }
            catch (ArgumentException ex)
            {
                return JsonResult(new ActionOutput { Message = ex.Message, Status = ActionStatus.Error });
            }
            catch (Exception ex)
            {
                return JsonResult(new ActionOutput { Message = ex.Message, Status = ActionStatus.Error });
            }
        }

   

        #endregion
    }
}