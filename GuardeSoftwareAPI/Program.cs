using GuardeSoftwareAPI.Dao;
using System.Data;
using GuardeSoftwareAPI.Services.AccountMovement;
using GuardeSoftwareAPI.Services.ActivityLog;
using GuardeSoftwareAPI.Services.Address;
using GuardeSoftwareAPI.Services.Client;
using GuardeSoftwareAPI.Services.ClientIncreaseRegimen;
using GuardeSoftwareAPI.Services.Email;
using GuardeSoftwareAPI.Services.IncreaseRegimen;
using GuardeSoftwareAPI.Services.Locker;
using GuardeSoftwareAPI.Services.LockerType;
using GuardeSoftwareAPI.Services.Payment;
using GuardeSoftwareAPI.Services.PaymentMethod;
using GuardeSoftwareAPI.Services.Rental;
using GuardeSoftwareAPI.Services.RentalAmountHistory;
using GuardeSoftwareAPI.Services.User;
using GuardeSoftwareAPI.Services.UserType;
using GuardeSoftwareAPI.Services.WhareHouse;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//SERVICES
builder.Services.AddScoped<IAccountMovementService, AccountMovementService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IClientIncreaseRegimenService, ClientIncreaseRegimenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IIncreaseRegimenService, IncreaseRegimenService>();
builder.Services.AddScoped<ILockerService, LockerService>();
builder.Services.AddScoped<ILockerTypeService, LockerTypeService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaymentMethodService, PaymentMethodService>();
builder.Services.AddScoped<IRentalService, RentalService>();
builder.Services.AddScoped<IRentalAmountHistoryService, RentalAmountHistoryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserTypeService, UserTypeService>();
builder.Services.AddScoped<IWhareHouseService, WhareHouseService>();

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
