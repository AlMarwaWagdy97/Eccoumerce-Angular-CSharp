using Scalar.AspNetCore;
using Ecommerce;

var builder = WebApplication.CreateBuilder(args);

// Connect Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
    throw new InvalidDataException ("Connection string DefaultConnection not found");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddDependancies(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors("AngularAppPolicy");

app.UseAuthorization();

//app.MapIdentityApi<ApplicationUser>();

app.MapControllers();

//app.UseExceptionHandler();

app.Run();
