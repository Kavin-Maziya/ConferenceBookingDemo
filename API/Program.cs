using Microsoft.AspNetCore.Mvc.Diagnostics;
using Scalar.AspNetCore; 
 
 //Phase 1 : Builder - Register the services into the app
 /// Dependency injection container
 
 var builder = WebApplication.CreateBuilder(args);

 //Register Your services

 builder.Services.AddControllers(); //registering controller support
 builder.Services.AddOpenApi(); // Registering built-in OpenApi document generation


 var app = builder.Build(); //Nothing can be regsitered after this

//Phase 2: Pipeline - Configure your Middleware chain
// NB: Order matters!! 

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); 
}
app.MapControllers(); 
/*
app.MapGet("api/sesssion", async() =>
{
    await Task.Delay(100);
return Results.Ok(DummyDataStore.sesssions);
}); */

app.Run(); 