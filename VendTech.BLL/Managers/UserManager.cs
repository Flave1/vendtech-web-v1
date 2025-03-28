﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Linq.Dynamic;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using VendTech.BLL.Common;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.DAL;

namespace VendTech.BLL.Managers
{
    public class UserManager : BaseManager, IUserManager
    {
        private readonly VendtechEntities _context;

        public UserManager()
        {
            _context = new VendtechEntities();
        }
   

        string IUserManager.GetWelcomeMessage()
        {
            return "Welcome To Base Project Demo";
        }

        UserModel IUserManager.ValidateUserSession(string token)
        {
            using(var context = new VendtechEntities())
            {
                if (!string.IsNullOrEmpty(token))
                {
                    var session = context.TokensManagers.Where(o => o.TokenKey.Equals(token)).FirstOrDefault();
                    if (session != null)
                    {
                        var currentTimeWithAppSeconds = session.User.AppLastUsed.Value.AddSeconds(Convert.ToInt16(context.AppSettings.FirstOrDefault().Value));
                        var hasExpired = currentTimeWithAppSeconds < DateTime.UtcNow;

                        //Utilities.LogProcessToDatabase($"currentTimeWithAppSeconds: {currentTimeWithAppSeconds}", "");
                        //Utilities.LogProcessToDatabase($"DateTime.UtcNow: {DateTime.UtcNow}", "");
                        //if (hasExpired)
                        //{
                        //    return null;
                        //}
                        var pos = context.POS.FirstOrDefault(x => x.SerialNumber == session.PosNumber);
                        if (session != null &&
                            (session.User.Status == (int)UserStatusEnum.Active
                            || session.User.Status == (int)UserStatusEnum.Pending
                            || session.User.Status == (int)UserStatusEnum.PasswordNotReset) && pos.Enabled == true) return new UserModel(session.TokenKey, session.User);
                        else return null;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }           
        }

       
        bool IUserManager.UpdateUserLastAppUsedTime(long userId)
        {

            using (var context = new VendtechEntities())
            {
                var user = context.Users.FirstOrDefault(p => p.UserId == userId);
                if (user != null)
                {
                    user.AppLastUsed = DateTime.UtcNow;
                    context.SaveChanges();
                    return true;
                }
                return false;
            }
            
        }
        ActionOutput IUserManager.UpdateProfilePic(long userId, HttpPostedFile image)
        {
            var user = _context.Users.FirstOrDefault(p => p.UserId == userId);
            var myfile = string.Empty;
            if (user == null)
                return ReturnError("User not exist.");
            if (image != null)
            {
                var ext = Path.GetExtension(image.FileName); //getting the extension(ex-.jpg)  
                myfile = Guid.NewGuid().ToString() + ext; //appending the name with id  
                // store the file inside ~/project folder(Images/ProfileImages)  
                var folderName = HttpContext.Current.Server.MapPath("~/Images/ProfileImages");
                if (!Directory.Exists(folderName))
                    Directory.CreateDirectory(folderName);
                var path = Path.Combine(folderName, myfile);
                image.SaveAs(path);

                //var obj = new QuickbloxServices();
                //var result = obj.RegisterUser(model);
            }
            else
                return ReturnError("Please attach an image.");
            if (!string.IsNullOrEmpty(user.ProfilePic))
            {
                if (File.Exists(HttpContext.Current.Server.MapPath("~" + user.ProfilePic)))
                    File.Delete(HttpContext.Current.Server.MapPath("~" + user.ProfilePic));
            }
            user.ProfilePic = string.IsNullOrEmpty(myfile) ? "" : "/Images/ProfileImages/" + myfile;
            _context.SaveChanges();
            return ReturnSuccess("Profile picture updated successfully.");
        }
        ActionOutput<ApiResponseUserDetail> IUserManager.GetUserDetailsForApi(long userId)
        {
            var user = _context.Users.FirstOrDefault(p => p.UserId == userId);
            if (user == null)
                return ReturnError<ApiResponseUserDetail>("User Not exist");
            return ReturnSuccess<ApiResponseUserDetail>(new ApiResponseUserDetail(user), "User detail fetched successfully.");
        }
        UpdateProfileModel IUserManager.GetAppUserProfile(long userId)
        {
            var user = _context.Users.FirstOrDefault(p => p.UserId == userId);
            if (user == null)
                return null;
            return new UpdateProfileModel
            {
                Address = user.Address,
                City = user.CityId,
                Country = user.CountryId,
                ProfilePicUrl = string.IsNullOrEmpty(user.ProfilePic) ? "" : Utilities.DomainUrl + user.ProfilePic,
                Name = user.Name,
                SurName = user.SurName,
                Phone = user.Phone,
                Email = user.Email
            };
        }
        ActionOutput IUserManager.UpdateUserProfile(long userId, UpdateProfileModel model)
        {
            var user = _context.Users.FirstOrDefault(p => p.UserId == userId);
            var myfile = string.Empty;

            if (user == null)
                return ReturnError("User Not exist");
            user.UpdatedAt = DateTime.UtcNow;
            user.Name = model.Name;
            user.SurName = model.SurName;
            user.CityId = model.City;
            user.DOB = model.DOB != null ? model.DOB : user.DOB;
            user.CountryId = model.Country;
            user.Phone = model.Phone;
            user.Address = model.Address;
            try
            {
                if (model.Image != null)
                {
                    var ext = Path.GetExtension(model.Image.FileName); //getting the extension(ex-.jpg)  
                    myfile = Guid.NewGuid().ToString() + ext; //appending the name with id  
                                                              // store the file inside ~/project folder(Images/ProfileImages)  
                    var folderName = HttpContext.Current.Server.MapPath("~/Images/ProfileImages");
                    if (!Directory.Exists(folderName))
                        Directory.CreateDirectory(folderName);
                    var path = Path.Combine(folderName, myfile);
                    model.Image.SaveAs(path);
                    if (!string.IsNullOrEmpty(user.ProfilePic))
                    {
                        if (File.Exists(HttpContext.Current.Server.MapPath("~" + user.ProfilePic)))
                            File.Delete(HttpContext.Current.Server.MapPath("~" + user.ProfilePic));
                    }
                    user.ProfilePic = string.IsNullOrEmpty(myfile) ? "" : "/Images/ProfileImages/" + myfile;
                    //var obj = new QuickbloxServices();
                    //var result = obj.RegisterUser(model);
                }
                _context.SaveChanges();
                return ReturnSuccess("User profile updated successfully, All changes will be fully update by the next login.");
            }
            catch (Exception e)
            {
                return ReturnError(e.ToString());
            }
        }

        ActionOutput IUserManager.UpdateAdminprofile(long userId, UpdateProfileModel model)
        {
            var user = _context.Users.FirstOrDefault(p => p.UserId == userId);
            var myfile = string.Empty;

            if (user == null)
                return ReturnError("Admin Not exist");
            user.UpdatedAt = DateTime.UtcNow;
            user.Name = model.Name;
            user.SurName = model.SurName;
            user.CityId = model.City;
            user.DOB = model.DOB != null ? model.DOB : user.DOB;
            user.CountryId = model.Country;
            user.Phone = model.Phone;
            user.Address = model.Address;
            if (model.Image != null)
            {
                var ext = Path.GetExtension(model.Image.FileName); //getting the extension(ex-.jpg)  
                myfile = Guid.NewGuid().ToString() + ext; //appending the name with id  
                // store the file inside ~/project folder(Images/ProfileImages)  
                var folderName = HttpContext.Current.Server.MapPath("~/Images/ProfileImages");
                if (!Directory.Exists(folderName))
                    Directory.CreateDirectory(folderName);
                var path = Path.Combine(folderName, myfile);
                model.Image.SaveAs(path);
                if (!string.IsNullOrEmpty(user.ProfilePic))
                {
                    if (File.Exists(HttpContext.Current.Server.MapPath("~" + user.ProfilePic)))
                        File.Delete(HttpContext.Current.Server.MapPath("~" + user.ProfilePic));
                }
                user.ProfilePic = string.IsNullOrEmpty(myfile) ? "" : "/Images/ProfileImages/" + myfile;
            }
            _context.SaveChanges();
            return ReturnSuccess("Admin profile has been  updated successfully, All changes will be fully update by the next login.");
        }


        PagingResult<NotificationApiListingModel> IUserManager.GetUserNotifications(int pageNo, int pageSize, long userId)
        {
            var result = new PagingResult<NotificationApiListingModel>();
            var query = _context.Notifications.Where(p => p.UserId == userId);
            result.TotalCount = query.Count();
            result.List = query.OrderByDescending(p => p.SentOn).Skip((pageNo - 1) * pageSize).Take(pageSize).ToList().Select(x => new NotificationApiListingModel(x)).ToList();
            result.Message = "Notifications fetched successfully.";
            query.ToList().ForEach(p => p.MarkAsRead = true);
            _context.SaveChanges();
            return result;
        }

        void IUserManager.DisposeUserNotifications()
        {
            try
            {
                _context.Database.ExecuteSqlCommand("DELETE FROM Notifications WHERE SentOn < DATEADD(day, -7, GETUTCDATE())");
            }
            catch (Exception)
            {
                // Handle exception if needed
            }
        }

        DataResult<List<MeterRechargeApiListingModel>, List<DepositListingModel>, ActionStatus> IUserManager.GetUserNotificationApi(int pageNo, int pageSize, long userId)
        {
            var result = new DataResult<List<MeterRechargeApiListingModel>, List<DepositListingModel>, ActionStatus>();
            IQueryable<TransactionDetail> query = null;

            query = _context.TransactionDetails.Where(p => !p.IsDeleted && p.POSId != null && p.Finalised == true);

            var user = _context.Users.FirstOrDefault(p => p.UserId == userId);
            var posIds = new List<long>();
            posIds = _context.POS.Where(p => p.VendorId != null && (p.VendorId == user.FKVendorId)).Select(p => p.POSId).ToList();
            query = query.Where(p => posIds.Contains(p.POSId.Value));
            result.Result1 = query.Where(no => _context.Notifications.Where(d => d.MarkAsRead == false && d.UserId == user.UserId).Select(i => i.RowId).AsEnumerable().Contains(no.TransactionDetailsId))
                .OrderByDescending(x => x.CreatedAt).Skip((pageNo - 1) * pageSize).Take(pageSize).ToList().Select(x => new MeterRechargeApiListingModel
            {
                Amount = Utilities.FormatAmount(x.Amount),
                CreatedAt = x.CreatedAt.ToString("dd/MM/yyyy hh:mm"),//ToString("dd/MM/yyyy HH:mm"),
                MeterNumber = x.Meter == null ? x.MeterNumber1 : x.Meter.Number,
                TransactionId = x.TransactionId,
                MeterRechargeId = x.TransactionDetailsId,
                RechargePin = x?.MeterToken1,
                POSId = x.POSId == null ? "" : x.POS.SerialNumber,
                NotType = "sale",
                PlatformId = x.PlatFormId
            }).ToList();
            IQueryable<DepositLog> query1 = null;

            query1 = _context.DepositLogs.OrderByDescending(p => p.Deposit.CreatedAt).Where(p => p.NewStatus == (int)DepositPaymentStatusEnum.Released);

            posIds = _context.POS.Where(p => p.VendorId != null && (p.VendorId == user.FKVendorId)).Select(p => p.POSId).ToList();
            query1 = query1.OrderByDescending(x => x.CreatedAt).Where(p => posIds.Contains(p.Deposit.POSId));
            var totalrecoed = query.ToList().Count();
            result.Result2 = query1.Where(no => _context.Notifications.Where(d => d.MarkAsRead == false && d.UserId == user.UserId).Select(i => i.RowId).AsEnumerable().Contains(no.DepositId))
               .Skip((pageNo - 1) * pageSize).Take(pageSize).AsEnumerable().Select(x => new DepositListingModel(x.Deposit)).ToList();


            result.Result3 = ActionStatus.Successfull;
            return result;
        }

        PagingResult<UserListingModel> IUserManager.GetUserPagedList(PagingModel model, bool onlyAppUser, string status)
        {
            if(model.SortBy == null)
            {
                model.SortBy = "Vendor";
            }
            model.RecordsPerPage = 10000000;
            var result = new PagingResult<UserListingModel>();
            IQueryable<User> query = null;

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = _context.Users.Where(z => ((UserStatusEnum)z.Status).ToString().ToLower().Contains(status.ToLower()));
            }
            else
            {
                if (model.IsActive)
                    query = _context.Users.Where(p => p.Status == (int)UserStatusEnum.Active);
                else
                    query = _context.Users.Where(p => p.Status != (int)UserStatusEnum.Active);
            }


            if (model.SortBy == "POS")
            {
                if (model.SortOrder == "Asc")
                {
                    query = query.OrderBy(s => s.POS.FirstOrDefault().SerialNumber);
                }
                else if (model.SortOrder == "Desc")
                {
                    query = query.OrderByDescending(s => s.POS.FirstOrDefault().SerialNumber);
                }
            }
            else if (model.SortBy == "Balance")
            {
                if (model.SortOrder == "Asc")
                {
                    query = query.OrderBy(s => s.POS.FirstOrDefault().Balance);
                }
                else if (model.SortOrder == "Desc")
                {
                    query = query.OrderByDescending(s => s.POS.FirstOrDefault().Balance);
                }
            }
            else if (model.SortBy == "Vendor")
            {
                if (model.SortOrder == "Asc")
                {
                    query = query.OrderBy(s => s.Vendor);
                }
                else if (model.SortOrder == "Desc")
                {
                    query = query.OrderByDescending(s => s.Vendor);
                }
            }
            else if (model.SortBy == "SurName")
            {
                if (model.SortOrder == "Asc")
                {
                    query = query.OrderBy(s => s.SurName);
                }
                else if (model.SortOrder == "Desc")
                {
                    query = query.OrderByDescending(s => s.SurName);
                }
            }
            else if (model.SortBy == "EMAIL")
            {
                if (model.SortOrder == "Asc")
                {
                    query = query.OrderBy(s => s.Email);
                }
                else if (model.SortOrder == "Desc")
                {
                    query = query.OrderByDescending(s => s.Email);
                }
            }
            else if (model.SortBy == "PHONE")
            {
                if (model.SortOrder == "Asc")
                {
                    query = query.OrderBy(s => s.Phone);
                }
                else if (model.SortOrder == "Desc")
                {
                    query = query.OrderByDescending(s => s.Phone);
                }
            }
            else
                query = query.OrderBy(model.SortBy + " " + model.SortOrder);
            //Client want to show app user and vendor on the same screen because they both can login from app
            if (onlyAppUser)
                query = query.Where(p => p.UserRole.Role == UserRoles.AppUser || p.UserRole.Role == UserRoles.Vendor);
            else
                query = query.Where(p => p.UserRole.Role != UserRoles.AppUser && p.UserRole.Role != UserRoles.Vendor);

            if (!string.IsNullOrEmpty(model.Search) && !string.IsNullOrEmpty(model.SearchField))
            {
                if (model.SearchField.Equals("FIRST"))
                    query = query.Where(z => z.Name.ToLower().Contains(model.Search.ToLower()));
                if (model.SearchField.Equals("EMAIL"))
                    query = query.Where(z => z.Email.ToLower().Contains(model.Search.ToLower()));
                if (model.SearchField.Equals("PHONE"))
                    query = query.Where(z => z.Phone.ToLower().Contains(model.Search.ToLower()));

                else if (model.SearchField.Equals("POSID"))
                    query = query.Where(z => z.POS.FirstOrDefault().SerialNumber.ToLower().Contains(model.Search.ToLower()));
                else if (model.SearchField.Equals("LAST"))
                    query = query.Where(z => z.SurName.ToLower().Contains(model.Search.ToLower()));
                else if (model.SearchField.Equals("STATUS"))
                    query = query.Where(z => ((UserStatusEnum)z.Status).ToString().ToLower().Contains(model.Search.ToLower()));
                else if (model.SearchField.Equals("VENDOR"))
                    query = query.Where(z => z.Vendor.ToLower().Contains(model.Search.ToLower()));
                else if (model.SearchField.Equals("ROLE"))
                    query = query.Where(z => z.UserRole.Role.ToLower().Contains(model.Search.ToLower()));
            }
            

           
           
            
            var list = query
               .Skip(model.PageNo - 1)
               .ToList().Select(x => new UserListingModel(x)).ToList();


            result.List = list;
            result.Status = ActionStatus.Successfull;
            result.Message = "User List";
            result.TotalCount = query.Count();
            return result;
        }

        PagingResult<B2bUserListingModel> IUserManager.GetB2BUserPagedList(PagingModel model, string status)
        {
            if (model.SortBy == null)
            {
                model.SortBy = "Vendor";
            }
            model.RecordsPerPage = 10000000;
            var result = new PagingResult<B2bUserListingModel>();
            IQueryable<User> query = null;

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = _context.Users.Where(z => ((UserStatusEnum)z.Status).ToString().ToLower().Contains(status.ToLower()));
            }
            else
            {
                if (model.IsActive)
                    query = _context.Users.Where(p => p.Status == (int)UserStatusEnum.Active);
                else
                    query = _context.Users.Where(p => p.Status != (int)UserStatusEnum.Active);
            }

            query = query.Where(p => p.UserRole.Role == UserRoles.B2BUsers);

            if (model.SortBy == "POS")
            {
                if (model.SortOrder == "Asc")
                {
                    query = query.OrderBy(s => s.POS.FirstOrDefault().SerialNumber);
                }
                else if (model.SortOrder == "Desc")
                {
                    query = query.OrderByDescending(s => s.POS.FirstOrDefault().SerialNumber);
                }
            }
            else if (model.SortBy == "Balance")
            {
                if (model.SortOrder == "Asc")
                {
                    query = query.OrderBy(s => s.POS.FirstOrDefault().Balance);
                }
                else if (model.SortOrder == "Desc")
                {
                    query = query.OrderByDescending(s => s.POS.FirstOrDefault().Balance);
                }
            }
            else if (model.SortBy == "Vendor")
            {
                if (model.SortOrder == "Asc")
                {
                    query = query.OrderBy(s => s.Vendor);
                }
                else if (model.SortOrder == "Desc")
                {
                    query = query.OrderByDescending(s => s.Vendor);
                }
            }
            else if (model.SortBy == "SurName")
            {
                if (model.SortOrder == "Asc")
                {
                    query = query.OrderBy(s => s.SurName);
                }
                else if (model.SortOrder == "Desc")
                {
                    query = query.OrderByDescending(s => s.SurName);
                }
            }
            else
                query = query.OrderBy(model.SortBy + " " + model.SortOrder);
           

            if (!string.IsNullOrEmpty(model.Search) && !string.IsNullOrEmpty(model.SearchField))
            {
                if (model.SearchField.Equals("FIRST"))
                    query = query.Where(z => z.Name.ToLower().Contains(model.Search.ToLower()));

                else if (model.SearchField.Equals("POSID"))
                    query = query.Where(z => z.POS.FirstOrDefault().SerialNumber.ToLower().Contains(model.Search.ToLower()));
                else if (model.SearchField.Equals("LAST"))
                    query = query.Where(z => z.SurName.ToLower().Contains(model.Search.ToLower()));
                else if (model.SearchField.Equals("STATUS"))
                    query = query.Where(z => ((UserStatusEnum)z.Status).ToString().ToLower().Contains(model.Search.ToLower()));
                else if (model.SearchField.Equals("VENDOR"))
                    query = query.Where(z => z.Vendor.ToLower().Contains(model.Search.ToLower()));
                else if (model.SearchField.Equals("ROLE"))
                    query = query.Where(z => z.UserRole.Role.ToLower().Contains(model.Search.ToLower()));
            }





            var list = query
               .Skip(model.PageNo - 1)
               .ToList().Select(x => new B2bUserListingModel(x)).ToList();


            result.List = list;
            result.Status = ActionStatus.Successfull;
            result.Message = "User List";
            result.TotalCount = query.Count();
            return result;
        }

