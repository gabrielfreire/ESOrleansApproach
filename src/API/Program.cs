
using ESOrleansApproach.Domain.Common;
using Orleans.Hosting;
using Orleans;
using Polly;
using Serilog;
using ESOrleansApproach.API.Extensions;
using ESOrleansApproach.API.Common;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Default", Serilog.Events.LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Error)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Error)
    .MinimumLevel.Override("Orleans", Serilog.Events.LogEventLevel.Error)
    .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Error)
    .WriteTo.Console(outputTemplate: "[{Timestamp:dd-MMM-yyyy HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .Enrich.WithProperty("AppName", "ESOrleansApproach")
    .CreateLogger();

var _builder = WebApplication.CreateBuilder(args);
{
    _builder.Host.UseSerilog();
    _builder.Host.ConfigureOrleans();
    _builder.Logging.AddConsole();

    // Add all application services to the container
    _builder.Services.AddApplicationServices(_builder.Configuration, _builder.Environment);
}

var app = _builder.Build();
{

    app.UseSwaggerUiWithProxy(_builder.Configuration);

    app.UseCors(x =>
           x.AllowAnyMethod()
           .AllowAnyHeader()
           .AllowCredentials()
           .SetIsOriginAllowed(origin => true));


    //app.UseCustomExceptionMiddleware();
    app.UseHealthChecks("/health");

    app.UseSecurityHeaders(
            SecurityHeadersDefinitions.GetHeaderPolicyCollection(app.Environment.IsDevelopment(),
            app.Configuration["IdentityConfiguration:Authority"]));

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseAuthentication();
    app.UseAuthenticationRequestContext();
    app.UseAuthorization();

    app.MapControllers();

    try
    {
        // it is necessary to enable legacy timestamp behavior in order for Npgsql to work in .NET 8
        // maybe changes to database tables are necessary in the future
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        await app.EnsureDatabaseCreated();
    
        app.Run();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "An error occurred while starting the application.");
    }
    finally
    {
        Log.CloseAndFlush();

    }
}