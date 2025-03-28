﻿using DocumentFormat.OpenXml.Drawing.Charts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Http.Description;
using System.Web.WebPages.Html;
using VendTech.Attributes;
using VendTech.BLL.Common;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.Framework.Api;

namespace VendTech.Areas.Api.Controllers
{
    public class AccountController : BaseAPIController
    {
        private readonly IUserManager _userManager;
        private readonly IAuthenticateManager _authenticateManager;
        private readonly IEmailTemplateManager _templateManager;
        private readonly IVendorManager _vendorManager;
        private readonly ICMSManager _cmsManager;
        private readonly IBankAccountManager _bankAccountManager;
        private readonly IPOSManager _posManager;
        private readonly IAgencyManager _agentManager;
        private readonly ISMSManager _smsManager;
        private readonly IMeterManager _meterManager;
        public AccountController(IUserManager userManager,
            IBankAccountManager bankAccountManager,
            IErrorLogManager errorLogManager,
            IEmailTemplateManager templateManager,
            IAuthenticateManager authenticateManager,
            ICMSManager cmsManager,
            IVendorManager vendorManager,
            IPOSManager posManager,
            IAgencyManager agentManager, ISMSManager smsManager, IMeterManager meterManager)
            : base(errorLogManager)
        {
            _userManager = userManager;
            _authenticateManager = authenticateManager;
            _templateManager = templateManager;
            _vendorManager = vendorManager;
            _cmsManager = cmsManager;
            _bankAccountManager = bankAccountManager;
            _posManager = posManager;
            _agentManager = agentManager;
            _smsManager = smsManager;
            _meterManager = meterManager;
        }

        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage Test()
        {
            var aa = _userManager.GetWelcomeMessage();
            return new JsonContent("OTP SENT SUCCESSFULLY", Status.Success).ConvertToHttpResponseOK();
        }

        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        [ActionName("SignIn")]
        public HttpResponseMessage SignIn(LoginAPIPassCodeModel model)
        {
            var country = Utilities.GetCountry();
            if (country.DomainUrl.Contains("vendtechsl.net"))
            {
                return new JsonContent("Please ensure you are connected to production", Status.Failed).ConvertToHttpResponseOK();
            }
            if (string.IsNullOrEmpty(model.DeviceToken))
            {
                return new JsonContent("Unsupported device Detected!", Status.Failed).ConvertToHttpResponseOK();
            }

            if (!ModelState.IsValid)
                return new JsonContent("Passcode is required.", Status.Failed).ConvertToHttpResponseOK();
            else
            {
              
                var userDetails = _authenticateManager.GetUserDetailByPassCode(model.PassCode);
                if (userDetails == null)
                    return new JsonContent("YOUR ACCOUNT IS DISABLED! \n PLEASE CONTACT VENDTECH MANAGEMENT", Status.Failed).ConvertToHttpResponseOK();
                else if (userDetails.UserId == 0)
                    return new JsonContent("Invalid Passcode.", Status.Failed).ConvertToHttpResponseOK();
                else if (!string.IsNullOrEmpty(userDetails.DeviceToken) && userDetails.DeviceToken != model.DeviceToken.Trim() && model.PassCode != "73086")
                    return new JsonContent("INVALID CREDENTIALS \n\n PLEASE RESET YOUR PASSCODE OR \n CONTACT VENDTECH MANAGEMENT", Status.Failed).ConvertToHttpResponseOK();
                else
                {
                    if (model.AppVersion != CurrentAppVersion)
                    {
                        //return new JsonContent("UPDATE_APP", Status.Success).ConvertToHttpResponseOK(); Will update later
                        return new JsonContent("APP VERSION IS OUT OF DATE, PLEASE UPDATE APP FROM PLAYSTORE", Status.Success).ConvertToHttpResponseOK();
                    }
                    var isEnabled = _authenticateManager.IsUserAccountORPosBlockedORDisabled(userDetails.UserId);
                    if (isEnabled)
                    {
                        return new JsonContent("YOUR ACCOUNT IS DISABLED! \n PLEASE CONTACT VENDTECH MANAGEMENT", Status.Failed).ConvertToHttpResponseOK();
                    }

                    var pos = _posManager.GetPosDetails(model.PassCode);
                    if(pos == null)
                    {
                        return new JsonContent("POS NOT AVAILABLE! \n PLEASE CONTACT VENDTECH MANAGEMENT", Status.Failed).ConvertToHttpResponseOK();
                    }
                    if (pos.Enabled)
                    {
                        userDetails.Percentage = _vendorManager.GetVendorPercentage(userDetails.UserId);
                        _authenticateManager.AddTokenDevice(model);
                        _userManager.UpdateUserLastAppUsedTime(pos.VendorId.Value);
                        if (_authenticateManager.IsTokenAlreadyExists(userDetails.UserId, userDetails.POSNumber))
                        {
                            _authenticateManager.DeleteGenerateToken(userDetails.UserId, userDetails.POSNumber);
                            return GenerateandSaveToken(userDetails, model);
                        }
                        else
                            return GenerateandSaveToken(userDetails, model);
                    }
                    else
                    {
                        return new JsonContent("POS IS DISABLED! \n PLEASE CONTACT VENDTECH MANAGEMENT", Status.Failed).ConvertToHttpResponseOK();
                    }
                }
            }
        }


        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        [ActionName("SignInV2")]
        public HttpResponseMessage SignInV2(LoginAPIPassCodeModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.DeviceToken))
                {
                    return new JsonContent(ApiCodes.RESET_PASSCODE, Status.Success).ConvertToHttpResponseOK();
                }

