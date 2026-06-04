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
        });

        // RecipeIngredient (bridge table)
        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasIndex(ri => new { ri.RecipeId, ri.IngredientId }).IsUnique();
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

        // Recipe-Category many-to-many
        modelBuilder.Entity<Recipe>()
            .HasMany(r => r.Categories)
            .WithMany(c => c.Recipes)
            .UsingEntity("RecipeCategories");
    }
}