        ActionOutput<string> IUserManager.SaveReferralCode(long userId)
        {
            var user = _context.Users.FirstOrDefault(p => p.UserId == userId);
            if (user == null)
                return ReturnError<string>("User not exist");
            var dbReferralCode = new ReferralCode();
            dbReferralCode.Code = Utilities.RandomString(6);
            dbReferralCode.IsUsed = false;
            dbReferralCode.FK_UserId = userId;
            dbReferralCode.CreatedAt = DateTime.UtcNow;
            _context.ReferralCodes.Add(dbReferralCode);
            _context.SaveChanges();
            return ReturnSuccess<string>(dbReferralCode.Code, "Referral code generated successfully");
        }
        int IUserManager.GetUnreadNotifications(long userId)
        {
            return _context.Notifications.Where(p => p.UserId == userId && !p.MarkAsRead).Count();
        }

        ActionOutput<UserDetailForAdmin> IUserManager.AdminLogin(LoginModal model)
        { 
            string encryptPasswordde = Utilities.DecryptPassword("VnRlY2hAVjNuNA==");
            string encryptPassword = Utilities.EncryptPassword(model.Password.Trim());
            var user = _context.Users.FirstOrDefault(p =>
            (UserRoles.AppUser != p.UserRole.Role) && (UserRoles.Vendor != p.UserRole.Role) && (UserRoles.Agent != p.UserRole.Role) &&
            (p.Status == (int)UserStatusEnum.Active || p.Status == (int)UserStatusEnum.PasswordNotReset) &&
             p.Password == encryptPassword &&
             p.Email.ToLower() == model.UserName.ToLower());
            if (user == null)
                return null;
            var modelUser = new UserDetailForAdmin
            {
                FirstName = user.Name,
                LastName = user.SurName,
                UserEmail = user.Email,
                UserID = user.UserId,
                UserType = user.UserRole.Role,
                UserName = user.UserName
                //ProfilePicPath = user.ProfilePic
            };
            return ReturnSuccess<UserDetailForAdmin>(modelUser, "User logged in successfully.");
        }

