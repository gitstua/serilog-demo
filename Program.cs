  using Microsoft.AspNetCore.Builder;
   using Microsoft.Extensions.DependencyInjection;
   using Serilog;
    using Serilog.Context;
   using Serilog.Core;
   using Serilog.Events;
   using System.Collections.Generic;

   namespace SerilogDemoApp
   {
       // 1. Define the custom enricher
       public class AzureAdIdentityEnricher : ILogEventEnricher
       {
           private readonly Dictionary<string, string> _identityData;

           public AzureAdIdentityEnricher(Dictionary<string, string> identityData)
           {
               _identityData = identityData ?? new Dictionary<string, string>();
           }

           public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
           {
               // Add custom properties from the identity dictionary to the log event
               foreach (var pair in _identityData)
               {
                   logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(pair.Key, pair.Value));
               }
           }
       }

       // 2. Service to simulate fetching/providing identity context
       public interface IIdentityService
       {
           Dictionary<string, string> CurrentUserIdentity { get; }
       }

       public class IdentityService : IIdentityService
       {
           // This simulates getting the claims from a request or token validation
           public Dictionary<string, string> CurrentUserIdentity => new Dictionary<string, string>
           {
               {"UserPrincipalName", "appuser@example.com"},
               {"Role", "ServiceAccount"},
               {"TenantId", "abcd-456"}
           };
       }

       public class Program
       {
           public static void Main(string[] args)
           {
               // Configure Serilog before the Host is built
               Log.Logger = new LoggerConfiguration()
                   .MinimumLevel.Information()
                   .Enrich.FromLogContext()
                   .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                   .CreateLogger();

               try
               {
                   var builder = WebApplication.CreateBuilder(args);
                   builder.Host.UseSerilog();

                   // 3. Register services (including our simulated identity provider)
                   builder.Services.AddSingleton<IIdentityService, IdentityService>();
                   builder.Services.AddControllers();
                   builder.Services.AddEndpointsApiExplorer();
                   builder.Services.AddSwaggerGen();


                   var app = builder.Build();

                   // 4. Register the Enricher Middleware (best practice for per-request data)
                   app.Use(async (context, next) =>
                   {
                       // Get identity from our service layer
                       using (var scope = context.RequestServices.CreateScope())
                       {
                           var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();

                           // Create a specific enricher instance for this request's context
                           var enricher = new AzureAdIdentityEnricher(identityService.CurrentUserIdentity);

                           // Attach the enricher to the logging scope for all logs during this request
                           using (LogContext.PushProperty("UserIdentity", identityService.CurrentUserIdentity))
                           {
                               await next();
                           }
                       }
                   });


                   app.UseSerilogRequestLogging(); // Serilog standard request logging

                   if (app.Environment.IsDevelopment())
                   {
                       app.UseSwagger();
                       app.UseSwaggerUI();
                   }

                   // Example API Endpoint - No need to pass the enricher now!
                   app.MapGet("/api/hello", () =>
                   {
                       Log.Information("Received request to /api/hello.");
                       return Results.Ok(new { Message = "Hello from Serilog-enriched ASP.NET Core!" });
                   }).WithName("GetHello");

                   app.Run();
               }
               catch (Exception ex)
               {
                   Log.Fatal(ex, "Host terminated unexpectedly!");
               }
               finally
               {
                   Log.CloseAndFlush();
               }
           }
       }
   }