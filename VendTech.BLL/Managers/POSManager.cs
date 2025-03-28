﻿using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic;
using VendTech.DAL;
using System.Web.Mvc;
using static VendTech.BLL.Jobs.BalanceLowSheduleJob;
using System.Data.Entity;
using System.Threading.Tasks;

namespace VendTech.BLL.Managers
{
    public class POSManager : BaseManager, IPOSManager
    {
        private readonly VendtechEntities _context;
        public POSManager(VendtechEntities context)
        {
            _context = context;
        }

        KeyValuePair<string, string> IPOSManager.GetVendorDetail(long posId)
        {
            var pos = _context.POS.FirstOrDefault(d => d.POSId == posId);
            if(pos != null)
            {
                return new KeyValuePair<string, string>(pos.SerialNumber, pos.User.Vendor);
            }
            return new KeyValuePair<string, string>();
        }
        PagingResult<POSListingModel> IPOSManager.GetPOSPagedList(PagingModel model, long agentId, long vendorId, bool callForGetVendorPos)
        {
            //model.RecordsPerPage = 1000000;
            var result = new PagingResult<POSListingModel>();
            IQueryable<POS> query = _context.POS.Where(p => !p.IsDeleted);


            if (model.SortBy == null)
            {
                model.SortBy = "SerialNumber";
            }
            else if (model.SortBy == "VendorId")
            {
                if (model.SortOrder == "Asc")
                {
                    query = query.Where(p => !p.IsDeleted).OrderBy(s => s.User.Vendor);
                }
                else if (model.SortOrder == "Desc")
                {
                    query = query.Where(p => !p.IsDeleted).OrderByDescending(s => s.User.Vendor);
                }
            }
            else if (model.SortBy == "COMMISSION")
            {
                query = query.OrderBy(r => r.Commission.Percentage + " " + model.SortOrder);
            }
            else if (model.SortBy == "Number")
            {
                query = query.Where(p => !p.IsDeleted).OrderBy("Phone" + " " + model.SortOrder);
            }
            else if (model.SortBy == "Balance")
            {
                query = query.Where(p => !p.IsDeleted).OrderBy("Balance" + " " + model.SortOrder);
            }
            else if (model.SortBy == "POSId")
            {
                query = query.Where(p => !p.IsDeleted).OrderBy("SerialNumber" + " " + model.SortOrder);
            }
            else if (model.SortBy == "Agency")
            {
                query = query.Where(p => !p.IsDeleted).OrderBy("User.Agency.AgencyName" + " " + model.SortOrder);
            }
            else if (model.SortBy == "appVersion")
            {
                query = query.Where(p => !p.IsDeleted).OrderBy("User.MobileAppVersion" + " " + model.SortOrder);
            }
            if (vendorId > 0)
            {
                var user = _context.Users.FirstOrDefault(p => p.UserId == vendorId);
                if (callForGetVendorPos)
                    query = query.Where(p => p.VendorId == user.UserId);
                else
                    query = query.Where(p => p.VendorId != null && p.VendorId == user.FKVendorId);
            }
            if (!string.IsNullOrEmpty(model.Search) && !string.IsNullOrEmpty(model.SearchField))
            {
                if (model.SearchField.Equals("POS"))
                    query = query.Where(z => z.SerialNumber.ToLower().Contains(model.Search.ToLower()));

                if (model.SearchField.Equals("AGENCY"))
                    query = query.Where(z => z.User.Agency.AgencyName.ToLower().Contains(model.Search.ToLower()));
                if (model.SearchField.Equals("PRODUCT"))
                    query = query.Where(z => z.POSAssignedPlatforms.Select(d =>d.Platform.Title).Contains(model.Search.ToLower()));

                if (model.SearchField.Equals("COMMISSION"))
                    query = query.Where(z => z.Commission.Percentage.ToString().ToLower().Contains(model.Search.ToLower()));
                else if (model.SearchField.Equals("VENDOR"))
                    query = query.Where(z => z.User.Vendor.ToLower().Contains(model.Search.ToLower()));
                else if (model.SearchField.Equals("POS_SIM"))
                    query = query.Where(z => z.Phone.Contains(model.Search)); 
                else if (model.SearchField.Equals("ENABLED"))
                    query = query.Where(z => z.Enabled.ToString().ToLower().Contains(model.Search.ToLower()));
            }
            var list = query.Where(r => r.Enabled == model.IsActive)//.Take(model.RecordsPerPage)
               .ToList().Select(x => new POSListingModel(x)).ToList();

            if (model.SortBy == "Product")
            {
                if (model.SortOrder == "Asc")
                    list = list.OrderBy(d => d.Products).ToList();
                else if (model.SortOrder == "Desc")
                    list = list.OrderByDescending(d => d.Products).ToList();
            }
            else if (model.SortBy == "MeterCount")
            {
                if (model.SortOrder == "Asc")
                {
                    list = list.OrderBy(d => d.POSCount).ToList();
                }
                else if (model.SortOrder == "Desc")
                {
                    list = list.OrderBy(d => d.POSCount).ToList();
                }
            }

            if (!string.IsNullOrEmpty(model.Search) && !string.IsNullOrEmpty(model.SearchField))
            {
                if (model.SearchField.Equals("POS_TYPE"))
                    list = list.Where(z => z.VendorType.ToLower().Contains(model.Search.ToLower())).ToList();
            }


            result.List = list;
            result.Status = ActionStatus.Successfull;
            result.Message = "POS List";
            result.TotalCount = query.Count();
            return result;
        }
        PagingResult<POSListingModel> IPOSManager.GetUserPosPagingListForApp(int pageNo, int pageSize, long userId)
        {
            var result = new PagingResult<POSListingModel>();
            var query = _context.POS.Where(p => !p.IsDeleted).OrderBy("SerialNumber" + " " + "Asc").AsEnumerable();
            //if (agentId > 0)
            //    query = query.Where(p => p.Vendor.AgencyId == agentId);
            if (userId > 0)
            {
                var user = _context.Users.FirstOrDefault(p => p.UserId == userId);
                query = query.Where(p => p.VendorId != null && p.VendorId == user.FKVendorId);
            }

            var list = query
               .Skip((pageNo - 1) * pageSize).Take(pageSize)
               .ToList().Select(x => new POSListingModel(x)).ToList();
            result.List = list;
            result.Status = ActionStatus.Successfull;
            result.Message = "POS List Fetched Successfully";
            result.TotalCount = Convert.ToInt32(list.Sum(x => x.Balance));
            return result;
        }
        List<PosAPiListingModel> IPOSManager.GetPOSSelectListForApi(long userId)
        {
            var query = _context.POS.Where(p => !p.IsDeleted && p.Enabled != false);
            // enable check has been removed
            //   var query = _context.POS.Where(p => !p.IsDeleted);
            var userPos = new List<POS>();
            if (userId > 0)
            {
                var user = _context.Users.FirstOrDefault(p => p.UserId == userId);

                query = query.Where(p => (p.VendorId != null && p.VendorId == user.FKVendorId));
            }
            return query.ToList().OrderBy(p => p.SerialNumber).Select(p => new PosAPiListingModel(p)
           ).ToList();
        }
        List<PosSelectItem> IPOSManager.GetVendorPos(long userId)
        {
            var query = _context.POS.Where(p => !p.IsDeleted && p.Enabled != false && !p.SerialNumber.StartsWith("AGT"));
            // var query = _context.POS.Where(p => !p.IsDeleted);
            var userPos = new List<POS>();
            if (userId > 0)
                query = query.Where(p => (p.VendorId != null && p.VendorId == userId));
            var list = query.ToList();
            return list.OrderBy(p => p.SerialNumber).Select(p => new PosSelectItem
            {
                Text = p.SerialNumber.ToUpper(),
                Value = p.POSId.ToString(),
                Percentage = p.CommissionPercentage
            }).ToList();
        }