        UserDetails IUserManager.BackgroundAdminLogin(long taskId)
        {
            var user = _context.Users.FirstOrDefault(p => p.UserId == taskId && (UserRoles.AppUser != p.UserRole.Role) && (UserRoles.Vendor != p.UserRole.Role) && (UserRoles.Agent != p.UserRole.Role) &&
            (p.Status == (int)UserStatusEnum.Active || p.Status == (int)UserStatusEnum.PasswordNotReset));
            if (user == null)
                return null;
            var modelUser = new UserDetails
            {
                FirstName = user.Name,
                LastName = user.SurName,
                UserEmail = user.Email,
                UserID = user.UserId,
                UserType = user.UserRole.Role,
                UserName = user.Email,
                IsAuthenticated = true,
                LastActivityTime = DateTime.UtcNow,
            };
            return modelUser;
        }

        void IUserManager.SaveChanges() => _context.SaveChanges();


        IList<UserAssignedModuleModel> IUserManager.GetNavigations(long userId)
        {
            var userModule = (from ua in _context.UserAssignedModules
                              join m in _context.Modules on ua.ModuleId equals m.ModuleId
                              where ua.UserId == userId
                              select new
                              {
                                  AssignUserModuleId = ua.AssignUserModuleId,
                                  ModuleName = m.ModuleName
                              }).ToList();
            return userModule.Select(x => new UserAssignedModuleModel
            {
                Modules = x.ModuleName,
                AssignUserModuleId = x.AssignUserModuleId
            }).ToList();
        }
        long IUserManager.GetUserId(string phone)
        {
            var userDetail = _context.Users.FirstOrDefault(x => x.Phone == phone);
            if (userDetail != null)
            {
                return userDetail.UserId;
            }
            return Convert.ToInt64(0);
        }

