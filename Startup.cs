using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kenloadv2AutoBackups
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Kenloadv2AutoBackups", Version = "v1" });
            });

            // Register the background service for auto-backups
            services.AddHostedService<BackupSchedulerService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kenloadv2AutoBackups v1"));
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }

    // Background service for handling auto-backups
    public class BackupSchedulerService : BackgroundService
    {
        private readonly string _requestUrl = "https://kenload.kenha.co.ke:4444/";
        private readonly string _email = "admin@admin.com";
        private readonly string _password = "@Admin123";
        private string _token;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _token = await GetToken(_email, _password, _requestUrl);
                if (_token != null)
                {
                    await CheckAndExecuteBackups();
                }

                // Wait for 1 minute before checking again
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task CheckAndExecuteBackups()
        {
            try
            {
                var requestUrl = _requestUrl + "api/BackUpDB";
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _token);
                var response = await client.GetAsync(requestUrl);
                var result = await response.Content.ReadAsStringAsync();

                // Deserialize the JSON response dynamically
                var scheduledBackups = JsonConvert.DeserializeObject<List<dynamic>>(result);
                Console.WriteLine($"Found {scheduledBackups.Count} backup schedules.");

                foreach (var backup in scheduledBackups)
                {
                    if (ShouldRunBackup(backup))
                    {
                        await ExecuteBackup(backup);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking backups: {ex.Message}");
            }
        }

        private bool ShouldRunBackup(dynamic backup)
        {
            var now = DateTime.Now;
            var backupTime = DateTime.Parse(backup.backup_time.ToString());

            // Check if the backup should run today based on the schedule
            bool shouldRunToday = (now.DayOfWeek == DayOfWeek.Monday && backup.backup_m == 1) ||
                                 (now.DayOfWeek == DayOfWeek.Tuesday && backup.backup_t == 1) ||
                                 (now.DayOfWeek == DayOfWeek.Wednesday && backup.backup_w == 1) ||
                                 (now.DayOfWeek == DayOfWeek.Thursday && backup.backup_th == 1) ||
                                 (now.DayOfWeek == DayOfWeek.Friday && backup.backup_f == 1) ||
                                 (now.DayOfWeek == DayOfWeek.Saturday && backup.backup_s == 1) ||
                                 (now.DayOfWeek == DayOfWeek.Sunday && backup.backup_su == 1);

            // Check if the current time matches the backup time
            if (shouldRunToday && now.TimeOfDay >= backupTime.TimeOfDay && now.TimeOfDay <= backupTime.AddMinutes(5).TimeOfDay)
            {
                return true;
            }

            return false;
        }

        private async Task ExecuteBackup(dynamic backup)
        {
            try
            {
                // Create the JSON payload
                var jsonPayload = new
                {
                    folderpath = backup.backup_path.ToString(),
                    backupFileName = backup.backup_name.ToString()
                };

                // Serialize the JSON payload
                var jsonContent = JsonContent.Create(jsonPayload);

                // Construct the URL
                var postUrl = _requestUrl + "api/DbBackup/CreateBackup";

                // Send POST request with JSON data
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _token);
                var response = await client.PostAsync(postUrl, jsonContent);
                var result = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Backup result: {result}");

                // If the backup was successful, delete the backup record
                if (result.Length > 0)
                {
                    var deleteUrl = _requestUrl + "api/BackUpDB/" + backup.id;
                    using var deleteClient = new HttpClient();
                    deleteClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _token);
                    var deleteResponse = await deleteClient.DeleteAsync(deleteUrl);
                    var deleteResult = await deleteResponse.Content.ReadAsStringAsync();

                    Console.WriteLine($"Delete result: {deleteResult}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing backup: {ex.Message}");
            }
        }

        private async Task<string> GetToken(string email, string password, string requestUrl)
        {
            try
            {
                var jsonSerializedData = JsonConvert.SerializeObject(new { email, password });
                var serializedData = new StringContent(jsonSerializedData, Encoding.UTF8, "application/json");
                var authUrl = requestUrl + "api/authmanagement/login";
                using var client = new HttpClient();
                var response = await client.PostAsync(authUrl, serializedData);
                var result = await response.Content.ReadAsStringAsync();
                var tokenInfo = JsonConvert.DeserializeObject<dynamic>(result);
                return tokenInfo.token;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting token: {ex.Message}");
                return null;
            }
        }
    }
}