        List<PosSelectItem> IPOSManager.GetAgencyPos(long userId)
        {
            var query = _context.POS.Where(p => !p.IsDeleted && p.Enabled != false && !p.SerialNumber.StartsWith("AGT"));
            // var query = _context.POS.Where(p => !p.IsDeleted);
            var userPos = new List<POS>();
            if (userId > 0)
                query = query.Where(p => (p.User != null && p.User.AgentId == userId));
            var list = query.ToList();
            return list.OrderBy(p => p.SerialNumber).Select(p => new PosSelectItem
            {
                Text = p.SerialNumber.ToUpper(),
                Value = p.POSId.ToString(),
                Percentage = p.CommissionPercentage
            }).ToList();
        }


        List<SelectListItem> IPOSManager.GetPOSSelectList(long userId, long agentId)
        {
            var query = new List<POS>();
            var userPos = new List<POS>();
            if (userId > 0)
            {
                var user = _context.Users.FirstOrDefault(p => p.UserId == userId);
                if (user != null)
                    query = _context.POS.Where(p => (p.VendorId != null && p.VendorId == user.FKVendorId && p.Enabled != false && !p.IsDeleted && !p.SerialNumber.StartsWith("AGT")) && !p.IsAdmin).ToList();
            }else
                query = _context.POS.Where(p => !p.IsDeleted && p.Enabled != false && !p.IsAdmin && !p.SerialNumber.StartsWith("AGT")).ToList();
             

            if (agentId > 0)
            {
                query = _context.POS.Where(p => p.User.AgentId == agentId && p.Enabled != false && !p.IsDeleted && !p.IsAdmin && !p.SerialNumber.StartsWith("AGT")).ToList();
            }
            return query.OrderBy(p => p.SerialNumber).Select(p => new SelectListItem
            {
                Text = p.SerialNumber.ToUpper(),
                Value = p.POSId.ToString()
            }).ToList();
        }

