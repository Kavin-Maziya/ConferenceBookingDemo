# Instructor Demo Guide
## Conference Booking System
### Week 2, Day 1 — EF Core 10 & Database Foundations

**Estimated Duration:** ~90 minutes of content + Q&A  
**Objective:** Replace the in-memory `BookingStore` with a real PostgreSQL database backed by EF Core 10. Trainees will understand the ORM mental model, configure a `DbContext`, create entity classes, configure them with the Fluent API, generate and apply their first migration, and replace every in-memory CRUD operation with an async EF Core equivalent.

---

## Phase 0 – Week 1 Recap & Week 2 Framing
*(5 min — talking points only, no code)*

**Talking Points:**

> "In Week 1 we built the entire shell of a production-grade API: routing, DTOs, validation, centralised error handling, structured logging, JWT authentication, and role-based authorisation. Everything you would expect to see in a real system."

> "But there is one critical flaw. Every time we restart the API, every booking we created disappears. We are running against a static `List<Booking>` in memory — a scaffolding tool. Today we rip it out and connect to a real database."

> "This week the theme is persistence. Day 1 is foundations: understanding the ORM, getting PostgreSQL connected, and making our first migration. By the end of today, bookings will survive a server restart."

**Write on the board:**

```
Week 1: API Shell          Week 2: Persistence
─────────────────          ─────────────────────
Routing                    EF Core + PostgreSQL
DTOs & Validation          Relationships
Error Handling             Queries & Filtering
Serilog                    Transactions
JWT Auth                   Seeding & Migrations
         ↓
    BookingStore (List<>)  →  BookingDbContext (PostgreSQL)
```

---

## Phase 1 – The ORM Mental Model
*(10 min — conceptual, no code)*

> "Before we write a single line, everyone needs to understand what EF Core is actually doing. Open a blank whiteboard."

**Draw the three layers:**

```
Your C# Code                 EF Core                   PostgreSQL
────────────────             ─────────────────────     ──────────────────
List<Booking>       →        DbSet<Booking>        →   bookings table
Booking object      →        Change Tracker        →   INSERT / UPDATE / DELETE
await SaveChanges() →        SQL Generator         →   SQL sent to Postgres
```

**Talking Points:**

> "An ORM — Object-Relational Mapper — is a translation layer. You write C# objects and LINQ queries. EF Core translates them into SQL. You never write `INSERT INTO bookings ...` by hand. EF Core writes it for you based on the shape of your classes."

> "The most important component is the **Change Tracker**. Every time you load an entity from the database, EF Core takes a snapshot of it. When you call `SaveChangesAsync()`, it compares the current state of every entity to its snapshot and generates only the SQL needed to reconcile the difference. Load a booking, change its title, save — EF Core generates exactly one `UPDATE` statement touching exactly one column."

> "The **DbContext** is the unit of work. It owns the Change Tracker, holds the database connection, and exposes `DbSet<T>` properties — one per database table. Its lifetime is one HTTP request. A new request gets a new DbContext. When the request ends, the DbContext is disposed and the connection is returned to the pool."

**Entities vs Domain Models vs DTOs — explain the three distinct layers:**

```
Entity          →   C# class that maps directly to a database table. EF Core manages it.
Domain Model    →   The concept your business logic works with. May or may not be the entity.
DTO             →   What the HTTP layer exposes. Never exposes raw entities to the client.
```

> "In a small API like ours, the Entity and the Domain Model are often the same class. In a large domain with complex business rules, they are deliberately separated. For this week, we will map directly from Entity to DTO to keep the focus on EF Core."

---

## Phase 2 – Environment Setup: PostgreSQL & Packages
*(10 min)*

**Action:** Start a PostgreSQL instance. Show both options — trainees choose based on their machine:

**Option A — Docker (recommended for consistent environments):**
```bash
docker run --name conferences-db \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=ConferenceBookings \
  -p 5432:5432 \
  -d postgres:17
```

**Option B — Local PostgreSQL install:**
- Start the PostgreSQL service
- Open pgAdmin or psql and create a database named `ConferenceBookings`

**Action:** Install the three required packages:

```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
```

