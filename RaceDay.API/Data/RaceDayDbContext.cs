using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

    public class RaceDayDbContext : DbContext
    {
        public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Venue> Venues => Set<Venue>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Enrolment> Enrolments => Set<Enrolment>();
        public DbSet<Result> Results => Set<Result>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<User>(e =>
            {
                e.HasIndex(u => u.Email).IsUnique();
                e.ToTable(t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser','Participant')"));
                e.Property(u => u.CreatedAt).HasDefaultValueSql("GETDATE()");
            });

            b.Entity<Venue>(e =>
            {
                e.Property(v => v.Latitude).HasColumnType("decimal(9,6)");
                e.Property(v => v.Longitude).HasColumnType("decimal(9,6)");
            });

            b.Entity<Event>(e =>
            {
                e.Property(x => x.EventDate).HasColumnType("date");
                e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
                e.ToTable(t => t.HasCheckConstraint("CK_Events_Type", "[EventType] IN ('Run','Walk','Cycle')"));

                e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                    .HasForeignKey(x => x.OrganiserID).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Venue).WithMany(v => v.Events)
                    .HasForeignKey(x => x.VenueID).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Category>(e =>
            {
                e.Property(c => c.DistanceKm).HasColumnType("decimal(5,2)");
                e.Property(c => c.EntryFee).HasColumnType("decimal(8,2)");
                e.HasOne(c => c.Event).WithMany(ev => ev.Categories)
                    .HasForeignKey(c => c.EventID).OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<Enrolment>(e =>
            {
                e.Property(x => x.EnrolmentDate).HasDefaultValueSql("GETDATE()");
                e.ToTable(t => t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Confirmed','Cancelled')"));
                e.HasIndex(x => new { x.ParticipantID, x.CategoryID }).IsUnique();

                e.HasOne(x => x.Participant).WithMany(u => u.Enrolments)
                    .HasForeignKey(x => x.ParticipantID).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                    .HasForeignKey(x => x.CategoryID).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Result>(e =>
            {
                e.ToTable(t => t.HasCheckConstraint("CK_Results_Status", "[Status] IN ('Finished','DNF','DQ')"));
                e.HasIndex(r => r.EnrolmentID).IsUnique();

                e.HasOne(r => r.Enrolment).WithOne(en => en.Result)
                    .HasForeignKey<Result>(r => r.EnrolmentID).OnDelete(DeleteBehavior.Restrict);
            });

            // Seed data (mirrors your SQL script)
            b.Entity<User>().HasData(
                new User { UserID = 1, FullName = "Thandiwe Mokoena", Email = "thandiwe.mokoena@raceday.co.za", PasswordHash = "HASHED_PASSWORD_1", Role = "Organiser", CreatedAt = new DateTime(2026, 1, 1) },
                new User { UserID = 2, FullName = "Johan van der Merwe", Email = "johan.vdm@raceday.co.za", PasswordHash = "HASHED_PASSWORD_2", Role = "Organiser", CreatedAt = new DateTime(2026, 1, 1) },
                new User { UserID = 3, FullName = "Lindiwe Dlamini", Email = "lindiwe.dlamini@gmail.com", PasswordHash = "HASHED_PASSWORD_3", Role = "Participant", CreatedAt = new DateTime(2026, 1, 1) },
                new User { UserID = 4, FullName = "Craig Adams", Email = "craig.adams@gmail.com", PasswordHash = "HASHED_PASSWORD_4", Role = "Participant", CreatedAt = new DateTime(2026, 1, 1) });

            b.Entity<Venue>().HasData(
                new Venue { VenueID = 1, VenueName = "Green Point Park", Address = "Fritz Sonnenberg Rd", City = "Cape Town", Province = "Western Cape", Latitude = -33.9047m, Longitude = 18.4058m },
                new Venue { VenueID = 2, VenueName = "Marks Park Sports Club", Address = "106 Judith Rd", City = "Johannesburg", Province = "Gauteng", Latitude = -26.1615m, Longitude = 28.0201m },
                new Venue { VenueID = 3, VenueName = "Comrades Marathon Route", Address = "Peter Brown Dr", City = "Pietermaritzburg", Province = "KwaZulu-Natal", Latitude = -29.6006m, Longitude = 30.3794m });

            b.Entity<Event>().HasData(
                new Event { EventID = 1, OrganiserID = 1, VenueID = 1, EventName = "Cape Town Cycle Tour Fun Ride", Description = "A community cycling event around the Cape Peninsula.", EventDate = new DateTime(2026, 11, 8), EventType = "Cycle", CreatedAt = new DateTime(2026, 1, 1) },
                new Event { EventID = 2, OrganiserID = 2, VenueID = 2, EventName = "Johannesburg Park Run Challenge", Description = "A timed 5km/10km park run series in Johannesburg.", EventDate = new DateTime(2026, 10, 17), EventType = "Run", CreatedAt = new DateTime(2026, 1, 1) },
                new Event { EventID = 3, OrganiserID = 1, VenueID = 3, EventName = "Comrades Legacy Charity Walk", Description = "A community walk along part of the Comrades route.", EventDate = new DateTime(2026, 9, 20), EventType = "Walk", CreatedAt = new DateTime(2026, 1, 1) });

            b.Entity<Category>().HasData(
                new Category { CategoryID = 1, EventID = 1, CategoryName = "35km Short Route", DistanceKm = 35m, EntryFee = 350m, MaxParticipants = 500 },
                new Category { CategoryID = 2, EventID = 1, CategoryName = "109km Full Route", DistanceKm = 109m, EntryFee = 650m, MaxParticipants = 1000 },
                new Category { CategoryID = 3, EventID = 2, CategoryName = "5km Fun Run", DistanceKm = 5m, EntryFee = 80m, MaxParticipants = 300 },
                new Category { CategoryID = 4, EventID = 2, CategoryName = "10km Timed Run", DistanceKm = 10m, EntryFee = 120m, MaxParticipants = 300 },
                new Category { CategoryID = 5, EventID = 3, CategoryName = "10km Charity Walk", DistanceKm = 10m, EntryFee = 100m, MaxParticipants = 200 });

            b.Entity<Enrolment>().HasData(
                new Enrolment { EnrolmentID = 1, ParticipantID = 3, CategoryID = 1, EnrolmentDate = new DateTime(2026, 1, 1), Status = "Confirmed" },
                new Enrolment { EnrolmentID = 2, ParticipantID = 3, CategoryID = 3, EnrolmentDate = new DateTime(2026, 1, 1), Status = "Confirmed" },
                new Enrolment { EnrolmentID = 3, ParticipantID = 4, CategoryID = 4, EnrolmentDate = new DateTime(2026, 1, 1), Status = "Confirmed" },
                new Enrolment { EnrolmentID = 4, ParticipantID = 4, CategoryID = 5, EnrolmentDate = new DateTime(2026, 1, 1), Status = "Confirmed" });

            b.Entity<Result>().HasData(
                new Result { ResultID = 1, EnrolmentID = 2, FinishTime = new TimeSpan(0, 22, 14), OverallPosition = 15, CategoryPosition = 4, Status = "Finished" },
                new Result { ResultID = 2, EnrolmentID = 3, FinishTime = new TimeSpan(0, 48, 52), OverallPosition = 32, CategoryPosition = 10, Status = "Finished" });
        }
    }