        List<SelectListItem> IPOSManager.GetPOSWithNameSelectList(long userId, long agentId, bool includeAdminPos)
        {
            IQueryable<POS> query = null;
            if (userId > 0)
            {
                var user = _context.Users.FirstOrDefault(p => p.UserId == userId);
                if (user != null)
                    query = _context.POS.Where(p => (p.VendorId != null && p.VendorId == user.FKVendorId 
                    && p.Enabled != false && !p.IsDeleted ) && !p.IsAdmin);
            }
            else
                query = _context.POS.Where(p => !p.IsDeleted && p.Enabled != false 
                && !p.IsAdmin);

            if (agentId > 0)
            {
                query = _context.POS.Where(p => p.User.AgentId == agentId && p.Enabled != false && !p.IsDeleted && !p.IsAdmin);
            }

            if (!includeAdminPos)
            {
                query = query.Where(p => !p.SerialNumber.StartsWith("AGT"));
            }
            return query.ToList().OrderBy(p => p.SerialNumber).Select(p => new SelectListItem
            {
                Text = p.User.Vendor + " - " + p.SerialNumber.ToUpper(),
                Value = p.POSId.ToString()
            }).ToList();
        }

        SavePosModel IPOSManager.GetPosDetail(long posId)
        {
            var dbPos = _context.POS.FirstOrDefault(p => p.POSId == posId);
            if(dbPos != null)
            {
                var user = new User();
                user = _context.Users.Where(x => x.FKVendorId == dbPos.VendorId && x.Status == (int)UserStatusEnum.Active).FirstOrDefault();
                if (user == null)
                {
                    user = _context.Users.FirstOrDefault(x => x.UserId == dbPos.VendorId);
                }
                var email = user.Email;
                if (dbPos == null)
                    return null;
                return new SavePosModel()
                {
                    SerialNumber = dbPos.SerialNumber,
                    VendorId = dbPos?.VendorId,
                    Phone = dbPos?.User?.Phone,
                    Type = dbPos.VendorType.ToString(),
                    POSId = dbPos.POSId,
                    Percentage = dbPos.CommissionPercentage,
                    Enabled = dbPos.Enabled == null ? false : dbPos.Enabled.Value,
                    SMSNotificationDeposit = dbPos.SMSNotificationDeposit == null ? false : dbPos.SMSNotificationDeposit.Value,
                    EmailNotificationDeposit = dbPos.EmailNotificationDeposit == null ? false : dbPos.EmailNotificationDeposit.Value,
                    EmailNotificationSales = dbPos.EmailNotificationSales == null ? false : dbPos.EmailNotificationSales.Value,
                    SMSNotificationSales = dbPos.SMSNotificationSales == null ? false : dbPos.SMSNotificationSales.Value,
                    //CountryCode = dbPos.CountryCode,
                    PassCode = dbPos.PassCode,
                    Email = email,
                    WebSms = dbPos?.WebSms ?? false,
                    PosSms = dbPos?.PosSms ?? false,
                    PosPrint = dbPos?.PosPrint ?? false,
                    WebPrint = dbPos?.WebPrint ?? false,
                    WebBarcode = dbPos?.WebBarcode ?? false,
                    PosBarcode = dbPos?.PosBarcode ?? false,
                };
            }
            return new SavePosModel();
        }
        POS IPOSManager.GetSinglePos(long pos)
        {
            return _context.POS.Find(pos);
        }
        POS IPOSManager.GetVendorPos2(long vendorId)
        {
            return _context.POS.FirstOrDefault(s => s.VendorId == vendorId);
        }
        SavePosModel IPOSManager.GetPosDetails(string passCode)
        {
            var dbPos = _context.POS.FirstOrDefault(p => p.PassCode == passCode);

            if (dbPos == null)
                return null;
            return new SavePosModel()
            {
                SerialNumber = dbPos.SerialNumber,
                VendorId = dbPos.VendorId,
                Phone = dbPos.Phone,
                Type = dbPos.VendorType.ToString(),
                POSId = dbPos.POSId,
                Percentage = dbPos.CommissionPercentage,
                Enabled = dbPos.Enabled == null ? false : dbPos.Enabled.Value,
                SMSNotificationDeposit = dbPos.SMSNotificationDeposit == null ? false : dbPos.SMSNotificationDeposit.Value,
                EmailNotificationDeposit = dbPos.EmailNotificationDeposit == null ? false : dbPos.EmailNotificationDeposit.Value,
                EmailNotificationSales = dbPos.EmailNotificationSales == null ? false : dbPos.EmailNotificationSales.Value,
                SMSNotificationSales = dbPos.SMSNotificationSales == null ? false : dbPos.SMSNotificationSales.Value,
                //CountryCode = dbPos.CountryCode,
                PassCode = dbPos.PassCode,
                Email = _context.Users.FirstOrDefault(x => x.UserId == dbPos.VendorId).Email,
                WebSms = dbPos?.WebSms ?? false,
                PosSms = dbPos?.PosSms ?? false,
                PosPrint = dbPos?.PosPrint ?? false,
                WebPrint = dbPos?.WebPrint ?? false,
                WebBarcode = dbPos?.WebBarcode ?? false,
                PosBarcode = dbPos?.PosBarcode ?? false,
            };
        }

