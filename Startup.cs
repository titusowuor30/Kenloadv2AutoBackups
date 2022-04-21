using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Kenloadv2AutoBackups
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            _ = cronBackupTime();
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
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kenloadv2AutoBackups v1"));
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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

        public static async Task cronBackupTime()
        {
            string requestUrl = "https://localhost:44365/";
            string email = "admin@admin.com";
            string password = "@Admin123";
            string token = "";
            while (true)
            {
                token = await getToken(email,password,requestUrl);
                if(token != null)
                {
                    try
                    {
                        var requestUrl1 = requestUrl + "api/BackUpDB";
                        using var client1=new HttpClient();
                        client1.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                        var response1 = await client1.GetAsync(requestUrl1);
                        var result1=response1.Content.ReadAsStringAsync().Result;
                        
                        List<ScheduledBackupDetails> scheduledBackupDetails = JsonConvert.DeserializeObject<List<ScheduledBackupDetails>>(result1);
                        Console.WriteLine(scheduledBackupDetails);
                        Console.WriteLine(result1);
                        ScheduledBackupDetails sbackups=new ScheduledBackupDetails();
                        foreach(ScheduledBackupDetails backup in scheduledBackupDetails)
                        {
                            var res = DateTime.Compare(DateTime.Now, backup.backup_time);
                            TimeSpan backuptimespan= DateTime.Now-backup.backup_time;
                            Console.WriteLine(backuptimespan);
                            Console.WriteLine(backuptimespan.TotalSeconds);
                            if(res==0 || (backuptimespan.TotalSeconds >=0 && backuptimespan.TotalSeconds <=5))
                            {
                                
                                try
                                {
                                    var json2 = JsonConvert.SerializeObject(new { });
                                    var backupsData=new StringContent(json2,Encoding.UTF8,"application/json");
                                    var postUrl = requestUrl + "api/DbBackup/CreateBackup?folderpath="+backup.backup_path +"&backupName="+backup.backup_name;
                                    using var client2= new HttpClient();
                                    client2.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                                    var response2 = await client2.PostAsync(postUrl,backupsData);
                                    var result2=response2.Content.ReadAsStringAsync().Result;
                                    //https://localhost:44365/api/DbBackup/CreateBackup?folderpath=C%3A%5CUsers%5CMasterspace%5CDesktop&backupName=autobackup.sql
                                    //https://localhost:44365/DbBackup/CreateBackup?folderpath=C:\Users\Masterspace\Desktop&backupName=autobackup.sql
                                    Console.WriteLine(result2);
                                    if(result2.Length > 0)
                                    {
                                        try
                                        {
                                            var postUrl2 = requestUrl + "api/BackUpDB/" + backup.id;
                                            using var client3 = new HttpClient();
                                            client3.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                                            var response3 = await client3.DeleteAsync(postUrl2);
                                            var result3 = response3.Content.ReadAsStringAsync().Result;
                                        }catch(Exception ex)
                                        {
                                            ex.Message.ToString();
                                        }
                                    }
                                }
                                catch(Exception ex)
                                {
                                    ex.Message.ToString();
                                } 
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        ex.Message.ToString();
                    }
                }
            }
        }

        public static async Task<string> getToken(string email, string password, string requestUrl)
        {
            try
            {
                var jsonSerializedData = JsonConvert.SerializeObject(new { email, password });
                var serializedData = new StringContent(jsonSerializedData, Encoding.UTF8, "application/json");
                var authUrl = requestUrl + "api/authmanagement/login";
                using var client = new HttpClient();
                var response = await client.PostAsync(authUrl, serializedData);
                var result = response.Content.ReadAsStringAsync().Result;
                TokenInfo tokenInfo = JsonConvert.DeserializeObject<TokenInfo>(result);
                return tokenInfo.token;
            }
            catch (Exception ex)
            {
                ex.Message.ToString();
                return null;
            }
        }
        public class TokenInfo
        {
            public string token { get; set; }
            public string success { get; set; }
            public string errors { get; set; }
        }
        public class ScheduledBackupDetails
        {
            public int id { get; set; }
            public int backup_delete { get; set; }
            public DateTime backup_time { get; set; }
            public string dayoftheweek { get; set; }
            public String backup_name { get; set; }
            public String backup_path { get; set; }
            public String fullincr { get; set; }
        }

    }

}
