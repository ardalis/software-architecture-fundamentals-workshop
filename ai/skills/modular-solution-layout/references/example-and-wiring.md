# Example Solution and Wiring

```
CorporateTraining.sln
├── src/
│   ├── CorporateTraining.Web/
│   ├── CorporateTraining.SharedKernel/
│   ├── CorporateTraining.Users/          + CorporateTraining.Users.Contracts/
│   ├── CorporateTraining.Courses/        + CorporateTraining.Courses.Contracts/
│   └── CorporateTraining.Certifications/ + CorporateTraining.Certifications.Contracts/
└── tests/
    ├── CorporateTraining.{Users,Courses,Certifications}.Tests/
    └── CorporateTraining.IntegrationTests/
```

- **Users:** registration, authentication, RBAC. Others never read user tables; they use `Users.Contracts` for lookups and identity checks.
- **Courses:** courses, learning paths, instructors, content. Refers to users by ID plus `Users.Contracts` queries.
- **Certifications:** issuing/tracking, expiry and compliance, renewals. Consumes course and user data via contracts, never tables.

```csharp
// In CorporateTraining.Users (the module owns its registration)
public static class UsersModuleExtensions
{
    public static IServiceCollection AddUsersModule(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<UsersDbContext>(o =>
            o.UseSqlServer(config.GetConnectionString("Default")));
        services.AddScoped<IUserService, UserService>(); // IUserService from Contracts
        return services;
    }
}

// CorporateTraining.Web/Program.cs (composition root)
builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddCoursesModule(builder.Configuration);
builder.Services.AddCertificationsModule(builder.Configuration);
```