        UserModel IPOSManager.GetUserPosDetails(string posSerialNumber)
        {
            var dbPos = _context.POS.FirstOrDefault(p => p.SerialNumber == posSerialNumber);

            if (dbPos == null)
                return null;
            var user = _context.Users.FirstOrDefault(x => x.UserId == dbPos.VendorId);
            return new UserModel()
            {
                Email = user.Email,
                UserId = user.UserId,
                FirstName = user.Name,
                Phone = user.Phone
            };
        }

        UserModel IPOSManager.GetUserPosDetailApi(string posSerialNumber)
        {
            var dbPos = _context.POS.FirstOrDefault(p => p.SerialNumber == posSerialNumber);
            var user = new User();
            if (dbPos == null)
                return null;
            user = _context.Users.FirstOrDefault(x => x.FKVendorId == dbPos.VendorId && x.Status == (int)UserStatusEnum.Active);
            if (user == null)
            {
                user = _context.Users.FirstOrDefault(x => x.UserId == dbPos.VendorId);
            }
            return new UserModel()
            {
                Email = user.Email,
                UserId = user.UserId,
                FirstName = user.Name,
                Phone = user.Phone
            };
        }

        ActionOutput IPOSManager.DeletePos(long posId)
        {
            var dbPos = _context.POS.FirstOrDefault(p => p.POSId == posId);
            if (dbPos == null)
                return ReturnError("POS not exist");
            dbPos.IsDeleted = true;
            dbPos.User.Status = (int)UserStatusEnum.Block;
            //EnableOrdisablePOSAccount(false, dbPos.POSId);
            _context.SaveChanges();
            return ReturnSuccess("POS DELETED SUCCESSFULLY.");
        }

