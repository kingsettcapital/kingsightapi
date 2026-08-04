// Target framework for the code: .NET 8.0 this is not in use.
// .NET > 5.0 uses program.cs not startup.cs but this is a legacy project and uses startup.cs
//using kingsightapi.Configuration;
//using kingsightapi.Services;
//using log4net;
//using log4net.Config;
//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.AspNetCore.Http.Features;
//using Microsoft.IdentityModel.Tokens;
//using Microsoft.OpenApi.Models;
//using System.Configuration;
//using System.Net;
//using System.Security.Claims;
//using System.Text;

//namespace kingsightapi
//{
//    public class Startup
//    {
//        private static readonly ILog log = LogManager.GetLogger(typeof(Startup));
//        public Startup(IConfiguration configuration)
//        {
//            Configuration = configuration;
//            var log4netConfigPath = configuration.GetSection("log4netConfigFile")?.Value;
//            if (string.IsNullOrWhiteSpace(log4netConfigPath))
//            {
//                throw new InvalidOperationException("log4netConfigFile is not configured in appsettings.");
//            }
//            XmlConfigurator.Configure(new FileInfo(log4netConfigPath));
//            log.Info("Startup constructor called.");
//        }

//        public IConfiguration Configuration { get; }

//        public void ConfigureServices(IServiceCollection services)
//        {
//            services.AddControllers();
//            services.AddScoped<IDBService, DBService>();
//            services.AddScoped<ILoanService, LoanService>();
//            services.AddScoped<IInvestorService, InvestorService>();
//            services.AddScoped<ICapitalInvestorService, CapitalInvestorService>();
//            services.AddScoped<IFundService, FundService>();
//            services.AddScoped<IInvestorPortalService, InvestorPortalService>();
//            services.AddScoped<IInvestorAliasService, InvestorAliasService>();
//            services.AddScoped<IFundPortalService, FundPortalService>();
//            services.AddScoped<IPropertyPortalService, PropertyPortalService>();
//            services.AddScoped<IPortalFilterService, PortalFilterService>();

//            services.AddScoped<IGlobalSearchService, GlobalSearchService>();
//            services.AddScoped<IDashboardService, DashboardService>();
//            services.AddScoped<ILoanSecurityValueService, LoanSecurityValueService>();
//            services.AddScoped<IOtherCostCaptureService, OtherCostCaptureService>();
//            services.AddScoped<ILoanFormService, LoanFormService>();
//            services.Configure<CmhcUploadOptions>(Configuration.GetSection(CmhcUploadOptions.SectionName));
//            services.Configure<FormOptions>(options =>
//            {
//                options.MultipartBodyLengthLimit = 52_428_800;
//            });
//            services.AddScoped<ICmhcFileStorage, LocalCmhcFileStorage>();
//            services.AddScoped<ICmhcUploadService, CmhcUploadService>();



//            services.AddCors(options =>
//            {
//                options.AddPolicy("cors_policy", policy => policy
//                    .WithOrigins(
//                        "http://localhost:4200",
//                        "https://kingsight.kingsettcapital.com",
//                        "https://kingsightdev.kingsettcapital.com",
//                        "https://kingsightuat.kingsettcapital.com",
//                        "https://kingsightdevapi.kingsettcapital.com",
//                        "https://kingsightuatapi.kingsettcapital.com",
//                        "https://kingsightapi.kingsettcapital.com",
//                        "https://login.kingsettcapital.com",
//                        "https://login.microsoftonline.com")
//                    .AllowAnyHeader()
//                    .AllowAnyMethod()
//                    .AllowCredentials());
//            });
//            log.Info("CORS policy configured with allowed origins.");

