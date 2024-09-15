using System.Collections.Generic;
using System.Reflection;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using VendTech.Attributes;
using VendTech.BLL.Common;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Managers;
using VendTech.BLL.Models;

namespace VendTech.Areas.Admin.Controllers
{
    public class UserController : AdminBaseV2Controller
    {
        #region Variable Declaration
        private new readonly IUserManager _userManager;
        private readonly IEmailTemplateManager _templateManager;
        private readonly IAuthenticateManager _authenticateManager;
        #endregion

        public UserController(IUserManager userManager, IErrorLogManager errorLogManager, IEmailTemplateManager templateManager, IAuthenticateManager authenticateManager)
            : base(errorLogManager)
        {
            _userManager = userManager;
            _templateManager = templateManager;
            _authenticateManager = authenticateManager;
        }

        #region User Management

        [HttpGet]
        public ActionResult ManageUsers()
        {
            ViewBag.SelectedTab = SelectedAdminTab.Users;
            var users = _userManager.GetUserPagedList(PagingModel.DefaultModel("CreatedAt", "Desc"));
            return View(users);
        }

        [AjaxOnly, HttpPost]
        public JsonResult GetUsersPagingList(PagingModel model)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Users;
            var modal = _userManager.GetUserPagedList(model);
            List<string> resultString = new List<string>();
            resultString.Add(RenderRazorViewToString("Partials/_userListing", modal));
            resultString.Add(modal.TotalCount.ToString());
            return JsonResult(resultString);
        }

        public ActionResult AddUser()
        {

            var countries = _authenticateManager.GetCountries();
            var countryDrpData = new List<SelectListItem>();
            foreach (var item in countries)
            {
                countryDrpData.Add(new SelectListItem { Text = item.Name, Value = item.CountryId.ToString() });
            }
            ViewBag.countries = countryDrpData;
            ViewBag.Cities = _authenticateManager.GetCities();

            ViewBag.UserTypes = _userManager.GetUserRolesSelectList();
            ViewBag.SelectedTab = SelectedAdminTab.Users;
            var model = new AddUserModel();
            model.ModuleList = _userManager.GetAllModules(0);
            model.WidgetList = _userManager.GetAllWidgets(0);
            return View(model);
        }

        [AjaxOnly, HttpPost]
        public JsonResult AddUserDetails(AddUserModel model)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Users;
            if (model.ImagefromWeb != null)
            {
                var file = model.ImagefromWeb;
                var constructorInfo = typeof(HttpPostedFile).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
                model.Image = (HttpPostedFile)constructorInfo
                           .Invoke(new object[] { file.FileName, file.ContentType, file.InputStream });
            }
            var result = _userManager.AddUserDetails(model);
            if (result.Status == ActionStatus.Successfull)
            {
                var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.NewCMSUser);
                string body = emailTemplate.TemplateContent;
                body = body.Replace("%UserName%", model.Email);
                body = body.Replace("%Password%", model.Password);
                body = body.Replace("%WebLink%", WebConfigurationManager.AppSettings["BaseUrl"].ToString() + "Admin");
                Utilities.SendEmail(model.Email, emailTemplate.EmailSubject, body);
            }
            return JsonResult(result);
        }

        public ActionResult EditUser(long userId)
        {
            ViewBag.UserTypes = _userManager.GetUserRolesSelectList();
            ViewBag.SelectedTab = SelectedAdminTab.Users;
            var userModel = new AddUserModel();

            var countries = _authenticateManager.GetCountries();
            var countryDrpData = new List<SelectListItem>();
            foreach (var item in countries)
            {
                var selected = userModel.CountryId == item.CountryId;
                countryDrpData.Add(new SelectListItem { Text = item.Name, Value = item.CountryId.ToString(), Selected = selected });
            }

            ViewBag.countries = countryDrpData;

            var cities = _authenticateManager.GetCities();
            var cityDrpData = new List<SelectListItem>();
            foreach (var item in cities)
            {
                var selected = userModel.City == item.CityId;
                cityDrpData.Add(new SelectListItem { Text = item.Name, Value = item.CityId.ToString(), Selected = selected });
            }
            ViewBag.Cities = cityDrpData;

            userModel = _userManager.GetAppUserDetailsByUserId(userId);
            userModel.ModuleList = _userManager.GetAllModules(userId);
            userModel.PlatformList = _userManager.GetAllPlatforms(userId);
            userModel.WidgetList = _userManager.GetAllWidgets(userId);
            return View(userModel);
        }

        [AjaxOnly, HttpPost]
        public JsonResult UpdateUserDetails(AddUserModel model)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Users;
            if (model.ImagefromWeb != null)
            {
                var file = model.ImagefromWeb;
                var constructorInfo = typeof(HttpPostedFile).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
                model.Image = (HttpPostedFile)constructorInfo
                           .Invoke(new object[] { file.FileName, file.ContentType, file.InputStream });
            }
            return JsonResult(_userManager.UpdateUserDetails(model));
        }

        [AjaxOnly, HttpPost]
        public JsonResult DeleteUser(long userId)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Users;
            return JsonResult(_userManager.DeleteUser(userId));
        }
        [AjaxOnly, HttpPost]
        public JsonResult DeclineUser(long userId)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Users;
            return JsonResult(_userManager.DeclineUser(userId));
        }
        [AjaxOnly, HttpPost]
        public JsonResult BlockUser(long userId)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Users;
            return JsonResult(_userManager.ChangeUserStatus(userId, UserStatusEnum.Block));
        }
        [AjaxOnly, HttpPost]
        public JsonResult UnBlockUser(long userId)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Users;
            return JsonResult(_userManager.ChangeUserStatus(userId, UserStatusEnum.Active));
        }

        [AjaxOnly, HttpPost]
        public JsonResult GetVendorName(int posId)
        {
            return Json(_userManager.GetVendorNamePOSNumber(posId));
        }
        #endregion
    }
}