        User IUserManager.GetUserDetailByEmail(string email)
        {
            var userDetail = _context.Users.Where(x => x.Email == email && x.Status != (int)UserStatusEnum.Deleted).FirstOrDefault();
            if (userDetail != null)
            {
                return userDetail;
            }
            return null;
        }
        ActionOutput<UserDetails> IUserManager.AgentLogin(LoginModal model)
        {
            string encryptPassword = Utilities.EncryptPassword(model.Password.Trim());
            var user = _context.Agencies.SingleOrDefault();
            if (user == null)
                return null;
            var modelUser = new UserDetails
            {
                UserID = user.AgencyId
            };
            return ReturnSuccess<UserDetails>(modelUser, "User logged in successfully.");
        }

        ActionOutput<UserDetails> IUserManager.VendorLogin(LoginModal model)
        {
            string encryptPassword = Utilities.EncryptPassword(model.Password.Trim());
            string _encryptPassword = Utilities.DecryptPassword("dGVzdG15cGF5");
            var user = _context.Users.SingleOrDefault(p => p.Password == encryptPassword && p.Email.ToLower() == model.UserName.ToLower() && p.Status == (int)UserStatusEnum.Active && p.UserRole.Role == UserRoles.Vendor);
            if (user == null)
                return null;
            var modelUser = new UserDetails
            {
                FirstName = user.Name,
                LastName = user.Name,
                UserEmail = user.Email,
                UserID = user.UserId
            };
            return ReturnSuccess<UserDetails>(modelUser, "User logged in successfully.");
        }
        public IList<ModulesModel> GetAllModulesAtAuthentication(long userId)
        {
            var moduleListModel = new List<ModulesModel>();
            var modulesPermissons = _context.UserAssignedModules.Where(x => x.UserId == userId).Select(x => x.ModuleId).ToList();
            var modules = _context.Modules.Where(c => modulesPermissons.Contains(c.ModuleId)).ToList();
            if (modules.Any())
                moduleListModel = modules.Select(x => new ModulesModel(x)).ToList();
            return moduleListModel;
        }
         
