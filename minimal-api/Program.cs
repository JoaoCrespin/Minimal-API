using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using minimal_api.Dominio.Entidades;
using minimal_api.Dominio.Interfaces;
using MinimalApi.Dominio.Entidades;
using MinimalApi.Dominio.Enuns;
using MinimalApi.Dominio.Interfaces;
using MinimalApi.Dominio.ModelViews;
using MinimalApi.Dominio.Servicos;
using MinimalApi.DTOs;
using MinimalApi.Infraestrutura.Db;

#region Builder

var builder = WebApplication.CreateBuilder(args);
//eu
var key = builder.Configuration.GetSection("Jwt").ToString();
if(string.IsNullOrEmpty(key)) key = "123456";

builder.Services.AddAuthentication(option => {
  option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
  option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(option =>{
  option.TokenValidationParameters = new TokenValidationParameters {
    ValidateLifetime = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
    ValidateIssuer = false,
    ValidateAudience = false,
  };
});

builder.Services.AddAuthorization();


builder.Services.AddScoped<IAdministradorServico, AdministradorServico>();
builder.Services.AddScoped<iVeiculoServico, VeiculoServico>();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
  options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme 
  {
    Name = "Authorization",
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "Insira o token JWT aqui"
  });

  options.AddSecurityRequirement
  (
    new OpenApiSecurityRequirement 
    { 
      { 
        new OpenApiSecurityScheme 
        { 
          Reference = new OpenApiReference 
          { 
            Type = ReferenceType.SecurityScheme, 
            Id = "Bearer" 
          } 
        }, 
        new string[] {} 
      }

  });

});

//eu
builder.Services.AddDbContext<DbContexto>(options => {
  options.UseMySql(
    builder.Configuration.GetConnectionString("mysql"),
    ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("mysql"))
    );
});

var app = builder.Build();
#endregion

#region Home

app.MapGet("/", () => Results.Json(new Home())).AllowAnonymous().WithTags("Home");

#endregion

#region Administradores
//EU
string GerarTokenJwt (Administrador administrador){
  if(string.IsNullOrEmpty(key)) return string.Empty;

  var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
  var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

  var claims = new List<Claim>()
  {
    new Claim("Email", administrador.Email),
    new Claim("Perfil", administrador.Perfil),
    new Claim(ClaimTypes.Role, administrador.Perfil),
    
  };

  var token = new JwtSecurityToken(
    claims: claims,
    expires: DateTime.Now.AddDays(1),
    signingCredentials: credentials
  );

  return new JwtSecurityTokenHandler().WriteToken(token);
};

app.MapPost("/administradores/login", ([FromBody] LoginDTO loginDTO, IAdministradorServico administradorServico) => {
  var adm = administradorServico.Login(loginDTO);
  if(adm != null)
  {
    string token = GerarTokenJwt(adm);
    return Results.Ok(new AdministradorLogado{
      Email = adm.Email,
      Perfil = adm.Perfil,
      Token = token
    });
  }
  else 
    return Results.Unauthorized();
}).AllowAnonymous().WithTags("Administradores");

app.MapPost("/administradores", ([FromBody] AdministradorDTO administradorDTO, IAdministradorServico administradorServico) => {
  var validacao = new ErrosDeValidacao{
    Mensagens = new List<string>()
  };
if(string.IsNullOrEmpty(administradorDTO.Email))
  validacao.Mensagens.Add("Email não pode ser vazio.");
if(string.IsNullOrEmpty(administradorDTO.Senha))
  validacao.Mensagens.Add("Senha não pode ser vaziaS.");
if(administradorDTO.Perfil == null)
  validacao.Mensagens.Add("Perfil não pode ser vazio.");

  if(validacao.Mensagens.Count > 0)
  {
    return Results.BadRequest(validacao);
  }
  var veiculo = new Administrador{
    Email = administradorDTO.Email,
    Senha = administradorDTO.Senha,
    Perfil = administradorDTO.Perfil?.ToString() ?? Perfil.Editor.ToString()
  };
  administradorServico.Incluir(veiculo);

  return Results.Created($"/administrador/{veiculo.Id}", veiculo);
}).RequireAuthorization().RequireAuthorization(new AuthorizeAttribute {Roles = "Adm"}).WithTags("Administradores");

