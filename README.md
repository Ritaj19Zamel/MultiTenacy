# 🏗️ Multi‑Tenancy API with Audit Logging (ASP.NET Core + EF Core)

This repository demonstrates a **multi‑tenant ASP.NET Core Web API** that uses **Entity Framework Core** for data access, **per‑tenant connection strings**, and **audit logging** captured by an EF Core `SaveChangesInterceptor`.

---

## ✨ Features

- **Row‑level tenant isolation** using EF Core global query filters (`TenantId` on entities that implement `IMustHaveTenant`)
- **Per‑tenant connection strings** read from `appsettings.json`
- **Audit logging** for `Insert`, `Update`, and `Delete` operations
- **CRUD endpoints** for `Product`
- **Swagger UI** out of the box

Tenant is selected via an HTTP header:

```http
tenant: devcreed
```

---

## 🧰 Tech Stack

- .NET 7/8
- ASP.NET Core Web API
- Entity Framework Core (SQL Server provider)
- Swashbuckle (Swagger)

---

## 📂 Project Highlights (what to look at)

- `Data/ApplicationDbContext.cs`: multi‑tenant `DbContext`, global filters, and `SaveChangesAsync` hook to stamp `TenantId`
- `Interceptors/AuditInterceptor.cs`: collects entity changes and writes to `AuditLogs` safely after the main save
- `Services/TenantService.cs`: resolves the current tenant and its connection string from request headers + configuration
- `Controllers/ProductController.cs`: sample CRUD endpoints

---

## ⚙️ Configuration

Update `appsettings.json` to list your tenants and (optionally) set a default connection string/provider.

```json
{
  "TenantSettings": {
    "Defaults": {
      "DBProvider": "mssql",
      "ConnectionString": "Data Source=(localdb)\\ProjectModels;Initial Catalog=SharedDb;Trusted_Connection=True;Encrypt=False"
    },
    "Tenants": [
      {
        "Name": "devcreed",
        "TId": "devcreed",
        "ConnectionString": "Data Source=(localdb)\\ProjectModels;Initial Catalog=DevCreedDb;Trusted_Connection=True;Encrypt=False"
      },
      {
        "Name": "microsoft",
        "TId": "microsoft",
        "ConnectionString": "Data Source=(localdb)\\ProjectModels;Initial Catalog=MicrosoftDb;Trusted_Connection=True;Encrypt=False"
      }
    ]
  }
}
```

> The active tenant is selected by sending the `tenant` header (e.g., `devcreed`, `microsoft`) on each request.

---

## ▶️ Quick Start

1) **Clone & Restore**  
```bash
git clone https://github.com/<your-username>/<your-repo>.git
cd <your-repo>
dotnet restore
```

2) **Add the initial EF Core migration** (first time only)  
```bash
dotnet tool install --global dotnet-ef   # if you don't have it
dotnet ef migrations add InitialCreate
```

3) **Apply migrations**  
Migrations run automatically for each configured tenant on startup (see `ConfigureServices.AddTenancy`).  
If you prefer manual updates:
```bash
dotnet ef database update
```

4) **Run the API**  
```bash
dotnet run
```
Open Swagger at the URL printed in the console (typically `https://localhost:<port>/swagger`).

---

## 📡 Using the API

Always include the **tenant header** in requests.

### Create Product
```http
POST /api/Product HTTP/1.1
Host: localhost:5001
tenant: devcreed
Content-Type: application/json

{
  "name": "Laptop",
  "description": "Gaming laptop",
  "rate": 5,
  "price": 1200,
  "stock": 10
}
```

### Get All Products
```http
GET /api/Product HTTP/1.1
Host: localhost:5001
tenant: devcreed
```

### Update Product
```http
PUT /api/Product/1 HTTP/1.1
Host: localhost:5001
tenant: devcreed
Content-Type: application/json

{
  "id": 1,
  "name": "Updated Laptop",
  "price": 1500,
  "stock": 8
}
```

### Delete Product
```http
DELETE /api/Product/1 HTTP/1.1
Host: localhost:5001
tenant: devcreed
```

---

## 🧾 Audit Logging

**Model:**

```csharp
public class AuditLog
{
    public int Id { get; set; }
    public string TableName { get; set; } = null!;
    public string Action { get; set; } = null!; // Insert, Update, Delete
    public string KeyValues { get; set; } = null!;
    public string OldValues { get; set; } = null!; // "{}" when not applicable
    public string NewValues { get; set; } = null!; // "{}" when not applicable
    public string TenantId { get; set; } = null!;
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**How it works:**

- In `SavingChanges`, the interceptor inspects tracked entries with states **Added / Modified / Deleted** and builds `AuditLog` objects.
- For **Added** entries, `NewValues` is populated and `OldValues` is set to `"{}"`.
- For **Deleted** entries, `OldValues` is populated and `NewValues` is set to `"{}"`.
- For **Modified** entries, both `OldValues` and `NewValues` contain only the **modified** properties.
- The logs are written in `SavedChanges`/`SavedChangesAsync` using a **fresh `ApplicationDbContext`** so we do not recurse into the same save operation.

> **Important:** The `OldValues` and `NewValues` columns are **non‑nullable** strings in the model. The interceptor ensures they are never null by defaulting to `"{}"` when not applicable. If your database schema was created before this change, make sure the columns allow non‑nulls or set a default of `'{}'`.

---

## 🧪 Multi‑Tenancy Flow

1. `TenantService` reads the `tenant` header and resolves the current tenant from configuration.
2. `ApplicationDbContext` is configured with the tenant’s **connection string**.
3. A global query filter ensures only rows with the same `TenantId` are returned for entities that implement `IMustHaveTenant`.
4. On insert, the `DbContext` stamps `TenantId` for those entities automatically.
5. `AuditInterceptor` records changes with the resolved `TenantId` and (if present) `UserId` from the current principal.

---

## 🧯 Troubleshooting

- **“SavingChanges not hit”**  
  Ensure the interceptor is registered and attached to the same `DbContext` you use:
  ```csharp
  builder.Services.AddScoped<AuditInterceptor>();
  builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
  {
      var tenantService = sp.GetRequiredService<ITenantService>();
      var interceptor = sp.GetRequiredService<AuditInterceptor>();
      options.UseSqlServer(tenantService.GetConnectionString())
             .AddInterceptors(interceptor);
  });
  ```
  Also register:
  ```csharp
  builder.Services.AddHttpContextAccessor();
  ```

- **`SqlException: Cannot insert NULL into column 'OldValues'`**  
  Your table likely has non‑nullable `OldValues`/`NewValues` but the interceptor didn’t set them. Make sure you’re using the version that sets `"{}"` for not‑applicable sides (adds/deletes) and that your migration reflects the non‑null requirement.

- **Tenant not found**  
  Verify the `tenant` header value exists under `TenantSettings:Tenants` and each tenant has a valid connection string (or falls back to the default).

---

## 🤝 Contributing

Issues and PRs are welcome!

---

## 📜 License

MIT — feel free to use this for learning or as a starting point for your own multi‑tenant API.