```bash
# Install the EF Core CLI tools globally (once per machine)
dotnet tool install --global dotnet-ef
```

**Talking Points:**

> "`Npgsql.EntityFrameworkCore.PostgreSQL` is the **provider** — it is the bridge between EF Core's generic SQL generation and PostgreSQL's specific dialect, data types, and wire protocol. EF Core itself is provider-agnostic: swap Npgsql for `Pomelo.EntityFrameworkCore.MySql` and the same code talks to MySQL instead."

> "`Microsoft.EntityFrameworkCore.Design` is a build-time package. The CLI tools need it to inspect your DbContext and generate migration files. It does not ship in the production binary."

> "`dotnet-ef` is the EF Core CLI. It lives outside your project — it is a global tool like `git`. You use it to add migrations, apply them to the database, and roll them back."

**Action:** Add the connection string to `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ConferenceBookings;Username=postgres;Password=postgres"
  }
}
```

**Talking Points:**

> "Never put a connection string in `appsettings.json`. That file is committed to source control. `appsettings.Development.json` is environment-specific and typically excluded from the repository via `.gitignore`. In production, the connection string comes from an environment variable or a secrets vault — never from a file on disk."

---

## Phase 3 – The Entity Class
*(10 min)*

**Action:** Open `API/Models/Booking.cs`. Explain what needs to change and why.

> "Our existing `Booking` is a positional `record`. EF Core can map records, but the Change Tracker is designed around mutable classes — it needs to be able to set individual properties on the object it is tracking. A positional record's properties are `init`-only, which creates friction. We are going to convert it to a regular class."

**Show the before and after side by side:**

```csharp
// BEFORE — record with positional constructor
// init-only properties: EF Core cannot mutate these during hydration
public record Booking(
    Guid Id,
    string Title,
    string Speaker,
    string Room,
    DateTime StartTime
);
```

```csharp
// AFTER — class with settable properties
// API/Models/Booking.cs
namespace API.Models;

public class Booking
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Speaker { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
}
```

**Talking Points:**

> "Notice the `= string.Empty` initialisers. These prevent nullable reference type warnings — they tell the compiler 'I know this property is non-null; EF Core will populate it from the database before anyone uses it.' Without them, the compiler correctly warns you that a `string` property could be null."

> "There is no `[Key]` attribute, no `[Column]` attribute, no `[Table]` attribute on this class. We will configure all of that in the `DbContext` using the **Fluent API** — which we will cover in the next phase. Keeping entities free of data annotations keeps your domain model clean and decoupled from the persistence layer."

> "EF Core uses conventions to guess sensible defaults: a property named `Id` or `BookingId` is assumed to be the primary key. A `Guid` primary key is not auto-generated by EF Core by default with PostgreSQL — we will configure that explicitly."

---

## Phase 4 – DbContext: The Gateway to the Database
*(15 min)*

**Action:** Create `API/Data/BookingDbContext.cs`.

```csharp
// API/Data/BookingDbContext.cs
using Microsoft.EntityFrameworkCore;
using API.Models;

namespace API.Data;

// DbContext is the Unit of Work and Repository rolled into one.
// It owns the connection, the Change Tracker, and all DbSets.
// Lifetime: one instance per HTTP request (registered as Scoped in DI).
public class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    // DbSet<T> represents the bookings table.
    // Querying this property generates SELECT statements.
    // Adding/removing entities through it generates INSERT/DELETE.
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Fluent API configuration lives here — not on the entity class.
        // This keeps the entity clean and makes it easy to see all
        // database configuration in one place.
        modelBuilder.Entity<Booking>(entity =>
        {
            // Explicit table name — PostgreSQL convention is lowercase snake_case.
            entity.ToTable("bookings");

            // Primary key — Guid generated by the application, not the database.
            // Guid.NewGuid() in the controller sets this before the entity is saved.
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id).ValueGeneratedNever();

            // Column constraints — these are enforced at the database level,
            // not just in application code. Defence in depth.
            entity.Property(b => b.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(b => b.Speaker)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(b => b.Room)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(b => b.StartTime)
                  .IsRequired();

            // Unique constraint — prevents duplicate room/time bookings at the database level.
            // Our controller checks first, but this is the true safety net.
            entity.HasIndex(b => new { b.Room, b.StartTime })
                  .IsUnique()
                  .HasDatabaseName("ix_bookings_room_starttime");
        });
    }
}
```