//            services.AddHttpClient();
//            //services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
//            //{
//            //    options.TokenValidationParameters = new TokenValidationParameters
//            //    {
//            //        ValidateIssuerSigningKey = true,
//            //        IssuerSigningKey = new SymmetricSecurityKey(
//            //        Encoding.ASCII.GetBytes(
//            //            Configuration.GetSection("AppSettings:Token").Value 
//            //            ?? throw new InvalidOperationException("JWT token secret is not configured in AppSettings:Token")
//            //            )
//            //        ),
//            //        ValidateIssuer = false,
//            //        ValidateAudience = false,
//            //        ValidateLifetime = true,
//            //        LifetimeValidator = (DateTime? notBefore, DateTime? expires, SecurityToken securityToken, TokenValidationParameters validationParameters) =>
//            //        {
//            //            if (expires != null)
//            //            {
//            //                return expires > DateTime.UtcNow;
//            //            }
//            //            return false;
//            //        }
//            //    };
//            //    /* added a comments */
//            //    //options.Events = new JwtBearerEvents()
//            //    //{
//            //    //    OnMessageReceived = context =>
//            //    //    {
//            //    //        var accessToken = context.Request.Query["access_token"];
//            //    //        // If the request is for our hub...
//            //    //        var path = context.HttpContext.Request.Path;
//            //    //        if (!string.IsNullOrEmpty(accessToken) &&
//            //    //            (path.StartsWithSegments("/hub")))
//            //    //        {
//            //    //            if (!string.IsNullOrWhiteSpace(accessToken))
//            //    //            {
//            //    //                context.Request.Headers.Append("Authorization", "Bearer " + accessToken);
//            //    //            }
//            //    //        }
//            //    //        return Task.CompletedTask;
//            //    //    },
//            //    //    OnAuthenticationFailed = async context =>
//            //    //    {
//            //    //        //context.Response.AddApplicationError("invaild token");
//            //    //        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
//            //    //        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
//            //    //        context.Response.Headers["Access-Control-Allow-Methods"] = "*";
//            //    //        context.Response.Headers["Access-Control-Allow-Headers"] = "*";
//            //    //        context.Response.Headers["Access-Control-Expose-Headers"] = "invaild token";
//            //    //        context.Response.Headers["Access-Control-Max-Age"] = "86400";
//            //    //        context.Response.Headers["content-type"] = "application/json; charset=utf-8";
//            //    //        context.Response.Headers["access-control-allow-credentials"] = "true";
//            //    //        //string json = JsonSerializer.Serialize(new { status = "fail", message = "Invalid token or it has expired, Please login again to get access!" });
//            //    //        //await context.Response.WriteAsync(json);
//            //    //    },
//            //    //    OnTokenValidated = async context =>
//            //    //    {

