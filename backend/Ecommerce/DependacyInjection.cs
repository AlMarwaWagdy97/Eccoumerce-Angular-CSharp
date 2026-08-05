using FluentValidation.AspNetCore;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Ecommerce.Authentication;
using Ecommerce.Email;
using Ecommerce.Options;
using System.Reflection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Ecommerce
{
    public static class DependacyInjection
    {
        public static IServiceCollection AddDependancies(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpContextAccessor();

            services.AddControllers();

            services.AddCors(options =>
            {
                options.AddPolicy("AngularAppPolicy", policy =>
                {
                    policy.AllowAnyOrigin() // 👈 بيسمح لأي بورت يكلم الـ API بدون قيود في الـ Dev mode
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            services.AddCors(options =>
            options.AddDefaultPolicy(builder =>
            builder.AllowAnyMethod()
                   .AllowAnyHeader()
                   .WithOrigins(configuration.GetSection("AllowedOrigins").Get<string[]>()!)
                   )
            );

            services.AddAuthConfig(configuration);

            // Connect Database
            var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                throw new InvalidDataException("Connection string DefaultConnection not found");
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

            services.AddOpenApi()
                .AddMapsterConf()
                .AddFluentValidationConf();

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAdminAuthService, AdminAuthService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICartService, CartService>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IFavoriteService, FavoriteService>();
            services.AddScoped<IAddressService, AddressService>();
            services.AddScoped<ICardService, CardService>();
            services.AddScoped<IRoleService, RoleService>();

            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails();
                
            return services;
        }

        private static IServiceCollection AddMapsterConf(this IServiceCollection services)
        {
            var mappingConfig = TypeAdapterConfig.GlobalSettings;
            mappingConfig.Scan(Assembly.GetExecutingAssembly());

            services.AddSingleton<IMapper>(new Mapper(mappingConfig));

            return services;
        }

        private static IServiceCollection AddFluentValidationConf(this IServiceCollection services)
        {
            services.AddFluentValidationAutoValidation()
               .AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
            return services;
        }

        private static IServiceCollection AddAuthConfig(this IServiceCollection services,
           IConfiguration configuration)
        {
            services.AddIdentity<ApplicationUser, IdentityRole>()
              .AddEntityFrameworkStores<ApplicationDbContext>();


            services.AddSingleton<IJwtProvider, JwtProvider>();

            //services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
            services.AddOptions<JwtOptions>()
                .BindConfiguration(JwtOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<SmtpOptions>()
                .BindConfiguration(SmtpOptions.SectionName)
                .ValidateOnStart();

            services.AddOptions<FrontendOptions>()
                .BindConfiguration(FrontendOptions.SectionName)
                .ValidateOnStart();

            services.AddScoped<IEmailSender, SmtpEmailSender>();

            var jwtSettings = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(o =>
            {
                o.SaveToken = true;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Key!)),
                    ValidIssuer = jwtSettings?.Issuer,
                    ValidAudience = jwtSettings?.Audience
                };
            })
            .AddJwtBearer(Ecommerce.Authorization.AdminAuthDefaults.Scheme, o =>
            {
                o.SaveToken = true;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Key!)),
                    ValidIssuer = jwtSettings?.Issuer,
                    ValidAudience = jwtSettings?.AdminAudience
                };
            });

            services.AddSingleton<IAuthorizationPolicyProvider, Ecommerce.Authorization.PermissionPolicyProvider>();
            services.AddSingleton<IAuthorizationHandler, Ecommerce.Authorization.PermissionAuthorizationHandler>();
            services.AddSingleton<IAdminJwtProvider, AdminJwtProvider>();

            return services;
        }
    }
}
