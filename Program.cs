  using Microsoft.AspNetCore.Builder;
    using Microsoft.ApplicationInsights.Extensibility;
   using Microsoft.Extensions.DependencyInjection;
   using Serilog;
    using Serilog.Context;
    using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
    using System.Linq;

   namespace SerilogDemoApp
   {

       public class Program
       {
           public static void Main(string[] args)
           {
               try
               {
                   var builder = WebApplication.CreateBuilder(args);

                   // Register Application Insights so Serilog can reuse the app's telemetry pipeline.
                   builder.Services.AddApplicationInsightsTelemetry();
                   builder.Host.UseSerilog((context, services, configuration) =>
                   {
                       configuration
                           .MinimumLevel.Information()
                           .Enrich.FromLogContext()
                           .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

                       var telemetryConfiguration = services.GetService<TelemetryConfiguration>();
                       if (telemetryConfiguration != null)
                       {
                           configuration.WriteTo.ApplicationInsights(telemetryConfiguration, TelemetryConverter.Traces);
                       }
                   });

                   builder.Services.AddControllers();
                   builder.Services.AddEndpointsApiExplorer();
                   builder.Services.AddSwaggerGen();


                   var app = builder.Build();

                   static string? GetEasyAuthUserGuid(HttpContext httpContext)
                   {
                       return httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"].FirstOrDefault()
                           ?? httpContext.User?.Claims.FirstOrDefault(c => c.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                           ?? httpContext.User?.Claims.FirstOrDefault(c => c.Type == "oid")?.Value;
                   }

                   // Enrich all logs during this request with the Easy Auth user GUID.
                   app.Use(async (context, next) =>
                   {
                       var easyAuthUserGuid = GetEasyAuthUserGuid(context);
                       using (LogContext.PushProperty("EasyAuthUserGuid", easyAuthUserGuid ?? "unknown"))
                       {
                           await next();
                       }
                   });


                   app.UseSerilogRequestLogging(options =>
                   {
                       options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                       {
                           diagnosticContext.Set("EasyAuthUserGuid", GetEasyAuthUserGuid(httpContext) ?? "unknown");
                       };
                   }); // Serilog standard request logging

                   if (app.Environment.IsDevelopment())
                   {
                       app.UseSwagger();
                       app.UseSwaggerUI();
                   }

                   // Example API Endpoint - No need to pass the enricher now!
                   app.MapGet("/api/hello", (HttpContext httpContext) =>
                   {
                       var easyAuthUserGuid = GetEasyAuthUserGuid(httpContext);

                       Log.Information("Received request to /api/hello.");
                       return Results.Ok(new
                       {
                           Message = $"v2 Hello from Serilog-enriched ASP.NET Core! UserGuid: {easyAuthUserGuid ?? "unknown"}",
                           UserGuid = easyAuthUserGuid
                       });
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