        public List<SelectListItem> GetAssignedReportModules(long UserId, bool isAdmin)
        {
            if (isAdmin)
            {
                return _context.Modules.Where(p => p.SubMenuOf == 9 || p.SubMenuOf == 26 && p.ModuleId != 30).ToList().OrderBy(l => l.ModuleId).Select(p => new SelectListItem
                {
                    Text = p.ModuleName,
                    Value = p.ModuleId.ToString()
                }).ToList();
            }
         
            return _context.UserAssignedModules.Where(p => p.UserId == UserId && p.Module.SubMenuOf == 9).ToList().OrderBy(l => l.ModuleId).Select(p => new SelectListItem
            {
                Text = p.Module.ModuleName,
                Value = p.Module.ModuleId.ToString()
            }).ToList();

        }
        ActionOutput IUserManager.UpdateUserDetails(AddUserModel userDetails)
        {
            string myfile = string.Empty;
            var user = _context.Users.Where(z => z.UserId == userDetails.UserId).FirstOrDefault();
            if (user == null)
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "User Not Exist."
                };
            }
            if (IsEmailUsed(userDetails.Email, userDetails.UserId))
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "This email-id already exists for another user."
                };
            }
            else
            {
                user.Email = userDetails.Email.Trim().ToLower();
                user.Name = userDetails.FirstName;
                if (user.UserType != Utilities.GetUserRoleIntValue(UserRoles.AppUser))
                    user.UserType = (int)userDetails.UserType;
                user.SurName = userDetails.LastName;
                user.Phone = userDetails.Phone;
                user.Status = userDetails.ResetUserPassword ? (int)UserStatusEnum.PasswordNotReset : user.Status;
                user.CountryCode = userDetails.CountryCode;
                user.CountryId = userDetails.CountryId;
                user.CityId = userDetails.City;
                user.Password = Utilities.EncryptPassword(userDetails.Password);
                if (userDetails.Image != null)
                {
                    var ext = Path.GetExtension(userDetails.Image.FileName); //getting the extension(ex-.jpg)  
                    myfile = Guid.NewGuid().ToString() + ext; //appending the name with id  
                    // store the file inside ~/project folder(Images/ProfileImages)  
                    var folderName = HttpContext.Current.Server.MapPath("~/Images/ProfileImages");
                    if (!Directory.Exists(folderName))
                        Directory.CreateDirectory(folderName);
                    var path = Path.Combine(folderName, myfile);
                    userDetails.Image.SaveAs(path);
                    if (!string.IsNullOrEmpty(user.ProfilePic))
                    {
                        if (File.Exists(HttpContext.Current.Server.MapPath("~" + user.ProfilePic)))
                            File.Delete(HttpContext.Current.Server.MapPath("~" + user.ProfilePic));
                    }
                    user.ProfilePic = string.IsNullOrEmpty(myfile) ? "" : "/Images/ProfileImages/" + myfile;
                    //var obj = new QuickbloxServices();
                    //var result = obj.RegisterUser(model);
                }
                _context.SaveChanges();

                RemoveORAddUserPermissions(user.UserId, userDetails);

                RemoveOrAddUserPlatforms(user.UserId, userDetails);

                RemoveOrAddUserWidgets(user.UserId, userDetails);

                return new ActionOutput
                {
                    Status = ActionStatus.Successfull,
                    Message = "User Details Updated Successfully."
                };
            }
        }
        AddUserModel IUserManager.GetAppUserDetailsByUserId(long userId)
        {
            var user = _context.Users.Where(z => z.UserId == userId).FirstOrDefault();
            if (user == null)
                return null;
            var us = new AddUserModel();
            us.Password = Utilities.DecryptPassword(user.Password);
            us.ConfirmPassword = Utilities.DecryptPassword(user.Password);
            us.UserId = user.UserId;
            us.FirstName = user?.Name;
            us.LastName = user?.SurName;
            us.Email = user.Email;
            us.UserType = user.UserType;
            us.Phone = user?.Phone;
            us.CompanyName = user?.CompanyName;
            us.VendorId = user.FKVendorId;
            us.Address = user?.Address;
            us.AgentId = user.AgentId;
            us.CountryId = user.CountryId != null ? user.CountryId.Value : 0;
            us.City = user.CityId != null ? user.CityId.Value : 0;
            us.ProfilePicUrl = string.IsNullOrEmpty(user?.ProfilePic) ? "" : Utilities.DomainUrl + user?.ProfilePic;
            us.AutoApprove = user.AutoApprove == null ? false : (bool)user.AutoApprove;
            us.AccountStatus = ((UserStatusEnum)(user.Status)).ToString();
            return us;
        }

        AddUserModel IUserManager.GetB2bUserDetailsByUserId(long userId)
        {
            var user = _context.Users.Where(z => z.UserId == userId).FirstOrDefault();
            if (user == null)
                return null;
            var us = new AddUserModel();
            us.Password = Utilities.DecryptPassword(user.Password);
            us.ConfirmPassword = Utilities.DecryptPassword(user.Password);
            us.UserId = user.UserId;
            us.FirstName = user?.Name;
            us.LastName = user?.SurName;
            us.Email = user.Email;
            us.UserType = user.UserType;
            us.Phone = user?.Phone;
            us.CompanyName = user?.CompanyName;
            us.VendorId = user.FKVendorId;
            us.Address = user?.Address;
            us.AgentId = user.AgentId;
            //us.ClientKey = user.B2bUserAccess.FirstOrDefault().Clientkey;
            //us.APIKey = user.B2bUserAccess.FirstOrDefault().APIKey;
            us.CountryId = user.CountryId != null ? user.CountryId.Value : 0;
            us.City = user.CityId != null ? user.CityId.Value : 0;
            us.ProfilePicUrl = string.IsNullOrEmpty(user?.ProfilePic) ? "" : Utilities.DomainUrl + user?.ProfilePic;
            us.AutoApprove = user.AutoApprove == null ? false : (bool)user.AutoApprove;
            us.AccountStatus = ((UserStatusEnum)(user.Status)).ToString();
            return us;
        }
        ActionOutput IUserManager.UpdateAppUserDetails(AddUserModel userDetails)
        {
            var user = _context.Users.Where(z => z.UserId == userDetails.UserId).FirstOrDefault();
            var myfile = string.Empty;

            if (user == null)
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "User Not Exist."
                };
            }

            if (IsEmailUsed(userDetails.Email, userDetails.UserId))
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "This email-id already exists for another user."
                };
            }
            else
            {
                user.Email = userDetails.Email.Trim().ToLower();
                user.Name = userDetails.FirstName;
                user.SurName = userDetails.LastName;
                user.Phone = userDetails.Phone;
                user.CountryCode = userDetails.CountryCode;
                user.Address = userDetails.Address;
                user.AgentId = userDetails.AgentId;
                user.Password = Utilities.EncryptPassword(userDetails.Password);
                user.AutoApprove = userDetails.AutoApprove;
                user.Status = (int)UserStatusEnum.Active;
                user.Vendor = string.IsNullOrEmpty(user.Vendor) ? $"{userDetails.FirstName} {userDetails.LastName}": user.Vendor;
                user.CompanyName = user.Vendor;
                if (userDetails.VendorId.HasValue && userDetails.VendorId > 0)
                    user.FKVendorId = userDetails.VendorId;

                if (userDetails.ResetUserPassword)
                    user.Status = (int)UserStatusEnum.PasswordNotReset;
                else
                    user.Status = user.Status;

                if (userDetails.IsRe_Approval)
                    user.Status = (int)UserStatusEnum.Active;

                if (userDetails.Image != null)
                {
                    var ext = Path.GetExtension(userDetails.Image.FileName); //getting the extension(ex-.jpg)  
                    myfile = Guid.NewGuid().ToString() + ext; //appending the name with id  
                    // store the file inside ~/project folder(Images/ProfileImages)  
                    var folderName = HttpContext.Current.Server.MapPath("~/Images/ProfileImages");
                    if (!Directory.Exists(folderName))
                        Directory.CreateDirectory(folderName);
                    var path = Path.Combine(folderName, myfile);
                    userDetails.Image.SaveAs(path);
                    if (!string.IsNullOrEmpty(user.ProfilePic))
                    {
                        if (File.Exists(HttpContext.Current.Server.MapPath("~" + user.ProfilePic)))
                            File.Delete(HttpContext.Current.Server.MapPath("~" + user.ProfilePic));
                    }
                    user.ProfilePic = string.IsNullOrEmpty(myfile) ? "" : "/Images/ProfileImages/" + myfile;

                }
                _context.SaveChanges();

                RemoveORAddUserPermissions(user.UserId, userDetails);

                RemoveOrAddUserPlatforms(user.UserId, userDetails);

                RemoveOrAddUserWidgets(user.UserId, userDetails);

                return new ActionOutput{
                    ID = user.UserId,
                    Status = ActionStatus.Successfull,
                    Message = "User Details Updated Successfully."
                };
            }
        }
        IList<Checkbox> IUserManager.GetAllModules(long userId)
        {
            IList<Checkbox> chekboxListOfModules = null;
            IList<Module> modules = _context.Modules.ToList();
            if (modules.Count() > 0)
            {
                chekboxListOfModules = modules.Select(x =>
                {
                    return new Checkbox()
                    {
                        ID = x.ModuleId,
                        ModuleName = x.ModuleName,
                        Description = x.Description,
                        Checked = false,
                        SubMenuOf = x.SubMenuOf,
                        IsAdmin = x.IsAdmin
                    };
                }).ToList();

                if (userId > 0)
                {
                    var existingPermissons = _context.UserAssignedModules.Where(x => x.UserId == userId).ToList();
                    if (existingPermissons.Count() > 0)
                    {
                        chekboxListOfModules.ToList().ForEach(x => x.Checked = existingPermissons.Where(z => z.ModuleId == x.ID).Any());
                    }
                }
                else
                {
                    var selectedIds = new HashSet<int> { 19, 18, 9, 16, 17 };
                    chekboxListOfModules.ToList().ForEach(d =>
                    {
                        if (selectedIds.Contains(d.ID))
                        {
                            d.Checked = true;
                        }
                    });
                }
            }
            return chekboxListOfModules;
        }
        List<SelectListItem> IUserManager.GetUserRolesSelectList()
        {
            return _context.UserRoles.Where(p => !p.IsDeleted && p.Role != UserRoles.AppUser && p.Role != UserRoles.Vendor).ToList().Select(p => new SelectListItem
            {
                Text = p.Role.ToUpper(),
                Value = p.RoleId.ToString().ToUpper()
            }).ToList();
        }
        List<SelectListItem> IUserManager.GetAppUsersSelectList()
        {
            return _context.Users.Where(p => p.Status == (int)UserStatusEnum.Active && p.UserType != 2).OrderBy(d => d.Name).ToList().Select(p => new SelectListItem
            {
                Text = p.Name.ToUpper() + " " + p.SurName.ToUpper(),
                Value = p.UserId.ToString().ToUpper()
            }).ToList();
        }

        List<SelectListItem> IUserManager.GetAgentSelectList()
        {
            return _context.Users.Where(p => p.Status == (int)UserStatusEnum.Active && p.UserType == 2 || p.UserType == 9 || p.UserAssignedModules.Select(s => s.ModuleId).Any(e => e == 26)).OrderBy(d => d.Name).ToList().Select(p => new SelectListItem
            {
                Text = p.Name.ToUpper() + " " + p.SurName.ToUpper(),
                Value = p.UserId.ToString().ToUpper()
            }).ToList();
        }

        IList<PlatformCheckbox> IUserManager.GetAllPlatforms(long userId)
        {
            IList<PlatformCheckbox> chekboxListOfModules = null;
            IList<Platform> modules = _context.Platforms.Where(p => !p.IsDeleted && p.Enabled).ToList();
            if (modules.Count() > 0)
            {
                chekboxListOfModules = modules.Select(x =>
                {
                    return new PlatformCheckbox()
                    {
                        Id = x.PlatformId,
                        Title = x.Title,
                        Checked = false
                    };
                }).ToList();

                if (userId > 0)
                {
                    var existingPermissons = _context.UserAssignedPlatforms.Where(x => x.UserId == userId).ToList();
                    if (existingPermissons.Count() > 0)
                    {
                        chekboxListOfModules.ToList().ForEach(x => x.Checked = existingPermissons.Where(z => z.PlatformId == x.Id).Any());
                    }
                }
            }
            return chekboxListOfModules;
        }


        IList<WidgetCheckbox> IUserManager.GetAllWidgets(long userId)
        {
            IList<WidgetCheckbox> chekboxListOfModules = null;
            IList<Widget> modules = _context.Widgets.Where(p => !p.IsDeleted && p.Enabled).ToList();
            if (modules.Count() > 0)
            {
                chekboxListOfModules = modules.Select(x =>
                {
                    return new WidgetCheckbox()
                    {
                        Id = x.WidgetId,
                        Title = x.Title,
                        Checked = false
                    };
                }).ToList();

                if (userId > 0)
                {
                    var existingPermissons = _context.UserAssignedWidgets.Where(x => x.UserId == userId).ToList();
                    if (existingPermissons.Count() > 0)
                    {
                        chekboxListOfModules.ToList().ForEach(x => x.Checked = existingPermissons.Where(z => z.WidgetId == x.Id).Any());
                    }
                }
                else
                {
                    var selectedIds = new HashSet<int> { 1, 4 };
                    chekboxListOfModules.ToList().ForEach(d =>
                    {
                        if (selectedIds.Contains(d.Id))
                        {
                            d.Checked = true;
                        }
                    });
                }
            }
            return chekboxListOfModules;
        }

        ActionOutput IUserManager.AddUserDetails(AddUserModel userDetails)
        {

            var existing_user_by_number = _context.Users.FirstOrDefault(z => z.Phone.Trim().ToLower() == userDetails.Phone.Trim().ToLower()) ?? null;

            if (existing_user_by_number != null)
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "User with this phone number already exist"
                };
            }

            string myfile = string.Empty;
            if (IsEmailUsed(userDetails.Email, userDetails.UserId))
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "This email-id already exists for another user."
                };
            }
            else
            {
                var dbUser = new User();
                dbUser.Name = userDetails.FirstName;
                dbUser.SurName = userDetails.LastName;
                dbUser.Email = userDetails.Email.Trim().ToLower();
                dbUser.Password = Utilities.EncryptPassword(userDetails.Password);
                dbUser.CreatedAt = DateTime.Now;
                dbUser.UserSerialNo = _context.Users.Max(d => d.UserSerialNo) + 1;
                dbUser.UserType = userDetails.UserType;
                dbUser.Status = userDetails.ResetUserPassword ? (int)UserStatusEnum.PasswordNotReset : (int)UserStatusEnum.Active;
                dbUser.Phone = userDetails.Phone;
                dbUser.CountryCode = userDetails.CountryCode; 
                if (userDetails.Image != null)
                {
                    var ext = Path.GetExtension(userDetails.Image.FileName); //getting the extension(ex-.jpg)  
                    myfile = Guid.NewGuid().ToString() + ext; //appending the name with id  
                    // store the file inside ~/project folder(Images/ProfileImages)  
                    var folderName = HttpContext.Current.Server.MapPath("~/Images/ProfileImages");
                    if (!Directory.Exists(folderName))
                        Directory.CreateDirectory(folderName);
                    var path = Path.Combine(folderName, myfile);
                    userDetails.Image.SaveAs(path);

                    dbUser.ProfilePic = string.IsNullOrEmpty(myfile) ? "" : "/Images/ProfileImages/" + myfile;
                    //var obj = new QuickbloxServices();
                    //var result = obj.RegisterUser(model);
                }

                try
                {
                    _context.Users.Add(dbUser);
                    _context.SaveChanges();

                    RemoveORAddUserPermissions(dbUser.UserId, userDetails);
                    RemoveOrAddUserPlatforms(dbUser.UserId, userDetails);
                    RemoveOrAddUserWidgets(dbUser.UserId, userDetails);
                }
                catch (Exception e)
                {
                    throw e;
                }
                return new ActionOutput
                {
                    Status = ActionStatus.Successfull,
                    Message = "User Added Successfully."
                };
            }
        }

        long IUserManager.GetVendtechAgencyId() => _context.Agencies.FirstOrDefault(d => d.AgencyName == "VENDTECH").AgencyId;

        ActionOutput IUserManager.AddAppUserDetails(AddUserModel userDetails)
        {

            var existing_user_by_number = _context.Users.FirstOrDefault(z => z.Phone.Trim().ToLower() == userDetails.Phone.Trim().ToLower()) ?? null;

            if (existing_user_by_number != null)
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "User with this phone number already exist"
                };
            }
            string myfile = "";
            var existngUser = _context.Users.Where(z => z.Email.Trim().ToLower() == userDetails.Email.Trim().ToLower()).FirstOrDefault();
            if (IsEmailUsed(userDetails.Email, userDetails.UserId))
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "This email-id already exists for another user."
                };
            }

            else
            {
                var dbUser = new User();
                dbUser.Name = userDetails.FirstName;
                dbUser.SurName = userDetails.LastName;
                dbUser.Email = userDetails.Email.Trim().ToLower();
                dbUser.Password = Utilities.EncryptPassword(userDetails.Password);
                dbUser.CreatedAt = DateTime.Now;

                if (userDetails.UserType == (int)AppUserTypeEnum.B2B)
                {
                    dbUser.UserType = Utilities.GetUserRoleIntValue(UserRoles.B2BUsers);
                }
                else if (userDetails.IsAgencyAdmin)
                {
                    dbUser.UserType = Utilities.GetUserRoleIntValue(UserRoles.AppUser);
                }
                else
                {
                    dbUser.UserType = Utilities.GetUserRoleIntValue(UserRoles.Vendor);
                }
                dbUser.IsEmailVerified = false;
                dbUser.UserSerialNo = _context.Users.Max(d => d.UserSerialNo) + 1;
                dbUser.Address = userDetails.Address;
                dbUser.AgentId = userDetails.AgentId;
                dbUser.Phone = userDetails.Phone;
                dbUser.CountryId = userDetails.CountryId;
                dbUser.CityId = userDetails.City;
                dbUser.Status = (int)UserStatusEnum.Active;
                dbUser.Vendor =  $"{userDetails.FirstName} {userDetails.LastName}";
                dbUser.CountryCode = userDetails.CountryCode;
                if (userDetails.Image != null)
                {
                    var ext = Path.GetExtension(userDetails.Image.FileName); //getting the extension(ex-.jpg)  
                    myfile = Guid.NewGuid().ToString() + ext; //appending the name with id  
                    // store the file inside ~/project folder(Images/ProfileImages)  
                    var folderName = HttpContext.Current.Server.MapPath("~/Images/ProfileImages");
                    if (!Directory.Exists(folderName))
                        Directory.CreateDirectory(folderName);
                    var path = Path.Combine(folderName, myfile);
                    userDetails.Image.SaveAs(path);

                    dbUser.ProfilePic = string.IsNullOrEmpty(myfile) ? "" : "/Images/ProfileImages/" + myfile;
                }
                try
                {
                    _context.Users.Add(dbUser);
                    _context.SaveChanges();
                }
                catch (Exception ex)
                {
                    return new ActionOutput
                    {
                        ID = dbUser.UserId,
                        Status = ActionStatus.Error,
                        Message = ex?.Message ?? ex?.InnerException?.Message
                    };
                }

                dbUser.FKVendorId = dbUser.UserId;
                RemoveORAddUserPermissions(dbUser.UserId, userDetails);

                RemoveOrAddUserPlatforms(dbUser.UserId, userDetails);

                RemoveOrAddUserWidgets(dbUser.UserId, userDetails);

                return new ActionOutput
                {
                    ID = dbUser.UserId,
                    Status = ActionStatus.Successfull,
                    Message = "User Added Successfully, Verification link has been sent on user email account"
                };
            }
        }

        ActionOutput IUserManager.AddAppUserDetails(RegisterAPIModel userDetails)
        {

            var existing_user_by_number = _context.Users.FirstOrDefault(z => z.Phone.Trim().ToLower() == userDetails.Mobile.Trim().ToLower()) ?? null;

            if (existing_user_by_number != null)
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "User with this phone number already exist"
                };
            }
            if (IsEmailUsed(userDetails.Email, 0))
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "This email-id already exists for another user."
                };
            }
            else
            {

                var dbUser = new User();
                dbUser.Name = userDetails.FirstName;
                dbUser.CompanyName = userDetails.CompanyName;
                dbUser.IsCompany = userDetails.IsCompany;
                dbUser.SurName = userDetails.LastName;
                dbUser.Email = userDetails.Email.Trim().ToLower();
                dbUser.Password = Utilities.EncryptPassword(Utilities.GenerateByAnyLength(4));
                dbUser.IsEmailVerified = false;
                dbUser.CreatedAt = DateTime.UtcNow;
                dbUser.UserType = Utilities.GetUserRoleIntValue(UserRoles.Vendor); 
                dbUser.Address = userDetails.Address;
                dbUser.CountryCode = Utilities.GetCountry().CountryCode;
                dbUser.CityId = Convert.ToInt32(userDetails.City != null ? userDetails.City : "0");
                dbUser.Status = (int)UserStatusEnum.Pending;
                dbUser.CountryId = Convert.ToInt16(userDetails.Country);
                dbUser.Phone = userDetails.Mobile;
                dbUser.AgentId = Convert.ToInt64(userDetails.Agency != null ? userDetails.Agency : "0");
                dbUser.Vendor = userDetails.IsCompany ? userDetails.CompanyName : $"{userDetails.FirstName} {userDetails.LastName}"; 
                dbUser.UserSerialNo = _context.Users.Max(d => d.UserSerialNo) + 1;

                _context.Users.Add(dbUser);
                _context.SaveChanges();
                dbUser.FKVendorId = dbUser.UserId;
                _context.SaveChanges();

                return ReturnSuccess(dbUser.UserId, $"Registration Successful !! Confirnmation email sent to  {userDetails.Email}");
            }
        }

        UserModel IUserManager.GetUserDetailsByUserId(long userId)
        {
            try
            {
                if (userId == 0) return new UserModel();
                var user = _context.Users.Where(z => z.UserId == userId).FirstOrDefault();
                user.UserRole = _context.UserRoles.FirstOrDefault(x => x.RoleId == user.UserType);
                if (user == null)
                    return null;
                else
                {
                    var current_user_data = new UserModel(user);
                    current_user_data.SelectedWidgets = user.UserAssignedWidgets.Select(e => e.WidgetId).ToList();
                    return current_user_data;
                }
            }
            catch (Exception)
            {
                return new UserModel();
            }

        }

        string IUserManager.GetUserPasswordbyUserId(long userId)
        {
            if (userId == 0) return string.Empty;
            var user = _context.Users.Where(z => z.UserId == userId).FirstOrDefault();
            if (user == null)
                return string.Empty;
            else
            {
                return Utilities.DecryptPassword(user.Password);
            }
        }

        ActionOutput IUserManager.ChangeUserStatus(long userId, UserStatusEnum status)
        {
            var user = _context.Users.Where(z => z.UserId == userId).FirstOrDefault();
            if (user == null)
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "User Not Exist."
                };
            }
            else
            {
                user.Status = (int)status;
                _context.SaveChanges();
                return new ActionOutput
                {
                    Status = ActionStatus.Successfull,
                    Message = status == UserStatusEnum.Block ? "User Blocked Successfully." : "User Activate Successfully."
                };
            }
        }

        ActionOutput IUserManager.DeclineUser(long userId)
        {
            var user = _context.Users.Where(z => z.UserId == userId).FirstOrDefault();
            if (user == null)
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "User Not Exist."
                };
            }
            else
            {
                user.Status = (int)UserStatusEnum.Declined;
                _context.SaveChanges();
                return new ActionOutput
                {
                    Status = ActionStatus.Successfull,
                    Message = "User Declined Successfully."
                };
            }
        }

        ActionOutput IUserManager.DeleteUser(long userId)
        {
            var user = _context.Users.Where(z => z.UserId == userId).FirstOrDefault();
            if (user == null)
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "User Not Exist."
                };
            }
            else
            {
                user.Status = (int)UserStatusEnum.Deleted;
                _context.SaveChanges();
                return new ActionOutput
                {
                    Status = ActionStatus.Successfull,
                    Message = "User Deleted Successfully."
                };
            }
        }

        decimal IUserManager.GetUserWalletBalance(long userId, long agentId, bool apiCall)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(z => z.UserId == userId);
                if(apiCall && agentId == 0)
                    agentId = _context.Agencies.FirstOrDefault(f => f.Representative == user.UserId)?.AgencyId ?? 0;
                if (user == null)
                    return 0;
                if (agentId > 0)
                {
                    //var posTotalBalance = _context.POS.Where(p => p.User.AgentId == agentId && !p.IsDeleted && p.Enabled != false && !p.SerialNumber.Contains("AGT")).ToList().Sum(p => p.Balance);
                    //return posTotalBalance.Value;

                    var posTotalBalance = _context.POS.Where(p => (p.VendorId != null && p.VendorId == user.FKVendorId) && p.Balance != null && !p.IsDeleted && p.Enabled != false).ToList().Sum(p => p.Balance);
                    return posTotalBalance.Value;
                }

                if (user.UserRole.Role == UserRoles.AppUser || user.UserRole.Role == UserRoles.Vendor) //user.UserRole.Role == UserRoles.Vendor ||
                {
                    var posTotalBalance = _context.POS.Where(p => (p.VendorId != null && p.VendorId == user.FKVendorId) && p.Balance != null && !p.IsDeleted && p.Enabled != false && !p.SerialNumber.Contains("AGT")).ToList().Sum(p => p.Balance);
                    return posTotalBalance.Value;
                }
                else if (user.UserRole.Role != UserRoles.AppUser)
                {
                    var posTotalBalance = _context.POS.ToList().Sum(p => p.Balance);
                    return posTotalBalance.Value;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                return new decimal();
            }
        }

        PendingDeposit IUserManager.GetUserPendingDeposit(long userId)
        {
            try
            {
                return _context.PendingDeposits.FirstOrDefault(d => d.UserId == userId) ?? null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        decimal IUserManager.GetUserWalletBalance(User user, long agentId)
        {
            try
            { 
                if (user == null)
                    return 0;
                if(agentId > 0)
                {
                    var posTotalBalance = _context.POS.Where(p => p.User.AgentId == agentId && !p.IsDeleted && p.Enabled != false && !p.SerialNumber.Contains("AGT")).ToList().Sum(p => p.Balance);
                    return posTotalBalance.Value;
                }
                if (user.UserRole.Role == UserRoles.AppUser || user.UserRole.Role == UserRoles.Vendor) //user.UserRole.Role == UserRoles.Vendor ||
                {
                    var posTotalBalance = _context.POS.Where(p => (p.VendorId != null && p.VendorId == user.FKVendorId) && p.Balance != null && !p.IsDeleted && p.Enabled != false && !p.SerialNumber.Contains("AGT")).ToList().Sum(p => p.Balance);
                    return posTotalBalance.Value;
                }
                else if (user.UserRole.Role != UserRoles.AppUser)
                {
                    var posTotalBalance = _context.POS.Where(p => p.Enabled == true && p.User.Status == (int)UserStatusEnum.Active).ToList().Sum(p => p.Balance);
                    return posTotalBalance.Value;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                return new decimal();
            }
        }

        bool RemoveORAddUserPermissions(long userId, AddUserModel model)
        {
            var existingpermissons = _context.UserAssignedModules.Where(x => x.UserId == userId && x.IsAddedFromAgency == false).ToList();
            if (existingpermissons.Count() > 0)
            {
                _context.UserAssignedModules.RemoveRange(existingpermissons);
                _context.SaveChanges();
            }
            List<UserAssignedModule> newpermissos = new List<UserAssignedModule>();
            if (model.SelectedModules != null)
            {
                model.SelectedModules.ToList().ForEach(c =>
                 newpermissos.Add(new UserAssignedModule()
                 {
                     UserId = userId,
                     ModuleId = c,
                     CreatedAt = DateTime.UtcNow,
                     IsAddedFromAgency = false,
                 }));
                _context.UserAssignedModules.AddRange(newpermissos);
                _context.SaveChanges();
            }
            return true;
        }
        bool RemoveOrAddUserPlatforms(long userId, AddUserModel model)
        {
            var existingPlatforms = _context.UserAssignedPlatforms.Where(x => x.UserId == userId).ToList();
            if (existingPlatforms.Count() > 0)
            {
                _context.UserAssignedPlatforms.RemoveRange(existingPlatforms);
                _context.SaveChanges();
            }

            List<UserAssignedPlatform> newPlatforms = new List<UserAssignedPlatform>();

            if (model.SelectedPlatforms != null)
            {
                model.SelectedPlatforms.ToList().ForEach(c =>
                 newPlatforms.Add(new UserAssignedPlatform()
                 {
                     UserId = userId,
                     PlatformId = c,
                     CreatedAt = DateTime.UtcNow,
                 }));
                _context.UserAssignedPlatforms.AddRange(newPlatforms);
                _context.SaveChanges();
            }
            return true;
        }

        bool RemoveOrAddUserWidgets(long userId, AddUserModel model)
        {
            //Deleting Exisiting Widgets
            var existing_widgets = _context.UserAssignedWidgets.Where(x => x.UserId == userId && x.IsAddedFromAgency == false).ToList();
            if (existing_widgets.Count() > 0)
            {
                _context.UserAssignedWidgets.RemoveRange(existing_widgets);
                _context.SaveChanges();
            }

            List<UserAssignedWidget> newwidgets = new List<UserAssignedWidget>();
            if (model.SelectedWidgets != null)
            {
                model.SelectedWidgets.ToList().ForEach(c =>
                 newwidgets.Add(new UserAssignedWidget()
                 {
                     UserId = userId,
                     WidgetId = c,
                     CreatedAt = DateTime.UtcNow,
                     IsAddedFromAgency = false
                 }));
                _context.UserAssignedWidgets.AddRange(newwidgets);
                _context.SaveChanges();
            }
            return true;
        }


        bool IsEmailUsed(string email, long userId)
        {
            var existngUser = _context.Users.Where(z => z.Email.Trim().ToLower() == email.Trim().ToLower() && z.UserId != userId && z.Status != (int)UserStatusEnum.Deleted  && z.Status != (int)UserStatusEnum.Declined).FirstOrDefault();
            if (existngUser == null)
                return false;
            return true;
        }

        List<User> IUserManager.GetAllAdminUsersByAppUserPermission()
        {
            return _context.Users.Where(p =>
            (UserRoles.AppUser != p.UserRole.Role) && (UserRoles.Vendor != p.UserRole.Role) && (UserRoles.Agent != p.UserRole.Role) &&
            (p.Status == (int)UserStatusEnum.Active) && p.UserAssignedModules.Select(f => f.ModuleId).Contains(11)).ToList(); // 11 is the Module key for appUsers
        }
        //List<User> IUserManager.GetAllAdminUsersByDepositRelease()
        //{
        //    return _context.Users.Where(p =>
        //    (UserRoles.AppUser != p.UserRole.Role) && (UserRoles.Vendor != p.UserRole.Role) && (UserRoles.Agent != p.UserRole.Role) &&
        //    (p.Status == (int)UserStatusEnum.Active) && p.UserAssignedModules.Select(f => f.ModuleId).Contains(7)).ToList(); // 7 is the Module key for Deposit Release
        //}

        UserDetails IUserManager.GetNotificationUsersCount(long currentUserId)
        {
            try
            {
                var notificationDetail = _context.UserAssignedModules.Where(x => x.UserId == currentUserId && (x.ModuleId == 6 || x.ModuleId == 11));
                var modelUser = new UserDetails();
                modelUser.AppUserMessage = notificationDetail.FirstOrDefault(x => x.ModuleId == 11) != null ? "NEW APP USERS APPROVAL" : string.Empty;
                modelUser.DepositReleaseMessage = notificationDetail.FirstOrDefault(x => x.ModuleId == 6) != null ? "NEW DEPOSITS RELEASE" : string.Empty;
                modelUser.RemainingAppUser = !string.IsNullOrEmpty(modelUser.AppUserMessage) ? _context.Users.Where(x => (x.UserRole.Role == UserRoles.AppUser || x.UserRole.Role == UserRoles.Vendor) && x.Status == (int)UserStatusEnum.Pending).Count() : 0;
                modelUser.RemainingDepositRelease = !string.IsNullOrEmpty(modelUser.DepositReleaseMessage) ? _context.PendingDeposits.Where(x => x.Status == (int)DepositPaymentStatusEnum.Pending && x.IsDeleted == false).Count() : 0;

                return modelUser;
            }
            catch (Exception)
            {
                return new UserDetails();
            }
        }

        IEnumerable<UserLiteDto> IUserManager.GetVendorNames_API()
        {
            var result = _context.POS.Where(x => x.User != null).ToList().Select(x => new UserLiteDto
            {
                VendorId = Convert.ToInt64(x.VendorId),
                VendorName = x.User?.Vendor
            });
            return result;
        }
        UserLiteDto IUserManager.GetVendorNamePOSNumber(int posId)
        {
            var result = new UserLiteDto();
            try
            {
                result = _context.POS.Where(p => p.POSId == posId).Select(x => new UserLiteDto
                {
                    POSId = x.POSId,
                    VendorName = x.User.Vendor
                }).FirstOrDefault();
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        UserLogo IUserManager.GetUserLogo(long userId)
        {
            return new UserLogo { Image = _context.Users.FirstOrDefault(d => d.UserId == userId)?.ProfilePic };
        }

        async Task<List<string>> IUserManager.GetActiveUsersDeviceTokens(int pageNo, int pageSize)
        {
            return await _context.Users.Where(d => !string.IsNullOrEmpty(d.DeviceToken) && d.POS.Count > 0).Select(d => d.DeviceToken).ToListAsync();
        }

    }


}