**Action:** Register the DbContext in `Program.cs`:

```csharp
// Program.cs — inside the builder phase
builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

**Talking Points:**

> "We are using a **primary constructor** — the same pattern we adopted for `AuthController` and `GlobalExceptionHandler`. `DbContextOptions<BookingDbContext>` carries the configuration: the connection string, the provider, and any options we set. EF Core's DI integration creates and injects this automatically."

> "`ValueGeneratedNever()` is critical. By default, EF Core on PostgreSQL might try to use a database sequence to generate Guid primary keys. We want the application to own this — we call `Guid.NewGuid()` before saving. This is intentional: the ID is known before the INSERT, which means we can set the `Location` header on a 201 response without having to re-query the database."

> "The `HasIndex` with `IsUnique` is the database-level equivalent of our controller's duplicate check. Even if someone bypassed the API and inserted directly into the database, the unique constraint would reject the duplicate. This is defence in depth — rules enforced at multiple layers."

> "`AddDbContext<T>` registers the context as **Scoped** by default. One DbContext per HTTP request. This aligns with the Change Tracker's purpose: track changes for the duration of one operation (one request), then dispose. If you accidentally register it as Singleton, you will get thread-safety bugs under concurrent load."

---

## Phase 5 – Migrations: Schema from Code
*(15 min)*

**Action:** Open the terminal and run the first migration.

> "We have defined our entity and our DbContext. EF Core now knows what we want the database to look like. A **migration** is a snapshot of the difference between the current database schema and what our code expects. Let us generate the first one."

```bash
dotnet ef migrations add InitialCreate
```

**Action:** Open the generated migration file and walk through it with the class.

```csharp
// Example of what EF Core generates — Migrations/20250601_InitialCreate.cs
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "bookings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Speaker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Room = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_bookings", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_bookings_room_starttime",
            table: "bookings",
            columns: new[] { "Room", "StartTime" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "bookings");
    }
}
```

**Talking Points:**

> "EF Core generated this file entirely from the `OnModelCreating` configuration we wrote. It has two methods: `Up` — what to do to advance to this schema version — and `Down` — how to completely undo it. Every migration is reversible."

> "Notice the `timestamp with time zone` type for `StartTime`. Npgsql maps `DateTime` to this PostgreSQL type, which stores values in UTC. This is correct behaviour — our controllers use `DateTime.UtcNow` consistently. If you stored a `DateTimeOffset` instead, Npgsql handles the offset data too."

> "This migration file is source code. It goes into version control. When a teammate pulls your branch, they run `dotnet ef database update` and their local database matches yours exactly. Migrations are the contract between your code and your schema."

**Action:** Apply the migration to the database:

```bash
dotnet ef database update
```

> "Check your PostgreSQL client (pgAdmin, TablePlus, or psql) — you should now see a `bookings` table and a `__EFMigrationsHistory` table. EF Core maintains that history table to track which migrations have been applied so it knows what to run next time."

**Migration workflow reference — show on the board:**

```
Code change needed?
       ↓
dotnet ef migrations add <DescriptiveName>
       ↓
Review the generated file — is it what you expected?
       ↓
dotnet ef database update   ← applies pending migrations
       ↓
Made a mistake before committing?
       ↓
