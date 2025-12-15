using GuardeSoftwareAPI.Dao;
using System.Data;
using GuardeSoftwareAPI.Services.accountMovement;
using GuardeSoftwareAPI.Services.activityLog;
using GuardeSoftwareAPI.Services.address;
using GuardeSoftwareAPI.Services.client;
using GuardeSoftwareAPI.Services.clientIncreaseRegimen;
using GuardeSoftwareAPI.Services.email;
using GuardeSoftwareAPI.Services.increaseRegimen;
using GuardeSoftwareAPI.Services.locker;
using GuardeSoftwareAPI.Services.lockerType;
using GuardeSoftwareAPI.Services.payment;
using GuardeSoftwareAPI.Services.paymentMethod;
using GuardeSoftwareAPI.Services.rental;
using GuardeSoftwareAPI.Services.rentalAmountHistory;
using GuardeSoftwareAPI.Services.user;
using GuardeSoftwareAPI.Services.userType;
using GuardeSoftwareAPI.Services.warehouse;
using Quartz;
using GuardeSoftwareAPI.Jobs;
using GuardeSoftwareAPI.Services.phone;
using GuardeSoftwareAPI.Services.communication;
using GuardeSoftwareAPI.Services.billingType;
using GuardeSoftwareAPI.Services.monthlyIncrease;
using GuardeSoftwareAPI.Services.statistics;
using GuardeSoftwareAPI.Services.auth;

using GuardeSoftwareAPI.Auth;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Globalization;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200", "http://filgueira.ar", "http://200.58.127.32") 
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// DbContext para Identity (usa la connection string del appsettings.json)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppIdentityDbContext>(options =>
    options.UseSqlServer(connectionString));

// Configuración de Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppIdentityDbContext>()
.AddDefaultTokenProviders();

// ---------- JWT AUTHENTICATION ----------

var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtOptions>(jwtSection);

var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("JWT Key is missing");
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateLifetime = true
        };
    });

// Registramos el generador de tokens
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddScoped<AccessDB>();
builder.Services.AddScoped<DaoUser>();
//SERVICES
builder.Services.AddScoped<IAccountMovementService, AccountMovementService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IBillingTypeService, BillingTypeService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IClientIncreaseRegimenService, ClientIncreaseRegimenService>();
builder.Services.AddScoped<ICommunicationService, CommunicationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IIncreaseRegimenService, IncreaseRegimenService>();
builder.Services.AddScoped<ILockerService, LockerService>();
builder.Services.AddScoped<ILockerTypeService, LockerTypeService>();
builder.Services.AddScoped<IMonthlyIncreaseService, MonthlyIncreaseService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaymentMethodService, PaymentMethodService>();
builder.Services.AddScoped<IPhoneService, PhoneService>();
builder.Services.AddScoped<IRentalService, RentalService>();
builder.Services.AddScoped<IRentalAmountHistoryService, RentalAmountHistoryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserTypeService, UserTypeService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<DaoStatistics>();
builder.Services.AddScoped<IAuthService, AuthService>();

// --- Configuration Quartz.NET ---
builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();

    // --- Job 1: ApplyDebitsJob (with trigger) ---
    var applyDebitsJobKey = new JobKey("ApplyDebitsJob");
    q.AddJob<ApplyDebitsJob>(opts => opts.WithIdentity(applyDebitsJobKey));
    q.AddTrigger(opts => opts
        .ForJob(applyDebitsJobKey)
        .WithIdentity("ApplyDebits-Trigger")
        .WithCronSchedule("0 0 4 1 * ?") // 4:00 AM del día 1 de cada mes
    );

    // --- Job 2: ApplyRentIncreaseJob (with trigger) ---
    // var applyRentIncreaseJobKey = new JobKey("ApplyRentIncreaseJob");
    // q.AddJob<ApplyRentIncreaseJob>(opts => opts.WithIdentity(applyRentIncreaseJobKey));
    // q.AddTrigger(opts => opts
    //     .ForJob(applyRentIncreaseJobKey)
    //     .WithIdentity("ApplyRentIncreaseJob-trigger")
    //     .WithCronSchedule("0 58 16 * * ?") // 16:58 (4:58 PM)
    // );

    // --- Job 3: ApplyInterestsJob (with trigger) ---
    var applyInterestsJobKey = new JobKey("ApplyInterestsJob");
    q.AddJob<ApplyInterestsJob>(opts => opts.WithIdentity(applyInterestsJobKey));
    q.AddTrigger(opts => opts
        .ForJob(applyInterestsJobKey)
        .WithIdentity("ApplyInterestsJob-trigger")
        .WithCronSchedule("0 0 4 10 * ?") // 4:00 AM del día 10 de cada mes
    );

    var applyIncreasesJobKey = new JobKey("ApplyMonthlyIncreasesJob");
                q.AddJob<ApplyMonthlyIncreasesJob>(opts => opts.WithIdentity(applyIncreasesJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(applyIncreasesJobKey)
                    .WithIdentity("ApplyMonthlyIncreasesJob-Trigger")
                    // Cron: 0 0 1 1 * ? -> (3:00 AM del día 1 de cada mes)
                    .WithCronSchedule("0 0 3 1 * ?") 
                );

    // --- Job 4: SendCommunicationJob (durable, no trigger) ---
    var sendCommJobKey = new JobKey(nameof(SendCommunicationJob));
    q.AddJob<SendCommunicationJob>(opts => opts
        .WithIdentity(sendCommJobKey)
        .StoreDurably() // <-- THE FIX IS HERE
    );
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

var cultureInfo = new CultureInfo("es-AR");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();


app.Run();
