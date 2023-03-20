using EQToolApis.DB;
using EQToolApis.Services;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<EQToolContext>(opts => opts.UseSqlServer(builder.Configuration.GetConnectionString("eqtooldb"))).AddScoped<EQToolContext>();
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHangfire(configuration => configuration
     .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
     .UseSimpleAssemblyNameTypeSerializer()
     .UseRecommendedSerializerSettings()
     .UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireConnection"), new SqlServerStorageOptions
     {
         CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
         SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
         QueuePollInterval = TimeSpan.Zero,
         UseRecommendedIsolationLevel = true,
         DisableGlobalLocks = true,
         DashboardJobListLimit = 2
     }));
builder.Services.AddHangfireServer();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("HangfireAccess", cfgPolicy =>
    {
        cfgPolicy.AddAuthenticationSchemes(OpenIdConnectDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser();
    });
});
builder.Services.Configure<DiscordServiceOptions>(options =>
{
    options.token = builder.Configuration.GetValue<string>("DiscordToken");
})
.AddSingleton<IDiscordService, DiscordService>();

builder.Services.AddMvc();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EQToolContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHangfireDashboard("/hangfire", new DashboardOptions()
    {
        Authorization = new List<IDashboardAuthorizationFilter> { }
    })
    .RequireAuthorization("HangfireAccess");
});
app.MapControllers();
app.MapRazorPages();

var isrelease = false;

#if !DEBUG
    isrelease = true;
#endif

if (isrelease)
{
    using (var scope = app.Services.CreateScope())
    {
        var backgroundclient = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        backgroundclient.AddOrUpdate<DiscordService.DiscordJob>(nameof(DiscordService.DiscordJob.ReadFutureMessages), (a) => a.ReadFutureMessages(), Cron.Minutely());
        backgroundclient.AddOrUpdate<DiscordService.DiscordJob>(nameof(DiscordService.DiscordJob.ReadPastMessages), (a) => a.ReadPastMessages(), Cron.Minutely());
        backgroundclient.AddOrUpdate<DiscordService.DiscordJob>(nameof(DiscordService.DiscordJob.StartItemPricing), (a) => a.StartItemPricing(), Cron.Hourly());
        backgroundclient.AddOrUpdate<UIDataBuild>(nameof(UIDataBuild.BuildData), (a) => a.BuildData(), "*/10 * * * *");
        var runnow = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
        runnow.Schedule<UIDataBuild>((a) => a.BuildData(), TimeSpan.FromSeconds(10));
    }
}
app.Run();