dotnet ef migrations remove  ← deletes the last unapplied migration file
```

**Naming convention talking point:**

> "Migration names are permanent — they go into source control and the `__EFMigrationsHistory` table. Name them like commit messages: describe what changed, not when. `InitialCreate`, `AddSpeakerBio`, `AddRoomCapacity`, `RenameStartTimeToScheduledAt` — these tell the story of your schema's evolution. `Migration1`, `Update`, `Fix` are useless six months later."

---

## Phase 6 – CRUD Operations with EF Core
*(15 min)*

> "Now we replace every `BookingStore.Bookings` call in `BookingsController` with an async EF Core operation. The controller receives the `BookingDbContext` via primary constructor injection."

**Action:** Show the updated controller — walk through each operation.

**Inject DbContext via primary constructor:**
```csharp
// BookingsController.cs
public class BookingsController(BookingDbContext db) : ControllerBase
```

**GET all — `ToListAsync()`:**
```csharp
[HttpGet]
public async Task<ActionResult<IEnumerable<Booking>>> GetBookingsAsync()
{
    // ToListAsync() executes: SELECT * FROM bookings
    // All rows are loaded into memory and returned.
    // Week 2 Day 3 will add filtering and pagination so we are not
    // loading the entire table on every request.
    var bookings = await db.Bookings.ToListAsync();
    return Ok(bookings);
}
```

**GET by ID — `FindAsync()`:**
```csharp
[HttpGet("{id:guid}")]
public async Task<ActionResult<Booking>> GetBookingByIdAsync(Guid id)
{
    // FindAsync checks the Change Tracker first (already-loaded entities),
    // then hits the database. Generates: SELECT * FROM bookings WHERE id = @id LIMIT 1
    var booking = await db.Bookings.FindAsync(id);

    if (booking is null)
        throw new BookingNotFoundException(id);

    return Ok(booking);
}
```

**POST — `Add()` + `SaveChangesAsync()`:**
```csharp
[Authorize(Roles = "Employee,Receptionist,Admin")]
[HttpPost]
public async Task<ActionResult<BookingResponse>> CreateBookingAsync(
    [FromBody] CreateBookingRequest request)
{
    // AnyAsync is more efficient than loading the entity — it generates EXISTS(SELECT ...)
    bool isDuplicate = await db.Bookings.AnyAsync(b =>
        b.Room == request.Room && b.StartTime == request.StartTime);

    if (isDuplicate)
        throw new DuplicateBookingException(request.Room, request.StartTime!.Value);

    var newBooking = new Booking
    {
        Id        = Guid.NewGuid(),  // Application owns the ID
        Title     = request.Title,
        Speaker   = request.Speaker,
        Room      = request.Room,
        StartTime = request.StartTime!.Value
    };

    db.Bookings.Add(newBooking);      // Tells the Change Tracker: this entity is new
    await db.SaveChangesAsync();       // Executes: INSERT INTO bookings (...)

    var response = new BookingResponse(
        newBooking.Id, newBooking.Title, newBooking.Speaker,
        newBooking.Room, newBooking.StartTime);

    return CreatedAtAction(nameof(GetBookingByIdAsync), new { id = response.id }, response);
}
```

**PUT — load, mutate, `SaveChangesAsync()`:**
```csharp
[Authorize(Roles = "Receptionist,FacilitiesManager,Admin")]
[HttpPut("{id:guid}")]
public async Task<ActionResult<BookingResponse>> UpdateBookingAsync(
    Guid id, [FromBody] CreateBookingRequest request)
{
    var booking = await db.Bookings.FindAsync(id);

    if (booking is null)
        return NotFound();

    // The Change Tracker took a snapshot when FindAsync loaded the entity.
    // Mutating these properties marks the entity as Modified.
    booking.Title     = request.Title;
    booking.Speaker   = request.Speaker;
    booking.Room      = request.Room;
    booking.StartTime = request.StartTime!.Value;

    // SaveChangesAsync compares against the snapshot and generates:
    // UPDATE bookings SET title=@t, speaker=@s, room=@r, start_time=@dt WHERE id=@id
    await db.SaveChangesAsync();

    var response = new BookingResponse(
        booking.Id, booking.Title, booking.Speaker,
        booking.Room, booking.StartTime);

    return Ok(response);
}
```

**DELETE — load, `Remove()`, `SaveChangesAsync()`:**
```csharp
[Authorize(Roles = "Admin")]
[HttpDelete("{id:guid}")]
public async Task<ActionResult> DeleteBookingAsync(Guid id)
{
    var booking = await db.Bookings.FindAsync(id);

    if (booking is null)
        throw new BookingNotFoundException(id);

    db.Bookings.Remove(booking);   // Marks the entity as Deleted
    await db.SaveChangesAsync();   // Executes: DELETE FROM bookings WHERE id = @id

    return NoContent(); // 204
}
```

**Talking Points:**

> "Notice that `Add` and `Remove` are synchronous — they only update the Change Tracker in memory. The actual database call only happens on `await SaveChangesAsync()`. This allows you to add multiple entities, make multiple changes, and then commit them all in a single database round trip. One call to `SaveChangesAsync` can produce many SQL statements, all wrapped in a single transaction."

> "The `with` expression we used previously on records is gone. Entities are mutable classes now — we set properties directly. The Change Tracker detects the mutation by comparing to the snapshot it took when `FindAsync` loaded the entity."

> "`AnyAsync` on the duplicate check is deliberate. `AnyAsync` generates `SELECT EXISTS(...)` — it stops at the first matching row. `FirstOrDefaultAsync` would load the entire entity just to check if it exists. Always use `AnyAsync` for existence checks."

---

## Phase 7 – EF Core 10 Highlights: Bulk Operations
*(5 min)*

> "One pain point with the traditional EF Core pattern is that updates and deletes require loading the entity first. EF Core 7 introduced `ExecuteUpdateAsync` and `ExecuteDeleteAsync` to address this. These are improved further in EF Core 10."

**`ExecuteDeleteAsync` — delete without loading:**
```csharp
// Deletes all bookings for a cancelled room in one SQL statement.
// No entities are loaded into the Change Tracker.
// Generates: DELETE FROM bookings WHERE room = 'Old Room A'
await db.Bookings
    .Where(b => b.Room == "Old Room A")
    .ExecuteDeleteAsync();