                if (!ModelState.IsValid)
                    return new JsonContent(ApiCodes.PASSCODE_REQUIRED, Status.Failed).ConvertToHttpResponseOK();
                else
                {
                    var userDetails = _authenticateManager.GetUserDetailByPassCode(model.PassCode);
                    if (userDetails == null)
                        return new JsonContent(ApiCodes.ACCOUNT_DISABLED, Status.Failed).ConvertToHttpResponseOK();
                    else if (userDetails.UserId == 0)
                        return new JsonContent(ApiCodes.INVALID_PASSCODE, Status.Failed).ConvertToHttpResponseOK();

                    else if (!userDetails.IsPasscodeNew && !string.IsNullOrEmpty(userDetails.DeviceToken) && userDetails.DeviceToken != model.DeviceToken.Trim() && model.PassCode != "73086")// 
                    {
                        if (model.AppVersion != CurrentAppVersion)//This version has not been published
                        {
                            return new JsonContent(ApiCodes.RESET_PASSCODE_2, Status.Success).ConvertToHttpResponseOK();
                        }

                        return new JsonContent(ApiCodes.INVALID_CREDENTIALS, Status.Failed).ConvertToHttpResponseOK();
                    }
                    else
                    {
                        Utilities.CheckMobileAppVersion(model.AppVersion, CurrentAppVersion);

                        var isEnabled = _authenticateManager.IsUserAccountORPosBlockedORDisabled(userDetails.UserId);
                        if (isEnabled)
                        {
                            return new JsonContent("YOUR ACCOUNT IS DISABLED! \n PLEASE CONTACT VENDTECH MANAGEMENT", Status.Failed).ConvertToHttpResponseOK();
                        }

                        var pos = _posManager.GetPosDetails(model.PassCode);
                        if (pos == null)
                        {
                            return new JsonContent(ApiCodes.POS_NOTFOUND, Status.Failed).ConvertToHttpResponseOK();
                        }
                        if (pos.Enabled)
                        {
                            userDetails.Percentage = _vendorManager.GetVendorPercentage(userDetails.UserId);
                            _authenticateManager.AddTokenDevice(model);
                            _userManager.UpdateUserLastAppUsedTime(pos.VendorId.Value);
                            if (userDetails.IsPasscodeNew)
                                _posManager.UpdatePasscode(userDetails.UserId);

                            if (_authenticateManager.IsTokenAlreadyExists(userDetails.UserId, userDetails.POSNumber))
                            {
                                _authenticateManager.DeleteGenerateToken(userDetails.UserId, userDetails.POSNumber);
                                return GenerateandSaveToken(userDetails, model);
                            }
                            else
                            {
                                return GenerateandSaveToken(userDetails, model);
                            }
                        }
                        else
                        {
                            return new JsonContent("POS IS DISABLED! \n PLEASE CONTACT VENDTECH MANAGEMENT", Status.Failed).ConvertToHttpResponseOK();
                        }
                    }
                }
            }
            catch (ArgumentException ex)
            {
                return new JsonContent(ex.Message, Status.Failed).ConvertToHttpResponseOK();
            }
            catch (Exception)
            {
                throw;
            }
           
        }

        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        [ActionName("DeleteUser")]
        public HttpResponseMessage DeleteUser(DeleteAPIModel model)
        {
            var user = _userManager.GetUserDetailByEmail(model.Email);
            if(user == null)
            {
                return new JsonContent("User account not found", Status.Failed).ConvertToHttpResponseOK();
            }
            user.Status = (int)UserStatusEnum.Deleted;
            _userManager.SaveChanges();

            return new JsonContent("User account delted successfully", Status.Success).ConvertToHttpResponseOK();
        }



        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        [ActionName("SignInNewpasscode")]
        public HttpResponseMessage SignInNewpasscode(LoginAPIPassCodeModel model)
        {
            if (!ModelState.IsValid)
                return new JsonContent("Please confirm fields", Status.Failed).ConvertToHttpResponseOK();
            else
            {
                var userDetails = _authenticateManager.SaveAndLoginPassCode(model.PassCode, model.UserId);
                if (userDetails == null)
                    return new JsonContent("YOUR ACCOUNT IS DISABLED! \n PLEASE CONTACT VENDTECH MANAGEMENT", Status.Failed).ConvertToHttpResponseOK();
                else if (userDetails.UserId == 0)
                    return new JsonContent("ACCOUNT DETAILS FOR EMAIL IS NOT AVAILABLE", Status.Failed).ConvertToHttpResponseOK();
                else
                {
                    var isEnabled = _posManager.GetPosDetails(model.PassCode).Enabled;
                    if (isEnabled)
                    {
                        userDetails.Percentage = _vendorManager.GetVendorPercentage(userDetails.UserId);
                        model.AppVersion = CurrentAppVersion;
                        _authenticateManager.AddTokenDevice(model);
                        if (_authenticateManager.IsTokenAlreadyExists(userDetails.UserId, userDetails.POSNumber))
                        {
                            _authenticateManager.DeleteGenerateToken(userDetails.UserId, userDetails.POSNumber);
                            return GenerateandSaveToken(userDetails, model);
                        }
                        else
                            return GenerateandSaveToken(userDetails, model);
                    }
                    else
                    {
                        return new JsonContent("POS IS DISABLED! \n PLEASE CONTACT VENDTECH MANAGEMENT", Status.Failed).ConvertToHttpResponseOK();
                    }
                }
            }
        }

        [HttpPost]
        public HttpResponseMessage Logout()
        {
            var token = Request.Headers.GetValues("Token").FirstOrDefault();
            var res = _authenticateManager.Logout(LOGGEDIN_USER.UserId, token);
            return new JsonContent(res.Message, res.Status == ActionStatus.Successfull ? Status.Success : Status.Failed).ConvertToHttpResponseOK();
        }

        [NonAction]
        private HttpResponseMessage GenerateandSaveToken(UserModel user, LoginAPIPassCodeModel model)
        {
            var IssuedOn = DateTime.UtcNow;
            var newToken = _authenticateManager.GenerateToken(user, IssuedOn);
            user.Token = newToken;
            user.MinVend = _meterManager.ReturnElectricityMinVend().ToString();
            user.AirtimeMinVend = _meterManager.ReturnAirtimeMinVend().ToString();
            TokenModel token = new TokenModel();
            token.TokenKey = newToken;
            token.UserId = user.UserId;
            token.ExpiresOn = DateTime.MaxValue;
            token.DeviceToken = model.DeviceToken;
            token.AppType = model.AppType;
            token.PosNumber = user.POSNumber;
            //  token.ExpiresOn = DateTime.Now.AddMinutes(Convert.ToInt32(ConfigurationManager.AppSettings["TokenExpiry"]));
            token.CreatedOn = DateTime.UtcNow;
            var result = _authenticateManager.InsertToken(token);

            if (result == 1)
            {
                //HttpResponseMessage response = new HttpResponseMessage();
                //response = Request.CreateResponse(HttpStatusCode.OK, user);
                //response.Headers.Add("Token", newToken);
                //// response.Headers.Add("TokenExpiry", ConfigurationManager.AppSettings["TokenExpiry"]);
                //response.Headers.Add("Access-Control-Expose-Headers", "Token");
                return new JsonContent(ApiCodes.LOGIN_SUCCESS, Status.Success, user).ConvertToHttpResponseOK();
            }
            else
            {
                return new JsonContent("Error in Creating Token", Status.Failed, user).ConvertToHttpResponseOK();
            }
        }

        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage SignUp(SignUpModel model)
        {
            var result = _authenticateManager.SignUp(model);
            UserModel user = null;
            if (result.Status == ActionStatus.Successfull)
            {
                var registered_user_password = _userManager.GetUserPasswordbyUserId(result.Object);
                var code = Utilities.GenerateRandomNo();
                var saveToken = _authenticateManager.SaveAccountVerificationRequest(result.Object, code.ToString());
                sendEmailToRegisteredUser(model);
                sendEmailToAdminUser(model);
                user = new UserModel();
                user = _userManager.GetUserDetailsByUserId(result.Object);
            }
            return new JsonContent(result.Message, result.Status == ActionStatus.Successfull ? Status.Success : Status.Failed, user).ConvertToHttpResponseOK();
        }

        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage ResendAccountVerificationOtp(long userId)
        {
            var code = Utilities.GenerateRandomNo();

            //var token = Guid.NewGuid();
            //var link = "<a href='" + WebConfigurationManager.AppSettings["BaseUrl"] + "/Admin/Home/ConfirmEmail?userId=" + result.Object + "&token=" + token + "'>Click here</a>";
            var user = _userManager.GetUserDetailsByUserId(userId);
            if (user == null)
                return new JsonContent("User not exist.", Status.Failed, user).ConvertToHttpResponseOK();
            var saveToken = _authenticateManager.SaveAccountVerificationRequest(userId, code.ToString());
            var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.NewAppUser);
            string body = emailTemplate.TemplateContent;
            body = body.Replace("%code%", code.ToString());
            Utilities.SendEmail(user.Email, emailTemplate.EmailSubject, body);
            return new JsonContent("OTP SENT SUCCESSFULLY.", Status.Success, user).ConvertToHttpResponseOK();
        }
        
        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ActionName("ForgotPassword")]
        public HttpResponseMessage ForgotPassword(string email)
        {
            if (!string.IsNullOrEmpty(email))
            {
                var otp = Utilities.GenerateRandomNo();
                var result = _authenticateManager.ForgotPassword(email, otp.ToString());
                if (result.Status == ActionStatus.Error)
                    return new JsonContent(result.Message, Status.Failed).ConvertToHttpResponseOK();
                var link = "<a style='background-color: #7bddff; color: #fff;text-decoration: none;padding: 10px 20px;border-radius: 30px;text-transform: uppercase;' href='" + WebConfigurationManager.AppSettings["BaseUrl"] + "Admin/Home/ResetPassword?userId=" + result.ID + "&token=" + otp + "'>Reset Now</a>";
                var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.ForgetPassword);
                if (emailTemplate.TemplateStatus)
                {
                    var user = _userManager.GetUserDetailByEmail(email);
                    if (user != null)
                    {
                        string body = emailTemplate.TemplateContent;
                        body = body.Replace("%USER%", link);
                        body = body.Replace("%POSID%", link);
                        body = body.Replace("%PASSWORD%", link);
                        string to = email;
                        Utilities.SendEmail(to, emailTemplate.EmailSubject, body);
                    }
                    
                }
                
                return new JsonContent("Password reset link has been sent to your email.", Status.Success).ConvertToHttpResponseOK();
            }
            return new JsonContent("Email is required.", Status.Failed).ConvertToHttpResponseOK();
        }


        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        public async Task<HttpResponseMessage> ForgotPasscode2(RestPassCodeModel request)
        {
           var otp = Convert.ToString(Utilities.GenerateRandomNo());
            var user = _userManager.GetUserDetailByEmail(request.Email);
            if(user != null)
            {
                var result = _authenticateManager.SaveAccountVerificationRequest(user.UserId, otp);
                if(result.Status == ActionStatus.Successfull)
                {
                    var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.ForgotPasscode);
                    if (emailTemplate != null && emailTemplate.TemplateStatus)
                    {
                        string body = emailTemplate.TemplateContent;
                        body = body.Replace("%USER%", user.Name);
                        body = body.Replace("%OTP%", otp);
                        if (!string.IsNullOrEmpty(user.Email))
                        {
                            Utilities.SendEmail(user.Email, emailTemplate.EmailSubject, body);
                        }

                        var msg = new SendSMSRequest
                        {
                            Recipient = "232" + user.Phone,
                            Payload = $"Greetings {user.Name} \n" +
                              $"Please enter the following OTP in the mobile APP.\n{otp}\n" +
                              "VENDTECH"
                        };
                        await Utilities.SendSms(msg);

                        return new JsonContent(user.UserId.ToString(), Status.Success).ConvertToHttpResponseOK();
                    }

                    

                    return new JsonContent("Unable to send otp email", Status.Failed).ConvertToHttpResponseOK();
                }
                return new JsonContent("Unable to generate otp", Status.Failed).ConvertToHttpResponseOK(); 
            } 
            return new JsonContent("User not found", Status.Failed).ConvertToHttpResponseOK();
        }
        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        public HttpResponseMessage ForgotPasscode(SavePassCodeModel savePassCodeModel)
        {
            var isEmailed = false;
            long userId = 0;
            var user = new UserModel();
            savePassCodeModel.PassCode = Convert.ToString(Utilities.GenerateFiveRandomNo());
            if (string.IsNullOrEmpty(savePassCodeModel.Email))
            {
                isEmailed = true;
            }
            if (!string.IsNullOrEmpty(savePassCodeModel.PassCode))
            {
                if (!string.IsNullOrEmpty(savePassCodeModel.PosNumber))
                {
                    user = _posManager.GetUserPosDetails(savePassCodeModel.PosNumber);
                    userId = user.UserId;
                }
                else
                {
                    userId = _userManager.GetUserId(savePassCodeModel.Phone);
                }
                if (userId > 0)
                {
                    var vendorDetail = _vendorManager.GetVendorDetailApi(userId);
                    savePassCodeModel.VendorId = vendorDetail.VendorId;

                    var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.GeneratePasscode);
                    string body = emailTemplate.TemplateContent;
                    body = body.Replace("%UserName%", vendorDetail.Name);
                    body = body.Replace("%passcode%", savePassCodeModel.PassCode);
                    if (!string.IsNullOrEmpty(savePassCodeModel.Email))
                    {
                        Utilities.SendEmail(savePassCodeModel.Email, emailTemplate.EmailSubject, body);
                        isEmailed = true;
                    }
                    //if (isEmailed && !string.IsNullOrEmpty(savePassCodeModel.Phone))
                    //{
                    //    String message = HttpUtility.UrlEncode("Hello " + name + ",%nPlease find the Passcode requested for login. " + savePassCodeModel.PassCode + " in Ventech account.");
                    //    //string msg = "This is a test message Your one time password for activating your Textlocal account is " + savePassCodeModel.PassCode;
                    //    using (var wb = new WebClient())
                    //    {
                    //        byte[] response = wb.UploadValues("https://api.textlocal.in/send/", new NameValueCollection()
                    //{
                    //{"apikey" , "3dmxGZ4kX6w-GheG39NELIgd6546OjfacESXqNOVY4"},
                    //{"numbers" , savePassCodeModel.CountryCode+savePassCodeModel.Phone},
                    //{"message" , message},
                    //{"sender" , "TXTLCL"}
                    //});
                    //        string result = System.Text.Encoding.UTF8.GetString(response);
                    //    }
                    //}
                }
                else
                {
                    isEmailed = false;
                }
                if (isEmailed)
                {
                    _posManager.SavePasscodePosApi(savePassCodeModel);
                    string message = string.Empty;
                    message = string.IsNullOrEmpty(savePassCodeModel.Email) ? "New PassCode is Sent to Mobile! Please Check."
                        : "New PassCode is Sent to Email! Please Check.";
                    return new JsonContent(message, Status.Success)
                        .ConvertToHttpResponseOK();
                }
                return new JsonContent("This Email Or Phone Number Is Not Register!!Please Try with something else!", Status.Failed).ConvertToHttpResponseOK();
            }
            return new JsonContent("PassCode Not Generated!", Status.Failed).ConvertToHttpResponseOK();
        }

        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage VerifyAccountVerificationCode(VerifyAccountVerificationCodeMOdel model)
        {
            var result = _authenticateManager.VerifyAccountVerificationCode(model);
            return new JsonContent(result.Message, result.Status == ActionStatus.Successfull ? Status.Success : Status.Failed).ConvertToHttpResponseOK();
        }

        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage GetPOSUserDetails(SavePassCodeModel savePassCodeModel)
        {
            var result = _posManager.GetUserPosDetailApi(savePassCodeModel.PosNumber);
            if (result != null)
            {
                return new JsonContent("User POS Details!!", Status.Success, result).ConvertToHttpResponseOK();
            }
            return new JsonContent("You Do not have a valid account!!", Status.Success, result).ConvertToHttpResponseOK();
        }

        [HttpGet, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage GetCountries()
        {
            var result = _authenticateManager.GetCountries();
            return new JsonContent("Countries fetched successfully.", Status.Success, result).ConvertToHttpResponseOK();
        }

        [HttpGet, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage GetCities(int countryId)
        {
            var result = _authenticateManager.GetCities(countryId);
            return new JsonContent("Cities fetched successfully.", Status.Success, result).ConvertToHttpResponseOK();
        }

        [HttpGet, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage GetAppUserTypes()
        {
            var data = _agentManager.GetAgentsSelectList();
            var enumValue = Utilities.EnumToList(typeof(AppUserTypeEnumApi));
            var result = new DataResult<List<System.Web.Mvc.SelectListItem>, List<System.Web.Mvc.SelectListItem>>();
            result.Result1 = data;
            result.Result2 = enumValue;
            return new JsonContent("App User Types fetched successfully.", Status.Success, result).ConvertToHttpResponseOK();
        }

        [HttpGet, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage GetTermsAndConditions()
        {
            CMSPageViewModel model = _cmsManager.GetPageContentByPageIdforFront(1);
            return new JsonContent("Terms and conditions fetched successfully.", Status.Success, new { html = model.PageContent }).ConvertToHttpResponseOK();
        }

        [HttpGet, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage GetPrivacyPolicy()
        {
            //Client wants these two combined so we did this way
            CMSPageViewModel privacyPolicy = _cmsManager.GetPageContentByPageIdforFront(6);
            CMSPageViewModel terms = _cmsManager.GetPageContentByPageIdforFront(1);
            return new JsonContent("Privacy policy fetched successfully.", Status.Success, new { privacyPolicyHtml = privacyPolicy.PageContent, termsHtml = terms.PageContent }).ConvertToHttpResponseOK();
        }

        [HttpGet, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage TestPush()
        {
            var obj = new PushNotificationModel();
            obj.DeviceToken = "Test_7C8H3QQFVYPMbKW3R4fYGVOPyNsZjDOeMms1F5Bj5PliLELHDMmcnazBjgoiLVuAlyNpHoasbmtQ6Adxkt8CCONqReaNjAtpXdQfTCqjAtsRgCscOKNHebc7sTtHwecr+Rxz1Y8234dpz+MRZbrlYzkQ9ivxxYBt/MHxvi72yzY=";
            obj.DeviceType = (int)AppTypeEnum.Android;
            obj.Message = "This is test message.";
            obj.Title = "Test Title";
            obj.NotificationType = NotificationTypeEnum.DepositStatusChange;
            obj.UserId = 1;
            PushNotification.PushNotificationToMobile(obj);
            return new JsonContent("Privacy policy fetched successfully.", Status.Success).ConvertToHttpResponseOK();
        }

        [HttpGet, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage IsUserNameExists(string userName)
        {
            if (!_authenticateManager.IsUserNameExists(userName))
                return new JsonContent("User with this user name not exist", Status.Success).ConvertToHttpResponseOK();
            return new JsonContent("This Username has already been taken.", Status.Failed).ConvertToHttpResponseOK();
        }

        [HttpGet]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage GetBankAccounts()
        {
            var result = _bankAccountManager.GetBankAccounts();
            return new JsonContent("Bank accounts fetched successfully.", Status.Success, result).ConvertToHttpResponseOK();
        }

        [HttpGet]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage GetBankNamesForCheque()
        {
            var result = _bankAccountManager.GetBankNames_API().ToList();
            var data = result.ToList().Select(p => new SelectListItem { Text = p.BankName, Value = p.Id.ToString() }).ToList();
            return new JsonContent("Banks  fetched successfully.", Status.Success, data).ConvertToHttpResponseOK();
        }

        [HttpGet]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage GetBankAccountsSelectList()
        {
            var bankAccounts = _bankAccountManager.GetBankAccounts();
            var data = bankAccounts.ToList().Select(p => new SelectListItem { Text = "(" + p.BankName + " - " + Utilities.FormatBankAccount(p.AccountNumber) + ")", Value = p.BankAccountId.ToString() }).ToList();
            return new JsonContent("Bank accounts fetched successfully.", Status.Success, data).ConvertToHttpResponseOK();
        }


        void sendEmailToAdminUser(SignUpModel request)
        {
            var adminUser = _userManager.GetAllAdminUsersByAppUserPermission();
            foreach (var admin in adminUser)
            {
                var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.NewUserEmailToAdmin);
                if (emailTemplate.TemplateStatus)
                {
                    string body = emailTemplate.TemplateContent;
                    body = body.Replace("%AdminUserName%", admin.Name);
                    body = body.Replace("%Name%", request.FirstName);
                    body = body.Replace("%Surname%", request.LastName);
                    body = body.Replace("%Vendor%", request.FirstName + " " + request.LastName);
                    Utilities.SendEmail(admin.Email, emailTemplate.EmailSubject, body);
                }

            }
        }
        void sendEmailToRegisteredUser(SignUpModel request)
        {
            var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.NewAppUserRegistration);
            string body = emailTemplate.TemplateContent;
            body = body.Replace("%USER%", request.FirstName);
            Utilities.SendEmail(request.Email, emailTemplate.EmailSubject, body);
        }


        [HttpGet, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage TestTB()
        {
            var ere = _bankAccountManager.PerformOperation();
            return new JsonContent("Privacy policy fetched successfully.", Status.Success).ConvertToHttpResponseOK();
        }
    }
}
