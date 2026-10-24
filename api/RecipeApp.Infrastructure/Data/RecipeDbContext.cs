using Microsoft.EntityFrameworkCore;
using RecipeApp.Core.Models;

namespace RecipeApp.Infrastructure.Data;

public class RecipeDbContext : DbContext
{
    public RecipeDbContext(DbContextOptions<RecipeDbContext> options)
        : base(options) { }

    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();
    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Recipe
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasIndex(r => r.Title);
            entity.Property(r => r.Title).HasMaxLength(200).IsRequired();
            entity.Property(r => r.Description).HasMaxLength(2000);
            entity.Property(r => r.Cuisine).HasMaxLength(100);
        });

        // Ingredient
        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasIndex(i => i.NormalizedName).IsUnique();
            entity.Property(i => i.Name).HasMaxLength(200).IsRequired();
            entity.Property(i => i.NormalizedName).HasMaxLength(200).IsRequired();
            entity.Property(i => i.Category).HasMaxLength(50);
            entity.Property(i => i.PreferredStore).HasMaxLength(50);
        });

        // RecipeIngredient (bridge table)
        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasIndex(ri => new { ri.RecipeId, ri.IngredientId });
            entity.Property(ri => ri.Quantity).HasPrecision(18, 4);
            entity.Property(ri => ri.Unit).HasMaxLength(20);
            entity.Property(ri => ri.Notes).HasMaxLength(200);

            entity.HasOne(ri => ri.Recipe)
                .WithMany(r => r.RecipeIngredients)
                .HasForeignKey(ri => ri.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ri => ri.Ingredient)
                .WithMany(i => i.RecipeIngredients)
                .HasForeignKey(ri => ri.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // RecipeStep
        modelBuilder.Entity<RecipeStep>(entity =>
        {
            entity.HasIndex(rs => new { rs.RecipeId, rs.StepNumber }).IsUnique();
            entity.Property(rs => rs.Text).HasMaxLength(2000).IsRequired();

            entity.HasOne(rs => rs.Recipe)
                .WithMany(r => r.Steps)
                .HasForeignKey(rs => rs.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Category
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(c => c.Name).IsUnique();
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(500);
        });

        // ShoppingList
        modelBuilder.Entity<ShoppingList>(entity =>
        {
            entity.HasIndex(l => l.ShareToken).IsUnique();
            entity.Property(l => l.Name).HasMaxLength(200).IsRequired();
            entity.Property(l => l.ShareToken).HasMaxLength(64).IsRequired();
        });

        // ShoppingListItem
        modelBuilder.Entity<ShoppingListItem>(entity =>
        {
            entity.HasIndex(i => new { i.ShoppingListId, i.Category });
            entity.Property(i => i.Name).HasMaxLength(200).IsRequired();
            entity.Property(i => i.Category).HasMaxLength(50).IsRequired();
            entity.Property(i => i.Quantity).HasPrecision(18, 4);
            entity.Property(i => i.Unit).HasMaxLength(20);
            entity.Property(i => i.Notes).HasMaxLength(500);
            entity.Property(i => i.RecipeSources).HasMaxLength(1000);
            entity.Property(i => i.Store).HasMaxLength(50);

            entity.HasOne(i => i.ShoppingList)
                .WithMany(l => l.Items)
                .HasForeignKey(i => i.ShoppingListId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Ingredient>()
                .WithMany()
                .HasForeignKey(i => i.IngredientId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // MealPlan
        modelBuilder.Entity<MealPlan>(entity =>
        {
            entity.HasIndex(p => p.WeekStartDate).IsUnique();
            entity.Property(p => p.WeekStartDate).HasMaxLength(10).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
        });

        // MealPlanEntry
        modelBuilder.Entity<MealPlanEntry>(entity =>
        {
            entity.HasIndex(e => new { e.MealPlanId, e.DayOfWeek, e.MealType, e.SortOrder });
            entity.Property(e => e.MealType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Multiplier).HasPrecision(18, 4);
            entity.Property(e => e.CustomTitle).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(e => e.MealPlan)
                .WithMany(p => p.Entries)
                .HasForeignKey(e => e.MealPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Recipe)
                .WithMany()
                .HasForeignKey(e => e.RecipeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // RecipeImage (metadata only; the file lives on disk under data/images/{RecipeId}/)
        modelBuilder.Entity<RecipeImage>(entity =>
        {
            entity.HasIndex(i => i.FileName).IsUnique();
            entity.Property(i => i.FileName).HasMaxLength(200).IsRequired();

            entity.HasOne(i => i.Recipe)
                .WithMany(r => r.Images)
                .HasForeignKey(i => i.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Recipe-Category many-to-many
        modelBuilder.Entity<Recipe>()
            .HasMany(r => r.Categories)
            .WithMany(c => c.Recipes)
            .UsingEntity("RecipeCategories");
    }
}