```

**`ExecuteUpdateAsync` — update without loading:**
```csharp
// Renames a room across all bookings in one SQL statement.
// Generates: UPDATE bookings SET room = 'Main Hall' WHERE room = 'Ballroom'
await db.Bookings
    .Where(b => b.Room == "Ballroom")
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(b => b.Room, "Main Hall"));
```

**Talking Points:**

> "These operations bypass the Change Tracker entirely. They translate directly to a single `DELETE` or `UPDATE` statement. This is dramatically more efficient for bulk operations — you do not pay the cost of loading potentially thousands of entities into memory just to delete them."

> "The trade-off: because they bypass the Change Tracker, EF Core does not fire any entity lifecycle events and does not update any tracked entities in memory. If you have already loaded some of these bookings earlier in the same request, the in-memory copies will be stale. For bulk operations, this is almost always the right trade-off."

> "This pattern is the right tool for administration endpoints — 'cancel all bookings in this room', 'archive all bookings older than 30 days'. For single-entity operations triggered by user actions, the load-then-save pattern remains correct."

---

## Phase 8 – Testing in Scalar
*(5 min)*

**Action:** Run the application and open the Scalar UI.

1. **Verify the database is empty:** Call `GET /api/bookings` — expect an empty array `[]`.
2. **Create a booking:** Call `POST /api/bookings` (with an Admin token) and submit a valid body. Expect a `201 Created` with the booking in the response body.
3. **Prove persistence:** Stop the application with `Ctrl+C`. Restart it with `dotnet run`. Call `GET /api/bookings` again.

> "The booking is still there. It survived the restart. That is the difference between an in-memory list and a real database. We have made the system remember."

4. **Verify the unique constraint:** Attempt to `POST` the same room and start time again. Expect a `409 Conflict` — the application-level check in the controller fires first, but even if it did not, the database unique constraint would reject the insert.

---

## Wrap-Up & What's Next
*(5 min)*

**Write on the board:**

```
The Persistence Stack (Day 1 complete)

C# Object (Booking)
    ↓
Change Tracker (tracks mutations)
    ↓
SQL Generator (translates to PostgreSQL SQL)
    ↓
Npgsql Provider (sends over the wire)
    ↓
