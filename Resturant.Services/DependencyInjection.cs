using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Resturant.Services.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resturant.Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Register services here, e.g.:
            // services.AddScoped<IOrderService, OrderService>();
            services.AddTransient<IEmailSender, EmailSender>();
            
            return services;
        }
    }
}