        ActionOutput IPOSManager.ChangePOSStatus(int posId, bool value)
        {
            var pos = _context.POS.Where(z => z.POSId == posId).FirstOrDefault();
            if (pos == null)
            {
                return new ActionOutput
                {
                    Status = ActionStatus.Error,
                    Message = "POS Not Exist."
                };
            }
            else
            {
                pos.Enabled = value;
                if (value)
                    pos.User.Status = (int)UserStatusEnum.Active;
                else
                    pos.User.Status = (int)UserStatusEnum.Block;

                //EnableOrdisablePOSAccount(value, pos.POSId);
                _context.SaveChanges();
                return new ActionOutput
                {
                    Status = ActionStatus.Successfull,
                    Message = "POS status changed Successfully."
                };
            }
        }

        decimal IPOSManager.GetPosCommissionPercentage(long posId)
        {
            var dbPos = _context.POS.FirstOrDefault(p => p.POSId == posId);
            if (dbPos == null || dbPos.CommissionPercentage == null)
                return 0;
            return dbPos.Commission.Percentage;
        }
        decimal IPOSManager.GetPosBalance(long posId)
        {
            var dbPos = _context.POS.FirstOrDefault(p => p.POSId == posId && !p.SerialNumber.StartsWith("AGT"));
            if (dbPos == null || dbPos.Balance == null)
                return 0;
            return dbPos.Balance.Value;
        }
        decimal IPOSManager.GetPosPercentage(long posId)
        {
            var dbPos = _context.POS.FirstOrDefault(p => p.POSId == posId);
            if (dbPos == null)
                return 0;
            return dbPos.Commission.Percentage;
        }
        decimal IPOSManager.GetPosCommissionPercentageByUserId(long userId)
        {
            var userObj = _context.Users.FirstOrDefault(p => p.UserId == userId);
            var userAssignedPos = new POS();
            if (userObj.UserRole.Role == UserRoles.Vendor)
                userAssignedPos = userObj.POS.FirstOrDefault();
            else if (userObj.UserRole.Role == UserRoles.AppUser && userObj.User1 != null)
                userAssignedPos = userObj.User1.POS.FirstOrDefault();

            if (userAssignedPos != null && userAssignedPos.Commission != null)
                return userAssignedPos.Commission.Percentage;
            return 0;
        }

