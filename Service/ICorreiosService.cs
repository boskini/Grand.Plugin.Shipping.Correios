using Grand.Services.Shipping;
using System.Threading.Tasks;

namespace Grand.Plugin.Shipping.Correios.Service
{
    public interface ICorreiosService
    {
        Task<WSCorreiosCalcPrecoPrazo.cResultado> RequestCorreiosAsync(GetShippingOptionRequest getShippingOptionRequest);

        Task<decimal> GetConvertedRateToPrimaryCurrencyAsync(decimal rate);
    }
}
