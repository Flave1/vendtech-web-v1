using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using System;
using System.Linq;
using System.Linq.Dynamic;
using VendTech.DAL;
using System.Data.Entity.Validation;

namespace VendTech.BLL.Managers
{
    public class EmailTemplateManager : BaseManager, IEmailTemplateManager
    {
        private readonly VendtechEntities _context;

        public EmailTemplateManager(VendtechEntities context)
        {
            _context = context;
        }

        PagingResult<TemplateViewModel> IEmailTemplateManager.GetEmailTemplateList(PagingModel model)
        {
            var result = new PagingResult<TemplateViewModel>();
            var query = _context.EmailTemplates.Where(s => s.IsActive == true).OrderBy(model.SortBy + " " + model.SortOrder);
            if (!string.IsNullOrEmpty(model.Search))
            {
                query = query.Where(z => z.TemplateName.Contains(model.Search));
            }
            var list = query
               .Skip(model.PageNo - 1)
               .ToList().Select(x => new TemplateViewModel(x)).ToList();
            result.List = list;
            result.Status = ActionStatus.Successfull;
            result.Message = "Template List";
            result.TotalCount = query.Count();
            return result;
        }


        ActionOutput IEmailTemplateManager.AddUpdateEmailTemplate(AddEditEmailTemplateModel templateModel)
        {
            var existingTemplate = _context.EmailTemplates.FirstOrDefault(z => z.TemplateId == templateModel.TemplateId);
            if (existingTemplate == null)
            {
                _context.EmailTemplates.Add(new EmailTemplate
                {
                    TemplateName = templateModel.TemplateName,
                    EmailSubject = templateModel.EmailSubject,
                    TemplateContent = templateModel.TemplateContent,
                    TemplateStatus = templateModel.TemplateStatus,
                    CreatedOn = DateTime.UtcNow,
                    TemplateType = templateModel.TemplateType,
                });
                _context.SaveChanges();
                return new ActionOutput
                {
                    Status = ActionStatus.Successfull,
                    Message = "Template Added Sucessfully."
                };
            }
            else
            {
                existingTemplate.EmailSubject = templateModel.EmailSubject;
                existingTemplate.TemplateContent = templateModel.TemplateContent;
                existingTemplate.TemplateStatus = templateModel.TemplateStatus;
                existingTemplate.UpdatedOn = DateTime.UtcNow;
                existingTemplate.TemplateType = templateModel.TemplateType;
                if(templateModel.Receivers != null && templateModel.Receivers.Count > 0)
                    existingTemplate.Receiver = string.Join(",", templateModel.Receivers);
                else
                    existingTemplate.Receiver = string.Empty;


                try
                {
                    _context.SaveChanges();
                }

                catch (DbEntityValidationException dbEx)
                {
                    Exception raise = dbEx;
                    foreach (var validationErrors in dbEx.EntityValidationErrors)
                    {
                        foreach (var validationError in validationErrors.ValidationErrors)
                        {
                            string message = string.Format("{0}:{1}",
                                validationErrors.Entry.Entity.ToString(),
                                validationError.ErrorMessage);
                            // raise a new exception nesting
                            // the current instance as InnerException
                            raise = new InvalidOperationException(message, raise);
                        }
                    }
                    throw raise;
                }
                catch (Exception ec)
                {

                }
                return new ActionOutput
                {
                    Status = ActionStatus.Successfull,
                    Message = "Template Updated Sucessfully."
                };
            }
        }


        TemplateViewModel IEmailTemplateManager.GetEmailTemplateByTemplateType(TemplateTypes type)
        {
            var template = _context.EmailTemplates.Where(z => z.TemplateType == (int)type).FirstOrDefault();
            if (template == null)
                return null;
            else
                return new TemplateViewModel(template);
        }

        AddEditEmailTemplateModel IEmailTemplateManager.GetEmailTemplateByTemplateId(int templateId)
        {
            var existingTemplate = _context.EmailTemplates.FirstOrDefault(z => z.TemplateId == templateId);
            if (existingTemplate != null)
                return new AddEditEmailTemplateModel(existingTemplate);
            else
                return null;
        }

    }
}