        IList<PlatformCheckbox> IPOSManager.GetAllPlatforms(long posId)
        {
            IList<PlatformCheckbox> chekboxListOfModules = null;
            IList<Platform> modules = _context.Platforms.Where(p => !p.IsDeleted && p.Enabled && !p.DisablePlatform).ToList();
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

                if (posId > 0)
                {
                    var existingPermissons = _context.POSAssignedPlatforms.Where(x => x.POSId == posId).ToList();
                    if (existingPermissons.Count > 0)
                    {
                        chekboxListOfModules.ToList().ForEach(x => x.Checked = existingPermissons.Where(z => z.PlatformId == x.Id).Any());
                    }
                }
                else
                {
                    var selectedIds = new HashSet<int> { 19, 18, 9, 16, 17 };
                    chekboxListOfModules.ToList().ForEach(d =>
                    {
                        d.Checked = true;
                    });
                }
            }
            return chekboxListOfModules;
        }
        ActionOutput IPOSManager.SavePos(SavePosModel model)
        {
            var dbPos = new POS();
            if (model.POSId > 0)
            {
                dbPos = _context.POS.FirstOrDefault(p => p.POSId == model.POSId);
                if (dbPos == null)
                    return ReturnError("Pos not exist");
            }
            else
            {
                if(_context.POS.Any(d => d.SerialNumber.Contains(model.SerialNumber)))
                {
                    return ReturnError("POS with same ID already exist");
                }
            }
            dbPos.SerialNumber = model.SerialNumber;
            dbPos.VendorId = model.VendorId != null ? model.VendorId : dbPos.VendorId;
            if(dbPos.VendorId.HasValue && dbPos.VendorId > 0)
            {
                dbPos.User = _context.Users.FirstOrDefault(d => d.UserId == dbPos.VendorId.Value);
            }
            dbPos.VendorType = Convert.ToInt16(model.Type);
            dbPos.Phone = model.Phone;
            dbPos.Enabled = model.Enabled;
            dbPos.SMSNotificationDeposit = model.SMSNotificationDeposit;
            dbPos.SMSNotificationSales = model.SMSNotificationSales;
            dbPos.EmailNotificationSales = model.EmailNotificationSales;
            dbPos.EmailNotificationDeposit = model.EmailNotificationDeposit;
            //dbPos.CountryCode = model.CountryCode;
            dbPos.CreatedAt = DateTime.UtcNow;
            dbPos.CommissionPercentage = model.Percentage;
            dbPos.IsDeleted = false;
            dbPos.WebSms = model.WebSms;
            dbPos.PosSms = model.PosSms;
            dbPos.PosPrint = model.PosPrint;
            dbPos.WebPrint = model.WebPrint;
            dbPos.WebBarcode = model.WebBarcode;
            dbPos.PosBarcode = model.PosBarcode;
            if (model.Enabled)
                dbPos.User.Status = (int)UserStatusEnum.Active;
            else
                dbPos.User.Status = (int)UserStatusEnum.Block;


            if (model.POSId == 0)
                _context.POS.Add(dbPos);
            _context.SaveChanges();
            //EnableOrdisablePOSAccount(model.Enabled, model.POSId);
            //Deleting Exisiting Platforms
            var existingPlatforms = _context.POSAssignedPlatforms.Where(x => x.POSId == dbPos.POSId).ToList();
            if (existingPlatforms.Count > 0)
            {
                _context.POSAssignedPlatforms.RemoveRange(existingPlatforms);
                _context.SaveChanges();
            }
            List<POSAssignedPlatform> newPlatforms = new List<POSAssignedPlatform>();

            if (model.SelectedPlatforms != null)
            {
                model.SelectedPlatforms.ToList().ForEach(c =>
                 newPlatforms.Add(new POSAssignedPlatform()
                 {
                     POSId = dbPos.POSId,
                     PlatformId = c,
                     CreatedAt = DateTime.UtcNow,
                 }));
                _context.POSAssignedPlatforms.AddRange(newPlatforms);
                _context.SaveChanges();
            }
            return ReturnSuccess("POS SAVED SUCCESSFULLY.");
        }

        
        void EnableOrdisablePOSAccount(bool isEnabled, long posId)
        {
            var pos = _context.POS.Where(z => z.POSId == posId).FirstOrDefault();
            if (pos != null)
            {
                var posUserAccount = pos.User;
                if (posUserAccount != null && !isEnabled)
                    posUserAccount.Status = (int)UserStatusEnum.Block;
                else if (posUserAccount != null && isEnabled)
                    posUserAccount.Status = (int)UserStatusEnum.Active;
            }
        }
        ActionOutput IPOSManager.SavePasscodePos(SavePassCodeModel savePassCodeModel)
        {
            var dbPos = new POS();
            if (savePassCodeModel.POSId > 0)
            {
                dbPos = _context.POS.FirstOrDefault(p => p.POSId == savePassCodeModel.POSId);
                if (dbPos == null)
                    return ReturnError("Pos does not exist");
            }
            else if (!string.IsNullOrEmpty(savePassCodeModel.Phone))
            {
                dbPos = _context.POS.FirstOrDefault(p => p.Phone == savePassCodeModel.Phone);
                if (dbPos == null)
                    return ReturnError("Pos does not exist");
            }
            //dbPos.CountryCode = !string.IsNullOrEmpty(savePassCodeModel.CountryCode) ? savePassCodeModel.CountryCode : dbPos.CountryCode;
            dbPos.PassCode = savePassCodeModel.PassCode;
            dbPos.IsNewPasscode = true;
            if (savePassCodeModel.POSId == 0)
                _context.POS.Add(dbPos);
            _context.SaveChanges();
            return ReturnSuccess("PASSCODE SENT SUCCESSFULLY.");
        }

        void IPOSManager.UpdatePasscode(long VendorId)
        {
            var pos = _context.POS.FirstOrDefault(d => d.VendorId ==  VendorId);
            if(pos == null) return;
            pos.IsNewPasscode = false;
            _context.SaveChanges();
        }

