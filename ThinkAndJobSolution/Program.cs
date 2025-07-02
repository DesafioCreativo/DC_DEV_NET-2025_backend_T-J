using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Serialization;
using System.Text;
using ThinkAndJobSolution.AccesoDato;
using ThinkAndJobSolution.Controllers.Authorization;
using ThinkAndJobSolution.Security;
using ThinkAndJobSolution.Utils;
using ThinkAndJobSolution.Utils.Interfaces;
using WebApi.HostedServices;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
var key = Encoding.ASCII.GetBytes(builder.Configuration["AppSettings:Secret"]);

// Configuración de JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
    };
});

// Registrar servicios y configuraciones
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.AddDbContext<DataContext>();
builder.Services.AddAuthorization();

// Registrar Controladores con Vistas (necesario para TempData)
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(opt =>
    {
        opt.SerializerSettings.ContractResolver = new DefaultContractResolver();
    });

// Registrar los servicios necesarios
builder.Services.AddScoped<IJwtUtils, JwtUtils>();
builder.Services.AddScoped<IDataAccess, DataAccess>();
builder.Services.AddScoped<ICl_Encryption, Cl_Encryption>();
builder.Services.AddScoped<IAuthService, AuthService>();
//builder.Services.AddScoped<ICl_Libreria, Cl_Libreria>();
builder.Services.AddScoped<ICl_Helpers, Cl_Helpers>();

// Registrar SignalR y el Hosted Service
builder.Services.AddSignalR();
builder.Services.AddHostedService<AlertaHostedService>();

// Configuración de Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

    // Configuración del esquema de seguridad para JWT
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Autenticación JWT usando el esquema Bearer. \r\n\r\n Ingrese 'Bearer' [espacio] y luego su token en el campo de texto.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// Asegúrate de habilitar Swagger en cualquier entorno
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
});

// Configuración de CORS
app.UseCors(x => x
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// global error handler
app.UseMiddleware<ErrorHandlerMiddleware>();
// custom jwt auth middleware
app.UseMiddleware<JwtMiddleware>();

// Registrar SignalR Hub
app.MapHub<AlertaHub>("/hubmodulo");

// Usar redirección HTTPS si es necesario
app.UseHttpsRedirection();
// Usar la autenticación y autorización en la aplicación
app.UseAuthentication();
app.UseAuthorization();

// Configuración de rutas
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Web}/{action=Index}/{id?}"
);

app.UseStaticFiles();
app.Run();

