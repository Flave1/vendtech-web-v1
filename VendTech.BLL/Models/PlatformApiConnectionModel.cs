﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using VendTech.BLL.Interfaces;
using VendTech.DAL;

namespace VendTech.BLL.Models
{
    public class PlatformApiConnectionModel : IntIdentifierModelBase
    {
        [Required]
        public int? PlatformApiId { get; set; }

        [Required]
        public int PlatformId { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public int Status { get; set; }
        public string StatusName { get; set; }
        public string PlatformApiName { get; set; }
        public PlatformApiModel PlatformApi { get; set; }

        public List<SelectListItem> PlatformApiList { get; set; }

        public static PlatformApiConnectionModel New()
        {
            return new PlatformApiConnectionModel();
        }

        public static PlatformApiConnectionModel From(IPlatformApiManager apiManager, VendTech.DAL.PlatformApiConnection apiConnection)
        {
            if (apiConnection == null) throw new ArgumentNullException("PlatformApiConnection is null");

            PlatformApiConnectionModel model = new PlatformApiConnectionModel();

            model.Id = apiConnection.Id;
            model.PlatformApiId = apiConnection.PlatformApiId;
            model.Name = apiConnection.Name;
            model.Status = apiConnection.Status;
            model.StatusName = EnumUtils.GetEnumName<StatusEnum>(apiConnection.Status);
            model.CreatedAt = apiConnection.CreatedAt;
            model.UpdatedAt = apiConnection.UpdatedAt;
            model.PlatformApi = PlatformApiModel.From(apiManager, apiConnection.PlatformApi);
            model.PlatformApiName = model.PlatformApi.Name;
            model.PlatformId = apiConnection.PlatformId;

            return model;
        }

        public VendTech.DAL.PlatformApiConnection To(VendTech.DAL.PlatformApiConnection apiConnection)
        {
            if (apiConnection == null) throw new ArgumentNullException("VendTech.DAL.PlatformApiConnection is null");

            apiConnection.Id = Id;
            apiConnection.PlatformApiId = PlatformApiId; 
            apiConnection.Name = Name;
            apiConnection.Status = Status;
            //apiConnection.CreatedAt = CreatedAt;
            apiConnection.UpdatedAt = UpdatedAt;
            apiConnection.PlatformId = PlatformId;

            return apiConnection;
        }
    }

    public class PlatformPacParams : LongIdentifierModelBase
    {
        public int PlatformId { get; set; }
        public int PlatformApiConnId { get; set; }
        public string Config { get; set; }
        public IDictionary<string, string> ConfigDictionary { get; set; }

        public static PlatformPacParams From(VendTech.DAL.PlatformPacParam pacParams)
        {
            return new PlatformPacParams
            {
                Id = pacParams.Id,
                PlatformApiConnId = pacParams.PlatformApiConnectionId,
                PlatformId = pacParams.PlatformId,
                Config = pacParams.Config,
                ConfigDictionary =  (! string.IsNullOrWhiteSpace(pacParams.Config))
                        ? JsonConvert.DeserializeObject<Dictionary<string, string>>(pacParams.Config): null,
                UpdatedAt = pacParams.UpdatedAt,
                CreatedAt = pacParams.CreatedAt,
            };
        }
    }
}
