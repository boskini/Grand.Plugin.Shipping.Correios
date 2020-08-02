using System.Collections.Generic;
using Grand.Framework.Mvc.ModelBinding;

namespace Grand.Plugin.Shipping.Correios.Models
{
    public class CorreiosShippingModel
    {
        public CorreiosShippingModel()
        {
            ServicesOffered = new List<string>();
            AvailableCarrierServices = new List<string>();
        }

        [GrandResourceDisplayName("Plugins.Shipping.Correios.Fields.Url")]
        public string Url { get; set; }

        [GrandResourceDisplayName("Plugins.Shipping.Correios.Fields.PostalCodeFrom")]
        public string PostalCodeFrom { get; set; }

        [GrandResourceDisplayName("Plugins.Shipping.Correios.Fields.CompanyCode")]
        public string CompanyCode { get; set; }

        [GrandResourceDisplayName("Plugins.Shipping.Correios.Fields.Password")]
        public string Password { get; set; }

        [GrandResourceDisplayName("Plugins.Shipping.Correios.Fields.AddDaysForDelivery")]
        public string AddDaysForDelivery { get; set; }

        public IList<string> ServicesOffered { get; set; }
        [GrandResourceDisplayName("Plugins.Shipping.Correios.Fields.AvailableCarrierServices")]
        public IList<string> AvailableCarrierServices { get; set; }
        public string[] CheckedCarrierServices { get; set; }

        [GrandResourceDisplayName("Plugins.Shipping.Correios.Fields.ServiceNameDefault")]
        public string ServiceNameDefault { get; set; }

        [GrandResourceDisplayName("Plugins.Shipping.Correios.Fields.ShippingRateDefault")]
        public decimal ShippingRateDefault { get; set; }

        [GrandResourceDisplayName("Plugins.Shipping.Correios.Fields.QtdDaysForDeliveryDefault")]
        public int QtdDaysForDeliveryDefault { get; set; }

        [GrandResourceDisplayName("Plugins.Shipping.Correios.Fields.PercentageShippingFee")]
        public decimal PercentageShippingFee { get; set; }
    }
}