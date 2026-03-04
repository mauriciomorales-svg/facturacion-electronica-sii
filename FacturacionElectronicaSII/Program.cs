using FacturacionElectronicaSII.Interfaces;
using FacturacionElectronicaSII.Services;
using FacturacionElectronicaSII.Services.Mock;
using FacturacionElectronicaSII.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configuración de CORS para permitir llamadas desde frontends
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.AllowAnyOrigin()      // Permite cualquier dominio (React, Angular, Vue, etc.)
              .AllowAnyMethod()      // Permite GET, POST, PUT, DELETE, etc.
              .AllowAnyHeader();     // Permite cualquier header
    });

    // Política más restrictiva para producción (opcional)
    options.AddPolicy("ProduccionSegura", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",      // React dev
                "http://localhost:4200",      // Angular dev
                "http://localhost:5173",      // Vite dev
                "http://localhost:8080",      // Vue dev
                "https://tu-dominio.cl"       // Tu dominio en producción
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "API Facturación Electrónica SII",
        Version = "v1",
        Description = "Servicio de facturación electrónica para Chile (SII)"
    });

    // Incluir comentarios XML para Swagger
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configuración de servicios según ambiente
var ambiente = builder.Configuration["FacturacionElectronica:Ambiente"] ?? "Mock";

if (ambiente == "Mock")
{
    // Servicios Mock para desarrollo sin conexión real con el SII
    builder.Services.AddScoped<ISIIService, MockSIIService>();
    builder.Services.AddScoped<IFirmaService, MockFirmaService>();
    // ✅ CAMBIO: Usar CAF REAL desde archivos (sin MySQL) incluso en modo Mock
    builder.Services.AddScoped<ICAFService, CAFService>();
}
else
{
    // Servicios reales para Certificación y Producción
    builder.Services.AddScoped<ISIIService, SIIService>();
    builder.Services.AddScoped<IFirmaService, FirmaService>();
    builder.Services.AddScoped<ICAFService, CAFService>();
}

// Servicios Core (siempre se usan)
builder.Services.AddScoped<ITEDService, TEDService>();
builder.Services.AddScoped<IXMLBuilderService, XMLBuilderService>();
builder.Services.AddScoped<IDTEService, DTEService>();
builder.Services.AddScoped<ILibroService, LibroService>();
builder.Services.AddScoped<IRCOFService, RCOFService>();

// Servicio de validación OPCIONAL (para debugging y validaciones extra)
builder.Services.AddScoped<IValidacionService, ValidacionService>();

// Servicio de generación de PDFs (representación impresa)
builder.Services.AddScoped<IPDFService, PDFService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Facturación Electrónica SII v1");
    });
}

// Middleware de manejo de excepciones global
app.UseMiddleware<ExceptionMiddleware>();

// Habilitar CORS (DEBE ir antes de UseAuthorization)
// Usar "PermitirTodo" en desarrollo, "ProduccionSegura" en producción
app.UseCors("PermitirTodo");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