//            //    //        var userid = context.Principal.Claims.Where(x => x.Type == ClaimTypes.NameIdentifier).FirstOrDefault().Value;
//            //    //        string email = context.Principal.Claims.Where(x => x.Type == ClaimTypes.Email).FirstOrDefault()?.Value;
//            //    //        if (string.IsNullOrEmpty(email))
//            //    //            email = context.Principal.Claims.Where(x => x.Type == ClaimTypes.Upn).FirstOrDefault()?.Value;
//            //    //        string message = string.Empty;
//            //    //        //var adminUser = email != null ? await Extension.IsUserExistWithEmail(email, conectionString) : null;
//            //    //        //bool userAccess = false;
//            //    //        //if (adminUser == null)
//            //    //        //    userAccess = await Extension.IsUserExist(userid, conectionString);
//            //    //        //if (!userAccess && adminUser == null) // !userAccess && 
//            //    //        //    message = "You have no longer access to this application, please contact to admin to get access!";
//            //    //        //else if (adminUser == null && await Extension.IsPasswordChangeAfter(userid, context.SecurityToken.ValidFrom, conectionString))
//            //    //        //{
//            //    //        //    message = "You recently changed your password, please login again to get access!";
//            //    //        //    context.Response.Cookies.Delete("auth-key");
//            //    //        //}
//            //    //        if (message != "" || message.Length > 0)
//            //    //        {
//            //    //            //context.Response.AddApplicationError("invaild token");
//            //    //            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
//            //    //            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
//            //    //            context.Response.Headers["Access-Control-Allow-Methods"] = "*";
//            //    //            context.Response.Headers["Access-Control-Allow-Headers"] = "*";
//            //    //            context.Response.Headers["Access-Control-Expose-Headers"] = "invaild token";
//            //    //            context.Response.Headers["Access-Control-Max-Age"] = "86400";
//            //    //            context.Response.Headers["content-type"] = "application/json; charset=utf-8";
//            //    //            context.Response.Headers["access-control-allow-credentials"] = "true";
//            //    //            // string json = JsonSerializer.Serialize(new { status = "fail", message});
//            //    //            // await context.Response.WriteAsync(json);
//            //    //        }
//            //    //        //else if (adminUser != null)
//            //    //        //{
//            //    //        //    List<Claim> claims = context.Principal.Claims.ToList();
//            //    //        //    claims.Remove(claims.First(x => x.Type == ClaimTypes.NameIdentifier));
//            //    //        //    claims.Add(new Claim(ClaimTypes.NameIdentifier, adminUser.UserId.ToString()));
//            //    //        //    claims.Remove(claims.First(x => x.Type == ClaimTypes.Name));
//            //    //        //    claims.Add(new Claim(ClaimTypes.Name, adminUser.UserTypeStatus));
//            //    //        //    var userIdentity = new ClaimsIdentity(claims, ClaimTypes.Name);
//            //    //        //    context.Principal = new ClaimsPrincipal(userIdentity);
//            //    //        //}
//            //    //    },
//            //    //};
//            //});
//            services.AddSwaggerGen(options =>
//            {
//                options.SwaggerDoc("v1", new OpenApiInfo { Title = "KingSight API", Version = "v1" });
//                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
//                {
//                    In = ParameterLocation.Header,
//                    Description = "Please enter a valid JWT token",
//                    Name = "Authorization",
//                    Type = SecuritySchemeType.Http,
//                    Scheme = "Bearer"
//                });

//                //options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
//                //{
//                //    Description = "API Key authentication using the X-Api-Key header",
//                //    Name = "X-Api-Key",
//                //    In = ParameterLocation.Header,
//                //    Type = SecuritySchemeType.ApiKey,
//                //});

//                //options.AddSecurityRequirement(new OpenApiSecurityRequirement
//                //{
//                //    {
//                //        new OpenApiSecurityScheme
//                //        {
//                //            Reference = new OpenApiReference
//                //            {
//                //                Type = ReferenceType.SecurityScheme,
//                //                Id = "Bearer"
//                //            }
//                //        },
//                //        new string[] { }
//                //    }
//                //});

//                //// --- NEW: API Key Security Requirement for Swagger UI ---
//                //// This tells Swagger to apply the "ApiKey" security to your operatifons.
//                //options.AddSecurityRequirement(new OpenApiSecurityRequirement
//                //{
//                //    {
//                //        new OpenApiSecurityScheme
//                //        {
//                //            Reference = new OpenApiReference
//                //            {
//                //                Type = ReferenceType.SecurityScheme,
//                //                Id = "ApiKey" // <--- Must match the Id used in AddSecurityDefinition("ApiKey", ...)
//                //            }
//                //        },
//                //        new string[] { } // No specific scopes required for API Key
//                //    }
//                //});
//            });
//        }

//        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
//        {
//            if (env.IsDevelopment())
//            {
//                app.UseDeveloperExceptionPage();
//            }

//            app.UseSwagger();
//            app.UseSwaggerUI(c =>
//            {
//                c.SwaggerEndpoint("/swagger/v1/swagger.json", "KingSight API v1");
//            });

//            app.UseRouting();
//            app.UseCors("cors_policy");

//            app.UseAuthentication();
//            app.UseAuthorization();
//            log.Info($"Project Started At: {DateTime.Now.ToString("yyyy-MM-dd")}");

//            app.UseEndpoints(endpoints =>
//            {
//                endpoints.MapControllers();
//            });
//        }
//    }
//}