﻿using Microsoft.EntityFrameworkCore;

namespace ThinkAndJobSolution.Controllers.Authorization
{
    public class DataContext : DbContext
    {
        public DbSet<LoginData> Users { get; set; }

        private readonly IConfiguration Configuration;

        public DataContext(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        //protected override void OnConfiguring(DbContextOptionsBuilder options)
        //{
        //    // in memory database used for simplicity, change to a real db for production applications
        //    options.UseInMemoryDatabase("TestDb");
        //}

    }
}