app.MapGet("/administradores", ([FromQuery] int? pagina, IAdministradorServico administradorServico) => {
  var adms = new List<AdministradorModelView>();
  var administradores=administradorServico.Todos(pagina);
  foreach (var adm in administradores)
  {
    adms.Add(new AdministradorModelView{
      Id = adm.Id,
      Email = adm.Email,
      Perfil = (Perfil)Enum.Parse(typeof(Perfil), adm.Perfil),
    });
  }
  
  return Results.Ok(adms);
}).RequireAuthorization().RequireAuthorization(new AuthorizeAttribute {Roles = "Adm"}).WithTags("Administradores");

app.MapGet("/administradores/{id}", ([FromRoute] int id, IAdministradorServico administradorServico) => {
  var administrador = administradorServico.BuscaPorId(id);

  if(administrador == null) return Results.NotFound();

  return Results.Ok(administrador);
}).RequireAuthorization().RequireAuthorization(new AuthorizeAttribute {Roles = "Adm"}).WithTags("Administradores");
#endregion

#region Veiculos

ErrosDeValidacao validaDTO(VeiculoDTO veiculoDTO)
{
  var validacao = new ErrosDeValidacao{
    Mensagens = new List<string>()
};
  if(string.IsNullOrEmpty(veiculoDTO.Nome))
  {
    validacao.Mensagens.Add("O nome não pode ser vazio.");
  }
  if(string.IsNullOrEmpty(veiculoDTO.Marca))
  {
    validacao.Mensagens.Add("a marca não pode ficar em branco.");
  }
  if(veiculoDTO.Ano < 1950)
  {
    validacao.Mensagens.Add("Veículo muito antigo, aceito somente anos superiores a 1950.");
  }
  return validacao;
}

app.MapPost("/veiculos", ([FromBody] VeiculoDTO veiculoDTO, iVeiculoServico veiculoServico) => {

  
  var validacao = validaDTO(veiculoDTO);
  if(validacao.Mensagens.Count > 0)
  {
    return Results.BadRequest(validacao);
  }

  var veiculo = new Veiculo{
    Nome = veiculoDTO.Nome,
    Marca = veiculoDTO.Marca,
    Ano = veiculoDTO.Ano
  };
  veiculoServico.Incluir(veiculo);

  return Results.Created($"/veiculo/{veiculo.Id}", veiculo);
}).RequireAuthorization().RequireAuthorization(new AuthorizeAttribute {Roles = "Adm,Editor"}).WithTags("Veiculo"); // Cria um veículo

app.MapGet("/veiculos", ([FromQuery] int? pagina, iVeiculoServico veiculoServico) => {
  var veiculos = veiculoServico.Todos(pagina);
  return Results.Ok(veiculos);
}).RequireAuthorization().WithTags("Veiculo"); //Lista de veículos

app.MapGet("/veiculos/{id}", ([FromRoute] int id, iVeiculoServico veiculoServico) => {
  var veiculo = veiculoServico.BuscaPorId(id);

  if(veiculo == null) return Results.NotFound();

  return Results.Ok(veiculo);
}).RequireAuthorization().RequireAuthorization(new AuthorizeAttribute {Roles = "Adm,Editor"}).WithTags("Veiculo"); //Retorna um veículo específico

app.MapPut("/veiculos/{id}", ([FromRoute] int id, VeiculoDTO veiculoDTO, iVeiculoServico veiculoServico) => {

  var veiculo = veiculoServico.BuscaPorId(id);
  if(veiculo == null) return Results.NotFound(); 
 
  var validacao = validaDTO(veiculoDTO);
  if(validacao.Mensagens.Count > 0)
  {
    return Results.BadRequest(validacao);
  }

  veiculo.Nome = veiculoDTO.Nome;
  veiculo.Marca = veiculoDTO.Marca;
  veiculo.Ano = veiculoDTO.Ano;

  veiculoServico.Atualizar(veiculo);

  return Results.Ok(veiculo);
}).RequireAuthorization().RequireAuthorization(new AuthorizeAttribute {Roles = "Adm"}).WithTags("Veiculo"); //Alterar veiculo

app.MapDelete("/veiculos/{id}", ([FromRoute] int id, iVeiculoServico veiculoServico) => {
  var veiculo = veiculoServico.BuscaPorId(id);
  if(veiculo == null) return Results.NotFound();

  veiculoServico.Apagar(veiculo);

  return Results.NoContent();
}).RequireAuthorization().RequireAuthorization(new AuthorizeAttribute {Roles = "Adm"}).WithTags("Veiculo"); //Apaga um veículo

#endregion

#region App
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization(); //Ambos para JTW

app.Run();
#endregion
