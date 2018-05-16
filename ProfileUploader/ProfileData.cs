using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace ProfileUploader
{
    public class ProfileData : DbContext
    {
        public DbSet<Profile> Profile { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbpath}");
        }

        public ProfileData(string databasePath)
        {
            _dbpath = databasePath;
        }

        public string _dbpath;
    }

    [Table("trackrecord155")]
    public class Profile
    {
        /*
         * CREATE TABLE `trackrecord155` (
           `timestamp`	INTEGER NOT NULL,
           `p1name`	TEXT,
           `p1id`	INTEGER NOT NULL,
           `p1sid`	INTEGER NOT NULL,
           `p1win`	INTEGER NOT NULL,
           `p2name`	TEXT,
           `p2id`	INTEGER NOT NULL,
           `p2sid`	INTEGER NOT NULL,
           `p2win`	INTEGER NOT NULL,
           PRIMARY KEY(`timestamp`)
           );
         */
        [Column("timestamp")][Key]
        public long Timestamp { get; set; }
        [Column("p1name")]
        public byte[] P1Name { get; set; }
        [Column("p1id")]
        public int P1ID { get; set; }
        [Column("p1sid")]
        public int P1SID { get; set; }
        [Column("p1win")]
        public int P1Win { get; set; }
        [Column("p2name")]
        public byte[] P2Name { get; set; }
        [Column("p2id")]
        public int P2ID { get; set; }
        [Column("p2sid")]
        public int P2SID { get; set; }
        [Column("p2win")]
        public int P2Win { get; set; }

    }
}
