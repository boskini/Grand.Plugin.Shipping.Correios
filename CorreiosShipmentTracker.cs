using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grand.Services.Shipping.Tracking;

namespace Grand.Plugin.Shipping.Correios
{
    public class CorreiosShipmentTracker : IShipmentTracker
    {
        private readonly CorreiosSettings _correiosSettings;

        public CorreiosShipmentTracker(CorreiosSettings correiosSettings)
        {
            this._correiosSettings = correiosSettings;
        }

        public virtual Task<bool> IsMatch(string trackingNumber)
        {
            throw new NotImplementedException("");
        }

        public virtual Task<string> GetUrl(string trackingNumber)
        {
            throw new NotImplementedException("");
        }

        public virtual Task<IList<ShipmentStatusEvent>> GetShipmentEvents(string trackingNumber)
        {
            throw new NotImplementedException("");
        }

    }

}