        ActionOutput IPOSManager.SavePasscodePosApi(SavePassCodeModel savePassCodeModel)
        {
            var dbPos = new POS();
            if (!string.IsNullOrEmpty(savePassCodeModel.PosNumber))
            {
                dbPos = _context.POS.FirstOrDefault(p => p.SerialNumber == savePassCodeModel.PosNumber);
                if (dbPos == null)
                    return ReturnError("Pos does not exist");
            }
            else if (!string.IsNullOrEmpty(savePassCodeModel.Phone))
            {
                dbPos = _context.POS.FirstOrDefault(p => p.Phone == savePassCodeModel.Phone);
                if (dbPos == null)
                    return ReturnError("Pos does not exist");
            }
            //dbPos.CountryCode = !string.IsNullOrEmpty(savePassCodeModel.CountryCode) ? savePassCodeModel.CountryCode : dbPos.CountryCode;
            dbPos.PassCode = savePassCodeModel.PassCode;
            if (dbPos.POSId == 0)
                _context.POS.Add(dbPos);
            _context.SaveChanges();
            return ReturnSuccess("PASSCODE SAVED SUCCESSFULLY.");
        }

        POS IPOSManager.ReturnAgencyAdminPOS(long userId)
        {
            return _context.POS.FirstOrDefault(e => e.VendorId == userId);
        }

        PagingResultWithDefaultAmount<BalanceSheetListingModel2> IPOSManager.CalculateBalancesheet(List<BalanceSheetListingModel> result)
        {
            var balanceSheet = new PagingResultWithDefaultAmount<BalanceSheetListingModel2>();
            balanceSheet.List = new List<BalanceSheetListingModel2>();
            decimal openingBalance = 0;

            foreach (var item in result)
            {
                if (openingBalance == 0)
                    openingBalance = item.BalanceBefore.Value;

                balanceSheet.List.Add(new BalanceSheetListingModel2(item));
            }

            balanceSheet.Amount = BLL.Common.Utilities.FormatAmount(openingBalance);
            return balanceSheet;
        }

        List<UserScheduleDTO> IPOSManager.GetAllUserRunningLow()
        {
            decimal commmission1 = Convert.ToDecimal(0.00);
            decimal commmission2 = Convert.ToDecimal(0.50);
            return _context.POS.Where(d => d.User != null 
            && (d.Commission.Percentage == commmission1 
            ||  d.Commission.Percentage == commmission2)
            && d.Balance < 1 && d.User.Status == (int)UserStatusEnum.Active
            ).AsEnumerable().Select(e => new UserScheduleDTO
            {
                CreatedAt = DateTime.UtcNow,
                ScheduleType = (int)UserScheduleTypes.LowBalnce,
                Status = (int)UserScheduleStatus.NotSent,
                UserId = e.User.UserId,
                Balance = e.Balance.ToString(),
            }).ToList();
        }

        bool IPOSManager.BalanceLowMessageIsSent(long userId, UserScheduleTypes type) => 
            _context.UserSchedules.Any(d => d.UserId == userId && d.ScheduleType == (int)type && d.Status == (int)UserScheduleStatus.NotSent);

        void IPOSManager.SaveUserSchedule(long userId, string balance)
        {
            var userSchedule = new UserSchedule
            {
                CreatedAt = DateTime.UtcNow,
                ScheduleType = (int)UserScheduleTypes.LowBalnce,
                Status = (int)UserScheduleStatus.NotSent,
                UserId = userId,
                Balance = balance
            };
            _context.UserSchedules.Add(userSchedule);
            _context.SaveChanges();
        }

        void IPOSManager.RemoveFromSchedule(long userId)
        {
            var schedule = _context.UserSchedules.FirstOrDefault(d => d.UserId == userId && d.ScheduleType == (int)UserScheduleTypes.LowBalnce);
            _context.UserSchedules.Remove(schedule);
            _context.SaveChanges();
        }

        bool IPOSManager.BalanceLowScheduleExist(long userId, UserScheduleTypes type) =>
            _context.UserSchedules.Any(d => d.UserId == userId && d.ScheduleType == (int)type);

