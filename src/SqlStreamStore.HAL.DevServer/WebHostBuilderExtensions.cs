﻿namespace SqlStreamStore.HAL.DevServer
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;

    internal static class WebHostBuilderExtensions
    {
        public static IWebHostBuilder UseStartup(this IWebHostBuilder builder, IStartup startup)
            => builder
                .ConfigureServices(services => services.AddSingleton(startup))
                .UseSetting(WebHostDefaults.ApplicationKey, startup.GetType().AssemblyQualifiedName);
    }
}