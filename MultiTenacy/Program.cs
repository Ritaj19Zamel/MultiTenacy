



var builder = WebApplication.CreateBuilder(args);


builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IProductService, ProductService>();

builder.Services.AddTenancy(builder.Configuration);
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var tenantService = sp.GetRequiredService<ITenantService>();
    var interceptor = sp.GetRequiredService<AuditInterceptor>();
    options.UseSqlServer(tenantService.GetConnectionString())
           .AddInterceptors(interceptor);  
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
