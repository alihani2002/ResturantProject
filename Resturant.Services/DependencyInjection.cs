/* 
 * NOTE: didn't create migration or database
 */
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Resturant.Core.Interfaces;
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
            services.AddTransient<IEmailSender, EmailSender>();
            services.AddScoped<ICloudinaryService, CloudinaryService>();
            
            services.AddScoped<ITableService, TableService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<ISessionService, SessionService>();
            services.AddScoped<IAnalyticsService, AnalyticsService>();
            
            return services;
        }
    }
}

