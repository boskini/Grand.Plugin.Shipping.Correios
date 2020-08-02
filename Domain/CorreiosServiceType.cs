namespace Grand.Plugin.Shipping.Correios.Domain
{
    public class CorreiosServiceType
    {
        private string[] _services = {
                                        "Sedex à vista",
                                        "PAC à vista",
                                        "Sedex 12 (à vista)",
                                        "Sedex 10 (à vista)",
                                        "Sedex Hoje"
                                     };

        public string[] Services => _services;

        public static string GetServiceName(string serviceId)
        {
            string service = string.Empty;
            switch (serviceId)
            {
                case "04014":
                    service = "Sedex à vista";
                    break;
                case "04510":
                    service = "PAC à vista";
                    break;
                case "04782":
                    service = "Sedex 12 (à vista)";
                    break;
                case "04790":
                    service = "Sedex 10 (à vista)";
                    break;
                case "04804":
                    service = "Sedex Hoje";
                    break;
                default:
                    break;
            }
            return service;
        }

        public static string GetServiceId(string service)
        {
            string serviceId = string.Empty;
            switch (service)
            {
                case "Sedex à vista":
                    serviceId = "04014";
                    break;
                case "PAC à vista":
                    serviceId = "04510";
                    break;
                case "Sedex 12 (à vista)":
                    serviceId = "04782";
                    break;
                case "Sedex 10 (à vista)":
                    serviceId = "04790";
                    break;
                case "Sedex Hoje":
                    serviceId = "04804";
                    break;
                default:
                    break;
            }
            return serviceId;
        }
    }
}
