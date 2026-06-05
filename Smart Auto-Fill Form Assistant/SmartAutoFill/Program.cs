using Microsoft.EntityFrameworkCore;
using SmartAutoFill.Components;
using SmartAutoFill.Data;
using SmartAutoFill.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// SQL Server persistence (EF Core).
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Document extraction (Azure Document Intelligence).
builder.Services.AddScoped<IDocumentExtractionService, AzureDocumentExtractionService>();

// PII masking + persistence.
builder.Services.AddSingleton<IMaskingService, RegexMaskingService>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

builder.Services.AddHttpClient("ollama", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"]!);
    // Local CPU inference (esp. the first cold-start model load) can far exceed
    // the default 100s. Allow plenty of headroom.
    c.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddScoped<IRagService, RagService>();

// Allow larger uploads over the SignalR circuit (PDFs/scans can be several MB).
builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(o =>
{
    o.MaximumReceiveMessageSize = 20 * 1024 * 1024; // 20 MB
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
