﻿// <auto-generated />
using System;
using Kajsmentkeri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kajsmentkeri.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20250602193447_InitialPostgresMigration")]
    partial class InitialPostgresMigration
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.16")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Kajsmentkeri.Domain.Championship", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("CreatedById")
                        .HasColumnType("uuid");

                    b.Property<string>("Description")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Year")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.ToTable("Championships");
                });

            modelBuilder.Entity("Kajsmentkeri.Domain.ChampionshipScoringRules", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("ChampionshipId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("PointsForCorrectWinner")
                        .HasColumnType("integer");

                    b.Property<int>("PointsForExactScore")
                        .HasColumnType("integer");

                    b.Property<int>("PointsForOnlyCorrectWinner")
                        .HasColumnType("integer");

                    b.Property<int>("RarityPointsBonus")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("ChampionshipId")
                        .IsUnique();

                    b.ToTable("ChampionshipScoringRules");
                });

            modelBuilder.Entity("Kajsmentkeri.Domain.Match", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int?>("AwayScore")
                        .HasColumnType("integer");

                    b.Property<string>("AwayTeam")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("ChampionshipId")
                        .HasColumnType("uuid");

                    b.Property<int?>("HomeScore")
                        .HasColumnType("integer");

                    b.Property<string>("HomeTeam")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("StartTimeUtc")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("ChampionshipId");

                    b.ToTable("Matches");
                });

            modelBuilder.Entity("Kajsmentkeri.Domain.Prediction", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<bool>("GotExactScore")
                        .HasColumnType("boolean");

                    b.Property<bool>("GotWinner")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsOnlyCorrect")
                        .HasColumnType("boolean");

                    b.Property<Guid>("MatchId")
                        .HasColumnType("uuid");

                    b.Property<bool>("OneGoalMiss")
                        .HasColumnType("boolean");

                    b.Property<int>("Points")
                        .HasColumnType("integer");

                    b.Property<int>("PredictedAway")
                        .HasColumnType("integer");

                    b.Property<int>("PredictedHome")
                        .HasColumnType("integer");

                    b.Property<decimal>("RarityPart")
                        .HasColumnType("numeric");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("MatchId");

                    b.HasIndex("UserId", "MatchId")
                        .IsUnique();

                    b.ToTable("Predictions");
                });

            modelBuilder.Entity("Kajsmentkeri.Domain.ChampionshipScoringRules", b =>
                {
                    b.HasOne("Kajsmentkeri.Domain.Championship", "Championship")
                        .WithOne("ScoringRules")
                        .HasForeignKey("Kajsmentkeri.Domain.ChampionshipScoringRules", "ChampionshipId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Championship");
                });

            modelBuilder.Entity("Kajsmentkeri.Domain.Match", b =>
                {
                    b.HasOne("Kajsmentkeri.Domain.Championship", "Championship")
                        .WithMany("Matches")
                        .HasForeignKey("ChampionshipId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Championship");
                });

            modelBuilder.Entity("Kajsmentkeri.Domain.Prediction", b =>
                {
                    b.HasOne("Kajsmentkeri.Domain.Match", "Match")
                        .WithMany("Predictions")
                        .HasForeignKey("MatchId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Match");
                });

            modelBuilder.Entity("Kajsmentkeri.Domain.Championship", b =>
                {
                    b.Navigation("Matches");

                    b.Navigation("ScoringRules")
                        .IsRequired();
                });

            modelBuilder.Entity("Kajsmentkeri.Domain.Match", b =>
                {
                    b.Navigation("Predictions");
                });
#pragma warning restore 612, 618
        }
    }
}
