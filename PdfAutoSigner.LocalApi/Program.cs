// PdfAutoSigner signs PDF files automatically using a hardware security module. 
// Copyright (C) Plant An App
//  
// This program is free software: you can redistribute it and/or modify 
// it under the terms of the GNU Affero General Public License as 
// published by the Free Software Foundation, either version 3 of the 
// License, or (at your option) any later version. 
//  
// This program is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY

using Microsoft.Extensions.Hosting.WindowsServices;
using PdfAutoSigner.Lib.Signatures;
using PdfAutoSigner.Lib.Signers;
using PdfAutoSigner.LocalApi.Config;
using PdfAutoSigner.LocalApi.Helpers;
using PdfAutoSigner.LocalApi.Middlewares;
using PdfAutoSigner.LocalApi.Services;
using System.Diagnostics;
using System.Reflection;

//var options = new WebApplicationOptions
//{
//    Args = args,
//    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
//};
//var builder = WebApplication.CreateBuilder(options);
using var processModule = Process.GetCurrentProcess().MainModule;
var options = new WebApplicationOptions
{
    Args = args,
    //ContentRootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    ContentRootPath = Path.GetDirectoryName(processModule?.FileName)
};
var builder = WebApplication.CreateBuilder(options);

// Add support to be able to run as Windows service or Linux \ Mac daemon
//builder.Host.UseWindowsService().UseSystemd();

// Add configuration settings
builder.Configuration.AddJsonFile("hostsettings.json", optional: true);
builder.Configuration.AddJsonFile("tokensettings.json", optional: true);

// Enable logging to files
builder.Logging.AddLog4Net();

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Add services to the container.
builder.Services.Configure<TokenOptions>(builder.Configuration.GetSection(TokenOptions.TokensConfigPath));
builder.Services.AddTransient<IOSHelper, OSHelper>();
builder.Services.AddTransient<ITokenConfigService, TokenConfigService>();
builder.Services.AddTransient<ISignatureFactory, SignatureFactory>();
builder.Services.AddTransient<IDocAutoSigner, PdfDocAutoSigner>();
builder.Services.AddTransient<ISignaturesProviderService, SignaturesProviderService>();
builder.Services.AddTransient<ISignerService, SignerService>();
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
// DO NOT USE HTTPS - It will cause issues regarding missing certificates.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.UseCors();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
