using Grand.Core;
using Grand.Core.Domain.Directory;
using Grand.Services.Directory;
using Grand.Services.Shipping;
using System;
using System.Text;
using System.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel;
using Grand.Plugin.Shipping.Correios.Utils;
using Grand.Services.Catalog;
using System.Threading.Tasks;
using Grand.Plugin.Shipping.Correios;

namespace Grand.Plugin.Shipping.Correios.Service
{
    public class CorreiosService : ICorreiosService
    {
        //colocar as unidades de medida e moeda utilizadas como configuração
        private const string MEASURE_WEIGHT_SYSTEM_KEYWORD = "kg";
        private const string MEASURE_DIMENSION_SYSTEM_KEYWORD = "centimeter";
        private const string CURRENCY_CODE = "BRL";
        //colocar o tamanho/peso mínimo/máximo permitido dos produtos como configuração

        private readonly IMeasureService _measureService;
        private readonly IShippingService _shippingService;
        private readonly CorreiosSettings _correiosSettings;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly IProductService _productService;


        public CorreiosService(IMeasureService measureService, IShippingService shippingService, CorreiosSettings correiosSettings,
            ICurrencyService currencyService, CurrencySettings currencySettings, IProductService productService)
        {
            this._measureService = measureService;
            this._shippingService = shippingService;
            this._correiosSettings = correiosSettings;
            this._currencyService = currencyService;
            this._currencySettings = currencySettings;
            this._productService = productService;
        }

        
        public async Task<WSCorreiosCalcPrecoPrazo.cResultado> RequestCorreiosAsync(GetShippingOptionRequest getShippingOptionRequest)
        {
            Binding binding = new BasicHttpBinding();
            binding.Name = "CalcPrecoPrazoWSSoap";

            if (string.IsNullOrEmpty(getShippingOptionRequest.ZipPostalCodeFrom))
                getShippingOptionRequest.ZipPostalCodeFrom = _correiosSettings.PostalCodeFrom;

            decimal length, width, height;
            (width, length, height) = await GetDimensions(getShippingOptionRequest);

            EndpointAddress endpointAddress = new EndpointAddress(_correiosSettings.Url);

            WSCorreiosCalcPrecoPrazo.CalcPrecoPrazoWSSoap wsCorreios = new WSCorreiosCalcPrecoPrazo.CalcPrecoPrazoWSSoapClient(binding, endpointAddress);

            var selectedServices = GetSelectecServices(_correiosSettings);
            var shippRequest = await GetWheightAsync(getShippingOptionRequest);
            var declaredValue = await GetDeclaredValueAsync(getShippingOptionRequest);
                       
            return await wsCorreios.CalcPrecoPrazoAsync(
                (_correiosSettings.CompanyCode==null ? "" : _correiosSettings.CompanyCode),
                (_correiosSettings.Password==null ? "" : _correiosSettings.Password),
                selectedServices,
                getShippingOptionRequest.ZipPostalCodeFrom,
                getShippingOptionRequest.ShippingAddress.ZipPostalCode,
                shippRequest.ToString(), //nVlPeso
                1, //nCdFormato
                length, //nVlComprimento
                height, //nVlAltura
                width, //nVlLargura
                0, //nVlDiametro
                "N",//sCdMaoPropria
                (declaredValue < decimal.Parse("20.5") ? decimal.Parse("20.51") : declaredValue), //nVlValorDeclarado
                "N" //sCdAvisoRecebimento
                ) ;
        }

        private async Task<decimal> GetDeclaredValueAsync(GetShippingOptionRequest shippingOptionRequest)
        {
            decimal declaredValue = await GetConvertedRateFromPrimaryCurrency(shippingOptionRequest.Items.Sum(item => _productService.GetProductById(item.ShoppingCartItem.ProductId).Result.Price));
            return declaredValue < 18.0M ? 18.0M : declaredValue;
        }

        private async Task<int> GetWheightAsync(GetShippingOptionRequest shippingOptionRequest)
        {
            var usedMeasureWeight = await _measureService.GetMeasureWeightBySystemKeyword(MEASURE_WEIGHT_SYSTEM_KEYWORD);
            if (usedMeasureWeight == null)
                throw new GrandException($"Correios shipping service. Could not load \"{MEASURE_WEIGHT_SYSTEM_KEYWORD}\" measure weight");

            var weight = Convert.ToInt32(Math.Ceiling(await _measureService.ConvertFromPrimaryMeasureWeight(await _shippingService.GetTotalWeight(shippingOptionRequest), usedMeasureWeight)));
            return weight < 1 ? 1 : weight;
        }

        private async Task<(decimal, decimal, decimal)> GetDimensions(GetShippingOptionRequest shippingOptionRequest)
        {
            decimal length, width, height;
            var usedMeasureDimension = _measureService.GetMeasureDimensionBySystemKeyword(MEASURE_DIMENSION_SYSTEM_KEYWORD);
            if (usedMeasureDimension == null)
                throw new GrandException($"Correios shipping service. Could not load \"{MEASURE_DIMENSION_SYSTEM_KEYWORD}\" measure dimension");

            //await _shippingService.GetDimensions(shippingOptionRequest.Items, out width, out length, out height);
            (width, length, height) = await _shippingService.GetDimensions(shippingOptionRequest.Items);
            
            length = await _measureService.ConvertFromPrimaryMeasureDimension(length, usedMeasureDimension.Result);
            if (length < 16)
                length = 16;

            height = await _measureService.ConvertFromPrimaryMeasureDimension(height, usedMeasureDimension.Result);
            if (height < 2)
                height = 2;

            width = await _measureService.ConvertFromPrimaryMeasureDimension(width, usedMeasureDimension.Result);
            if (width < 11)
                width = 11;

            return (width, length, height);
        }

        public async Task<decimal> GetConvertedRateFromPrimaryCurrency(decimal rate)
        {
            return await GetConvertedRateAsync(rate, await _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId), await GetSupportedCurrencyAsync());
        }

        public async Task<decimal> GetConvertedRateToPrimaryCurrencyAsync(decimal rate) {
            return await GetConvertedRateAsync(rate, await GetSupportedCurrencyAsync(), await _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId));
        }

        private async Task<decimal> GetConvertedRateAsync(decimal rate, Currency source, Currency target)
        {
            return (source.CurrencyCode == target.CurrencyCode) ? rate : await _currencyService.ConvertCurrency(rate, source, target);
        }

        private async Task<Currency> GetSupportedCurrencyAsync()
        {
            var currency = await _currencyService.GetCurrencyByCode(CURRENCY_CODE);
            if (currency == null)
                throw new GrandException($"Correios shipping service. Could not load \"{CURRENCY_CODE}\" currency");
            return currency;
        }

        private string GetSelectecServices(CorreiosSettings correioSettings)
        {
            StringBuilder sb = new StringBuilder();
            correioSettings.ServicesOffered.RemoveLastIfEndsWith(":").Split(':').ToList().ForEach(service => sb.Append(service?.Remove(0, 1).Replace(']', ',')));
            return sb.ToString().Remove(sb.ToString().Length - 1, 1);
        }
    }
}