PostgreSQL bookings table
```

**Closing Talking Points:**

> "We have replaced the entire in-memory scaffolding with a real database in one session. The controller code barely changed — and that is the point of the architecture we built in Week 1. The layers are decoupled. Swapping the persistence layer did not require rewriting the auth, the error handling, or the DTOs."

> "Migration naming, the Change Tracker lifecycle, and `SaveChangesAsync` as the unit of work commit are the three concepts to take away from today. Everything else in EF Core builds on these."

> "Tomorrow we introduce relationships. We will add a `Room` entity and a `Speaker` entity, connect them to `Booking` with foreign keys, and learn how EF Core handles navigation properties and eager loading."

---

## Assignment 2.1 — CareerHub: DbContext, Entities & First Migration

**Objective:** Replace the in-memory job listing store in your CareerHub API with a PostgreSQL database backed by EF Core 10.

**Part 1 — Environment Setup**

Install the required packages into your CareerHub project:
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Microsoft.EntityFrameworkCore.Design`
- The `dotnet-ef` global tool (if not already installed)

Add a `ConnectionStrings:DefaultConnection` entry to your `appsettings.Development.json`. Use `CareerHub` as the database name. Do not commit real credentials to source control.

**Part 2 — The Entity Class**

Your existing `JobPosting` or `JobListing` type is likely a record or a DTO-shaped class. Convert it to a proper EF Core entity class in your `Models` folder with the following properties:

- `Id` — `Guid`, primary key, value generated by the application
- `Title` — `string`, required, max 200 characters
- `Company` — `string`, required, max 100 characters
- `Description` — `string`, required, max 2000 characters
- `Location` — `string`, required, max 100 characters
- `PostedAt` — `DateTime`, required, defaults to `DateTime.UtcNow`

**Part 3 — DbContext**

Create `Data/CareerHubDbContext.cs`. It must:
- Inherit from `DbContext` using a primary constructor
- Expose a `DbSet<JobListing>` property
- Configure the entity using `OnModelCreating` and the Fluent API — no data annotations on the entity class
- Map the entity to a lowercase table name (`job_listings`)
- Define the unique constraint that prevents duplicate titles at the same company (equivalent to your existing `DuplicateJobListingException` check)

Register the DbContext in `Program.cs` using `AddDbContext<T>` with the Npgsql provider. Read the connection string from configuration.

**Part 4 — First Migration**

Run `dotnet ef migrations add InitialCreate`. Open the generated file and verify:
- The `job_listings` table is created with the correct columns and types
- The unique index is present
- The `Down` method correctly drops the table

Apply the migration with `dotnet ef database update`. Verify the table exists in your PostgreSQL client.

**Part 5 — Replacing In-Memory CRUD**

Inject `CareerHubDbContext` into your `JobsController` via primary constructor. Replace every in-memory list operation with the correct EF Core async equivalent:

| Operation | In-Memory | EF Core |
|-----------|-----------|---------|
| Get all | `_store.ToList()` | `await db.JobListings.ToListAsync()` |
| Get by ID | `_store.FirstOrDefault(...)` | `await db.JobListings.FindAsync(id)` |
| Existence check | `_store.Any(...)` | `await db.JobListings.AnyAsync(...)` |
| Create | `_store.Add(...)` | `db.JobListings.Add(...); await db.SaveChangesAsync()` |
| Update | mutate + replace | load entity, mutate properties, `await db.SaveChangesAsync()` |
| Delete | `_store.Remove(...)` | `db.JobListings.Remove(...); await db.SaveChangesAsync()` |

**Proving It Works**

1. Run `dotnet run` and call `GET /api/jobs` — expect an empty array.
2. Call `POST /api/jobs` with a valid token and body — expect `201 Created`.
3. Stop the application. Restart it. Call `GET /api/jobs` — the listing must still be there.
4. Attempt to create the same listing again (same title and company) — expect `409 Conflict`.
5. Call `DELETE /api/jobs/{id}` with an Admin token — expect `204 No Content`. Confirm the row is gone in your PostgreSQL client.

**Version Control**

Suggested commits:
- Add Npgsql EF Core provider and design packages
- Create JobListing entity class and CareerHubDbContext
- Add InitialCreate migration
- Replace in-memory store with EF Core DbContext in JobsController

**README Updates**

Add a section covering:
1. **The Change Tracker:** In your own words, explain what the EF Core Change Tracker does and why `SaveChangesAsync()` is called once at the end of an operation rather than once per property change.
2. **Migrations:** Explain why the generated migration file should be committed to source control alongside the code change that caused it.
