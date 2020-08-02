using Autofac;
using Grand.Core.Configuration;
using Grand.Core.Infrastructure;
using Grand.Core.Infrastructure.DependencyManagement;
using Grand.Plugin.Shipping.Correios.Controllers;
using Grand.Plugin.Shipping.Correios.Service;

namespace Grand.Plugin.Shipping.Correios.Infrastructure
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        public virtual void Register(ContainerBuilder builder, ITypeFinder typeFinder, GrandConfig grandConfig)
        {
            builder.RegisterType<ShippingCorreiosController>();
            builder.RegisterType<CorreiosService>().As<ICorreiosService>().InstancePerLifetimeScope();

            builder.RegisterType<CorreiosComputationMethod>().InstancePerLifetimeScope();
        }

        public int Order => 2;
    }
}
