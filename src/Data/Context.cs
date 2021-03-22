using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace Data
{
    public sealed class Context : DbContext
    {
        // public static readonly ILoggerFactory ContextLoggerFactory
        //     = LoggerFactory.Create(builder => { builder.AddConsole(); });
        
        public Context(DbContextOptions<Context> options)
            : base(options)
        {
            Database.Migrate();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
