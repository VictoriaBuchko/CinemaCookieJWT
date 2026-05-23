using CinemaBooking.DTOs;
using CinemaBooking.Models;
using CinemaBooking.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//сервіси
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<List<Booking>>();
builder.Services.AddSingleton<List<MovieShow>>();

builder.Services.AddEndpointsApiExplorer();

//swagger з підтримкою JWT
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Введіть JWT токен"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});

//cookie автентифікація
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "CinemaAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
    })
    //jwt автентифікація
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT автентифікація не вдалась: {Error}",
                    context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                var name = context.Principal?.FindFirst(ClaimTypes.Name)?.Value;
                logger.LogInformation("JWT токен валідний для користувача: {Name}", name);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

var movieShows = app.Services.GetRequiredService<List<MovieShow>>();
movieShows.AddRange(new[]
{
    new MovieShow { Id = 1, Title = "Inception", StartTime = DateTime.Now.AddHours(1), Duration = TimeSpan.FromHours(2), AvailableSeats = 100 },
    new MovieShow { Id = 2, Title = "The Matrix", StartTime = DateTime.Now.AddHours(3), Duration = TimeSpan.FromHours(2.5), AvailableSeats = 80 },
    new MovieShow { Id = 3, Title = "Interstellar", StartTime = DateTime.Now.AddHours(5), Duration = TimeSpan.FromHours(3), AvailableSeats = 50 }
});

//список кінопоказів
app.MapGet("/movies", (List<MovieShow> shows) =>
{
    return Results.Ok(shows);
});

//деталі кінопоказу
app.MapGet("/movies/{id:int}", (int id, List<MovieShow> shows) =>
{
    var show = shows.FirstOrDefault(s => s.Id == id);
    return show is null ? Results.NotFound("Кінопоказ не знайдено") : Results.Ok(show);
});

//реєстрація через cookie
app.MapPost("/auth/register", async (RegisterRequest request, IUserService userService, HttpContext context) =>
{
    var user = userService.Register(request.Email, request.Password, request.Name);
    if (user is null)
        return Results.BadRequest(new AuthResponse(false, "Email вже зареєстровано"));

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Name),
        new Claim(ClaimTypes.Email, user.Email)
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

    return Results.Ok(new AuthResponse(true, "Реєстрація успішна"));
});

//логін через cookie
app.MapPost("/auth/login", async (LoginRequest request, IUserService userService, HttpContext context) =>
{
    var user = userService.Authenticate(request.Email, request.Password);
    if (user is null)
        return Results.BadRequest(new AuthResponse(false, "Невірний email або пароль"));

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Name),
        new Claim(ClaimTypes.Email, user.Email)
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

    return Results.Ok(new AuthResponse(true, "Вхід успішний"));
});

//вихід
app.MapPost("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new AuthResponse(true, "Вихід успішний"));
});

//бронювання користувача cookie
app.MapGet("/bookings", [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
(ClaimsPrincipal user, List<Booking> bookings, List<MovieShow> shows) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var userBookings = bookings
        .Where(b => b.UserId == userId)
        .Select(b => new BookingResponse(
            b.Id,
            b.UserId,
            b.MovieShowId,
            shows.FirstOrDefault(s => s.Id == b.MovieShowId)?.Title ?? "",
            b.NumberOfSeats,
            b.BookingTime))
        .ToList();

    return Results.Ok(userBookings);
});

//створення бронювання cookie
app.MapPost("/bookings/create", [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
(CreateBookingRequest request, ClaimsPrincipal user, List<Booking> bookings, List<MovieShow> shows) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    var show = shows.FirstOrDefault(s => s.Id == request.MovieShowId);
    if (show is null)
        return Results.NotFound("Кінопоказ не знайдено");

    if (show.AvailableSeats < request.NumberOfSeats)
        return Results.BadRequest("Недостатньо вільних місць");

    show.AvailableSeats -= request.NumberOfSeats;

    var booking = new Booking
    {
        Id = bookings.Count + 1,
        UserId = userId,
        MovieShowId = request.MovieShowId,
        NumberOfSeats = request.NumberOfSeats,
        BookingTime = DateTime.Now
    };

    bookings.Add(booking);
    return Results.Ok(new BookingResponse(
        booking.Id,
        booking.UserId,
        booking.MovieShowId,
        show.Title,
        booking.NumberOfSeats,
        booking.BookingTime));
});

//реєстрація через jwt
app.MapPost("/jwt/register", (RegisterRequest request, IUserService userService, IJwtService jwtService) =>
{
    var user = userService.Register(request.Email, request.Password, request.Name);
    if (user is null)
        return Results.BadRequest(new JwtLoginResponse(false, "Email вже зареєстровано"));

    var token = jwtService.GenerateToken(user);
    return Results.Ok(new JwtLoginResponse(true, "Реєстрація успішна", token));
});

//логін через jwt
app.MapPost("/jwt/login", (LoginRequest request, IUserService userService, IJwtService jwtService) =>
{
    var user = userService.Authenticate(request.Email, request.Password);
    if (user is null)
        return Results.BadRequest(new JwtLoginResponse(false, "Невірний email або пароль"));

    var token = jwtService.GenerateToken(user);
    return Results.Ok(new JwtLoginResponse(true, "Вхід успішний", token));
});

//бронювання користувача jwt
app.MapGet("/jwt/bookings", [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
(ClaimsPrincipal user, List<Booking> bookings, List<MovieShow> shows) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var userBookings = bookings
        .Where(b => b.UserId == userId)
        .Select(b => new BookingResponse(
            b.Id,
            b.UserId,
            b.MovieShowId,
            shows.FirstOrDefault(s => s.Id == b.MovieShowId)?.Title ?? "",
            b.NumberOfSeats,
            b.BookingTime))
        .ToList();

    return Results.Ok(userBookings);
});

//створення бронювання jwt
app.MapPost("/jwt/bookings/create", [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
(CreateBookingRequest request, ClaimsPrincipal user, List<Booking> bookings, List<MovieShow> shows) =>
{
    var userId = int.Parse(user.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    var show = shows.FirstOrDefault(s => s.Id == request.MovieShowId);
    if (show is null)
        return Results.NotFound("Кінопоказ не знайдено");

    if (show.AvailableSeats < request.NumberOfSeats)
        return Results.BadRequest("Недостатньо вільних місць");

    show.AvailableSeats -= request.NumberOfSeats;

    var booking = new Booking
    {
        Id = bookings.Count + 1,
        UserId = userId,
        MovieShowId = request.MovieShowId,
        NumberOfSeats = request.NumberOfSeats,
        BookingTime = DateTime.Now
    };

    bookings.Add(booking);
    return Results.Ok(new BookingResponse(
        booking.Id,
        booking.UserId,
        booking.MovieShowId,
        show.Title,
        booking.NumberOfSeats,
        booking.BookingTime));
});

app.Run();