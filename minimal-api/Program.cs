using Microsoft.AspNetCore.SignalR;
using Microsoft.Net.Http.Headers;


var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

//EU
app.MapPost("/login", (MinimalApi.DTOs;.LoginDTO loginDTO) => {
  if(loginDTO.Email == "adm@teste.com" && loginDTO.Senha == "123456")
    return Results.Ok("Login com sucesso");
  else 
    return Results.Unauthorized();
});

app.Run();

