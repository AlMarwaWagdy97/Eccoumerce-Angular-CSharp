using Scalar.AspNetCore;
using Ecommerce;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Connect Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
    throw new InvalidDataException ("Connection string DefaultConnection not found");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)
           // Favorite/Cart/CartItem are intentionally not soft-deletable but navigate to the
           // filtered Product entity. The warning is expected, not a defect.
           .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

builder.Services.AddDependancies(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    await Ecommerce.Presistence.DataSeeder.SeedAsync(app.Services);
    await Ecommerce.Presistence.AdminDataSeeder.SeedAsync(app.Services);
}

app.UseHttpsRedirection();

// Uploaded images under wwwroot/uploads are public by design — served before auth runs.
app.UseStaticFiles();

app.UseCors("AngularAppPolicy");

app.UseAuthorization();

//app.MapIdentityApi<ApplicationUser>();

app.MapControllers();

//app.UseExceptionHandler();

app.Run();
