using System;
using System.Globalization;
using Grand.Core;
using Grand.Core.Domain.Shipping;
using Grand.Core.Plugins;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using Grand.Services.Logging;
using Grand.Services.Shipping;
using Grand.Services.Shipping.Tracking;
using Grand.Plugin.Shipping.Correios.Domain;
using Grand.Plugin.Shipping.Correios.Service;
using Grand.Core.Domain.Orders;
using System.Collections.Generic;
using Microsoft.AspNetCore.Routing;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Grand.Plugin.Shipping.Correios
{
    public class CorreiosComputationMethod : BasePlugin, IShippingRateComputationMethod
    {
        private readonly ISettingService _settingService;
        private readonly CorreiosSettings _correiosSettings;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly ILocalizationService _localizationService;
        private readonly ICorreiosService _correiosService;
        private readonly ILanguageService _languageService;

        public CorreiosComputationMethod(ISettingService settingService,
            CorreiosSettings correiosSettings, 
            ILogger logger,
            IWebHelper webHelper,
            ILocalizationService localizationService, 
            ICorreiosService correiosService,
            ILanguageService languageService)
        {
            _settingService = settingService;
            _correiosSettings = correiosSettings;
            _logger = logger;
            _localizationService = localizationService;
            _correiosService = correiosService;
            _languageService = languageService;
            _webHelper = webHelper;
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/ShippingCorreios/Configure";
        }

        private bool ValidateRequest(GetShippingOptionRequest getShippingOptionRequest, GetShippingOptionResponse response)
        {
            if (getShippingOptionRequest.Items == null)
            {
                response.AddError(_localizationService.GetResource("Plugins.Shipping.Correios.Message.NoShipmentItems"));
                return false;
            }
            if (getShippingOptionRequest.ShippingAddress == null)
            {
                response.AddError(_localizationService.GetResource("Plugins.Shipping.Correios.Message.AddressNotSet"));
                return false;
            }
            if (string.IsNullOrEmpty(getShippingOptionRequest.ShippingAddress.CountryId))
            {
                response.AddError(_localizationService.GetResource("Plugins.Shipping.Correios.Message.CountryNotSet"));
                return false;
            }
            if (string.IsNullOrEmpty(getShippingOptionRequest.ShippingAddress.StateProvinceId))
            {
                response.AddError(_localizationService.GetResource("Plugins.Shipping.Correios.Message.StateNotSet"));
                return false;
            }
            if (getShippingOptionRequest.ShippingAddress.ZipPostalCode == null)
            {
                response.AddError(_localizationService.GetResource("Plugins.Shipping.Correios.Message.PostalCodeNotSet"));
                return false;
            }
            return true;
        }

        public async Task<GetShippingOptionResponse> GetShippingOptions(GetShippingOptionRequest getShippingOptionRequest)
        {
            if (getShippingOptionRequest == null)
                throw new ArgumentNullException("getShippingOptionRequest");

            var response = new GetShippingOptionResponse();

            if (!ValidateRequest(getShippingOptionRequest, response))
                return response;

            try
            {
                WSCorreiosCalcPrecoPrazo.cResultado wsResult = await _correiosService.RequestCorreiosAsync(getShippingOptionRequest);
                foreach (WSCorreiosCalcPrecoPrazo.cServico serv in wsResult?.Servicos)
                {
                    try
                    {
                        ValidateWSResult(serv);
                        response.ShippingOptions.Add(await GetShippingOption(ApplyAdditionalFee(Convert.ToDecimal(serv.Valor, new CultureInfo("pt-BR"))), CorreiosServiceType.GetServiceName(serv.Codigo.ToString()), CalcPrazoEntrega(serv)));
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e.Message, e);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error(e.Message, e);
            }

            if (response.ShippingOptions.Count <= 0)
                response.ShippingOptions.Add(await GetShippingOption(_correiosSettings.ShippingRateDefault, _correiosSettings.ServiceNameDefault, _correiosSettings.QtdDaysForDeliveryDefault));

            return response;
        }

        private decimal ApplyAdditionalFee(decimal rate) => _correiosSettings.PercentageShippingFee > 0.0M ? rate * _correiosSettings.PercentageShippingFee : rate;

        private async Task<ShippingOption> GetShippingOption(decimal rate, string serviceName, int prazo)
        {
            var d = await _correiosService.GetConvertedRateToPrimaryCurrencyAsync(rate);

            return new ShippingOption() { 
                Rate = d, 
                Name = $"{serviceName} - {prazo} dia(s)" 
            };
        }

        private int CalcPrazoEntrega(WSCorreiosCalcPrecoPrazo.cServico serv)
        {
            int prazo = Convert.ToInt32(serv.PrazoEntrega);
            if (_correiosSettings.AddDaysForDelivery > 0)
                prazo += _correiosSettings.AddDaysForDelivery;
            return prazo;
        }

        private void ValidateWSResult(WSCorreiosCalcPrecoPrazo.cServico wsServico)
        {
            if (string.IsNullOrEmpty(wsServico.Erro))
                throw new GrandException(wsServico.Erro + " - " + wsServico.MsgErro);

            if (Convert.ToInt32(wsServico.PrazoEntrega) <= 0)
                throw new GrandException(_localizationService.GetResource("Plugins.Shipping.Correios.Message.DeliveryUninformed"));

            if (Convert.ToDecimal(wsServico.Valor, new CultureInfo("pt-BR")) <= 0)
                throw new GrandException(_localizationService.GetResource("Plugins.Shipping.Correios.Message.InvalidValueDelivery"));
        }

        public override async Task Install()
        {
            var settings = new CorreiosSettings()
            {
                Url = "http://ws.correios.com.br/calculador/CalcPrecoPrazo.asmx",
                PostalCodeFrom = "",
                CompanyCode = "",
                Password = "",
                AddDaysForDelivery = 0,
                PercentageShippingFee = 1.0M
            };
            await _settingService.SaveSetting(settings);

            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.Url", "URL");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.Url.Hint", "Specify Correios URL.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.PostalCodeFrom", "Postal Code From");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.PostalCodeFrom.Hint", "Specify From Postal Code.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.CompanyCode", "Company Code");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.CompanyCode.Hint", "Specify Your Company Code.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.Password", "Password");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.Password.Hint", "Specify Your Password.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.AddDaysForDelivery", "Additional Days For Delivery");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.AddDaysForDelivery.Hint", "Set The Amount Of Additional Days For Delivery.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.AvailableCarrierServices", "Available Carrier Services");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.AvailableCarrierServices.Hint", "Set Available Carrier Services.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.ServiceNameDefault", "Service Name Default");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.ServiceNameDefault.Hint", "Service Name Used When The Correios Does Not Return Value.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.ShippingRateDefault", "Shipping Rate Default");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.ShippingRateDefault.Hint", "Shipping Rate Used When The Correios Does Not Return Value.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.QtdDaysForDeliveryDefault", "Number Of Days For Delivery Default");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.QtdDaysForDeliveryDefault.Hint", "Number Of Days For Delivery Used When The Correios Does Not Return Value.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.PercentageShippingFee", "Additional percentage shipping fee");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Fields.PercentageShippingFee.Hint", "Set the additional percentage shipping rate.");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Message.NoShipmentItems", "No shipment items");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Message.AddressNotSet", "Shipping address is not set");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Message.CountryNotSet", "Shipping country is not set");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Message.StateNotSet", "Shipping state is not set");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Message.PostalCodeNotSet", "Shipping zip postal code is not set");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,"Plugins.Shipping.Correios.Message.DeliveryUninformed", "Delivery uninformed");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.InvalidValueDelivery", "Invalid value delivery","en-US");
            
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.Url", "URL","pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.Url.Hint", "Forneça a URL do webservice.", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.PostalCodeFrom", "CEP origem", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.PostalCodeFrom.Hint", "Forneça o CEP origem.", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.CompanyCode", "Código da empresa", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.CompanyCode.Hint", "Forneça o código da empresa.", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.Password", "Senha", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.Password.Hint", "Forneça a senha.", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.AddDaysForDelivery", "Dias adicionais para envio", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.AddDaysForDelivery.Hint", "Forneça os dias adicionais para envio.", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.AvailableCarrierServices", "Serviços disponiveis", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.AvailableCarrierServices.Hint", "Escolha os serviços disponiveis.", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.ServiceNameDefault", "Nome do serviço padrão", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.ServiceNameDefault.Hint", "Nome usado quando os correios não fornecer um.", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.ShippingRateDefault", "Valor de envio padrão", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.ShippingRateDefault.Hint", "Valor padrão para quando os correios não informar o valor.", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.QtdDaysForDeliveryDefault", "Numero de dias padrão", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.QtdDaysForDeliveryDefault.Hint", "Numero de dias padrão quando os correios não informar.", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.PercentageShippingFee", "Porcentagem adicional no valor do frete", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.PercentageShippingFee.Hint", "Set the additional percentage shipping rate.", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.NoShipmentItems", "Sem items para envio", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.AddressNotSet", "O endereço de envio não foi ionformado", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.CountryNotSet", "O pais não foi informado", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.StateNotSet", "O estado do endereço de envio não informado", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.PostalCodeNotSet", "O CEP não foi informado", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.DeliveryUninformed", "Enytrega não informada", "pt-BR");
            await this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.InvalidValueDelivery", "Valor de entrega invalido", "pt-BR");

            await base.Install();
        }

        public override async Task Uninstall()
        {
            await _settingService.DeleteSetting<CorreiosSettings>();

            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.Url");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.Url.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.PostalCodeFrom");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.PostalCodeFrom.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.CompanyCode");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.CompanyCode.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.Password");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.Password.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.AddDaysForDelivery");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.AddDaysForDelivery.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.AvailableCarrierServices");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.AvailableCarrierServices.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.ServiceNameDefault");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.ServiceNameDefault.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.ShippingRateDefault");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.ShippingRateDefault.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.QtdDaysForDeliveryDefault");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.QtdDaysForDeliveryDefault.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.PercentageShippingFee");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Fields.PercentageShippingFee.Hint");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.NoShipmentItems");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.AddressNotSet");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.CountryNotSet");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.StateNotSet");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.PostalCodeNotSet");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.DeliveryUninformed");
            await this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Shipping.Correios.Message.InvalidValueDelivery");

            await base.Uninstall();
        }

        public Task<bool> HideShipmentMethods(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(false);
        }

        public async Task<decimal?> GetFixedRate(GetShippingOptionRequest getShippingOptionRequest)
        {
            return await Task.FromResult(decimal.Zero);
        }

        public async Task<IList<string>> ValidateShippingForm(IFormCollection form)
        {
            return await Task.FromResult(new List<string>());
        }

        public void GetPublicViewComponent(out string viewComponentName)
        {
            viewComponentName = "";
        }

        public ShippingRateComputationMethodType ShippingRateComputationMethodType => ShippingRateComputationMethodType.Realtime;

        public IShipmentTracker ShipmentTracker => new CorreiosShipmentTracker(_correiosSettings);
    }
}