        List<UserScheduleDTO> IPOSManager.GetUserSchedule()
        {
            return _context.UserSchedules.Where(d =>  d.ScheduleType == (int)UserScheduleTypes.LowBalnce)
                .AsEnumerable().Select(e => new UserScheduleDTO
            {
                CreatedAt = e.CreatedAt,
                ScheduleType = (int)UserScheduleTypes.LowBalnce,
                Status = e.Status,
                UserId = e.UserId
            }).ToList();
        }

        void IPOSManager.UpdateUserSchedule(long userId, UserScheduleStatus status)
        {
            var schedule = _context.UserSchedules.FirstOrDefault(d => d.UserId == userId && d.ScheduleType == (int)UserScheduleTypes.LowBalnce);
            schedule.Status = (int)status;
            _context.SaveChanges();
        }

        bool IPOSManager.IsWalletFunded(long userId) =>
           _context.POS.FirstOrDefault(d => d.VendorId == userId && d.Balance != null)?.Balance.Value > 1;

        async Task<TransactionDetail> IPOSManager.DeductBalanceAsync(long posId, TransactionDetail trans)
        {
            if (trans.TransactionDetailsId <= 0)
                throw new ArgumentException("TransactionDetailsId Required.");
            using (var ctx = new VendtechEntities())
            {
                if(trans.PaymentStatus == (int)PaymentStatus.Pending || trans.PaymentStatus == (int)PaymentStatus.Refunded)
                {
                    var currentBalance = await ctx.POS
                        .Where(p => p.POSId == posId)
                        .Select(p => p.Balance)
                        .FirstOrDefaultAsync();

                    decimal newBalance = (currentBalance ?? 0) - trans.Amount;
                    trans.BalanceBefore = currentBalance ?? 0;
                    trans.CurrentVendorBalance = newBalance;
                    trans.PaymentStatus = (int)PaymentStatus.Deducted;

                    string updatePosSql = "UPDATE POS SET Balance = @p0 WHERE POSID = @p1";
                    await ctx.Database.ExecuteSqlCommandAsync(updatePosSql, newBalance, posId);

                    string updateTransactionSql = @"UPDATE TransactionDetails 
                    SET BalanceBefore = @p1, CurrentVendorBalance = @p2, PaymentStatus = @p3
                    WHERE TransactionDetailsId = @p0";

                    await ctx.Database.ExecuteSqlCommandAsync(updateTransactionSql, trans.TransactionDetailsId, trans.BalanceBefore, trans.CurrentVendorBalance, trans.PaymentStatus);
                }
            }
            return await _context.TransactionDetails.FindAsync(trans.TransactionDetailsId);
        }

        async Task<TransactionDetail> IPOSManager.RefundDeductedBalanceAsync(long posId, TransactionDetail trans)
        {
            if (trans.TransactionDetailsId <= 0)
                throw new ArgumentException("TransactionDetailsId Required.");
            using (var ctx = new VendtechEntities())
            {
                if (trans.PaymentStatus == (int)PaymentStatus.Deducted)
                {
                    var currentBalance = await ctx.POS
                        .Where(p => p.POSId == posId)
                        .Select(p => p.Balance)
                        .FirstOrDefaultAsync();

                    decimal posBalance = currentBalance.Value + trans.Amount;
                    trans.CurrentVendorBalance = trans.BalanceBefore;
                    trans.PaymentStatus = (int)PaymentStatus.Refunded;

                    string updatePosSql = "UPDATE POS SET Balance = @p0 WHERE POSID = @p1";
                    await ctx.Database.ExecuteSqlCommandAsync(updatePosSql, posBalance, posId);

                    string updateTransactionSql = @"UPDATE TransactionDetails 
                    SET CurrentVendorBalance = @p1, PaymentStatus = @p2
                    WHERE TransactionDetailsId = @p0";

                    await ctx.Database.ExecuteSqlCommandAsync(updateTransactionSql, trans.TransactionDetailsId, trans.CurrentVendorBalance, trans.PaymentStatus);
                }
            }
            return await _context.TransactionDetails.FindAsync(trans.TransactionDetailsId);
        }
    }
}