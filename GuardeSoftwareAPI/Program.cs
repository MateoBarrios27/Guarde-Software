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
using GuardeSoftwareAPI.Services.whareHouse;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddScoped<AccessDB>();
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
builder.Services.AddScoped<IWarehouseService, WarehouseService>();

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
