using AgentOrchestrator.Core.Interfaces;
using AgentOrchestrator.Core.Services;
using AgentOrchestrator.Infrastructure.Data;
using AgentOrchestrator.Infrastructure.Services;
using AgentOrchestrator.Web.Hubs;
using AgentOrchestrator.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
var workingDirectory = builder.Environment.ContentRootPath;

builder.Services.AddSingleton<IClaudeCodeRunner>(
    new ClaudeCodeCliRunner(workingDirectory));
builder.Services.AddSingleton<IAgentRepository>(
    new FileAgentRepository(dataDirectory));
builder.Services.AddSingleton<IThreadRepository>(
    new FileThreadRepository(dataDirectory));
builder.Services.AddSingleton<IProjectRepository>(
    new FileProjectRepository(dataDirectory));

builder.Services.AddSingleton<AgentService>();
builder.Services.AddSingleton<TeamService>();
builder.Services.AddSingleton<ThreadOrchestrationService>();

builder.Services.AddSingleton<PendingMessageTracker>();
builder.Services.AddHostedService<RequestPollingService>();

builder.WebHost.UseUrls("http://0.0.0.0:5181");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        ctx.Context.Response.Headers.Pragma = "no-cache";
        ctx.Context.Response.Headers.Expires = "0";
    }
});
app.UseRouting();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers.Expires = "0";
    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
