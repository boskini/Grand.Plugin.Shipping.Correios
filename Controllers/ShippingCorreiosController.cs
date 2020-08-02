using System;
using System.Text;
using Grand.Core;
using Grand.Plugin.Shipping.Correios.Models;
using Grand.Services.Configuration;
using Grand.Plugin.Shipping.Correios.Domain;
using Grand.Plugin.Shipping.Correios.Utils;
using Grand.Framework.Mvc.Filters;
using Grand.Framework.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Grand.Plugin.Shipping.Correios.Controllers
{
    [Area("Admin")]
    [AuthorizeAdmin]
    public class ShippingCorreiosController : BaseShippingController
    {
        private readonly CorreiosSettings _correiosSettings;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;

        public ShippingCorreiosController(CorreiosSettings correiosSettings, ISettingService settingService, IWebHelper webHelper)
        {
            this._correiosSettings = correiosSettings;
            this._settingService = settingService;
            this._webHelper = webHelper;
        }

        public ActionResult Configure()
        {
            var model = new CorreiosShippingModel();
            model.Url = _correiosSettings.Url;
            model.PostalCodeFrom = _correiosSettings.PostalCodeFrom;
            model.CompanyCode = _correiosSettings.CompanyCode;
            model.Password = _correiosSettings.Password;
            model.AddDaysForDelivery = _correiosSettings.AddDaysForDelivery.ToString();
            model.ServiceNameDefault = _correiosSettings.ServiceNameDefault;
            model.ShippingRateDefault = _correiosSettings.ShippingRateDefault;
            model.QtdDaysForDeliveryDefault = _correiosSettings.QtdDaysForDeliveryDefault;
            model.PercentageShippingFee = _correiosSettings.PercentageShippingFee;

            var serviceTypoes = new CorreiosServiceType();
            foreach (string service in serviceTypoes.Services)
                model.AvailableCarrierServices.Add(service);
            LoadSavedServices(model, serviceTypoes);

            return View("~/Plugins/Shipping.Correios/Views/Configure.cshtml", model);
        }

        private void LoadSavedServices(CorreiosShippingModel model, CorreiosServiceType serviceTypoes)
        {
            if (!string.IsNullOrEmpty(_correiosSettings.ServicesOffered))
                foreach (string service in serviceTypoes.Services)
                {
                    string serviceId = CorreiosServiceType.GetServiceId(service);
                    if (!string.IsNullOrEmpty(serviceId) && !string.IsNullOrEmpty(_correiosSettings.ServicesOffered))
                        if (_correiosSettings.ServicesOffered.Contains($"[{serviceId}]")) // Add delimiters [] so that single digit IDs aren't found in multi-digit IDs
                            model.ServicesOffered.Add(service);
                }
        }

        [HttpPost]
        public ActionResult Configure(CorreiosShippingModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            _correiosSettings.Url = model.Url;
            _correiosSettings.PostalCodeFrom = model.PostalCodeFrom;
            _correiosSettings.CompanyCode = model.CompanyCode;
            _correiosSettings.Password = model.Password;
            _correiosSettings.AddDaysForDelivery = Convert.ToInt32(model.AddDaysForDelivery);
            _correiosSettings.ServiceNameDefault = model.ServiceNameDefault;
            _correiosSettings.ShippingRateDefault = model.ShippingRateDefault;
            _correiosSettings.QtdDaysForDeliveryDefault = model.QtdDaysForDeliveryDefault;
            _correiosSettings.PercentageShippingFee = model.PercentageShippingFee;
            _correiosSettings.ServicesOffered = GetSelectedServices(model);

            _settingService.SaveSetting(_correiosSettings);
            return Configure();
        }

        private string GetSelectedServices(CorreiosShippingModel model)
        {
            var carrierServicesOfferedDomestic = new StringBuilder();
            if (model.CheckedCarrierServices != null)
                foreach (var cs in model.CheckedCarrierServices)
                {
                    string serviceId = CorreiosServiceType.GetServiceId(cs);
                    if (!string.IsNullOrEmpty(serviceId))
                        carrierServicesOfferedDomestic.Append($"[{serviceId}]:"); // Add delimiters [] so that single digit IDs aren't found in multi-digit IDs
                }
            return carrierServicesOfferedDomestic.ToString().RemoveLastIfEndsWith(":");
        }
    }
}
