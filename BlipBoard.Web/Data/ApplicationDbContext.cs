using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlipBoard.Data
{
    public class Board
    {
        public Guid Id { get; set; }

        public Guid EnpointId { get; set; }

        [StringLength(100)]
        public String Name { get; set; }

        public Boolean IsEnabled { get; set; }

        [Required]
        [StringLength(250)]
        public String OwnerId { get; set; }

        public BoardUser Owner { get; set; }
    }

    public class Lane
    {
        [StringLength(250)]
        public String OwnerId { get; set; }

        public BoardUser Owner { get; set; }

        [StringLength(80)]
        public String Name { get; set; }

        public Int32 LaneNumber { get; set; }

        public DateTimeOffset ResetAt { get; set; }
    }

    public class BoardUser : IdentityUser
    {
    }

    public class ApplicationDbContext : IdentityDbContext<BoardUser>
    {
        public DbSet<Board> Boards { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Lane>().HasOne(e => e.Owner).WithMany();
        }
    }
}
