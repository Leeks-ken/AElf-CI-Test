using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MartinCostello.Logging.XUnit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Shouldly;
using Volo.Abp.AspNetCore.TestBase;
using Xunit.Abstractions;

namespace AElf.WebApp.Application;

public class WebAppTestBase : AbpAspNetCoreIntegratedTestBase<WebAppTestStartup>, ITestOutputHelperAccessor
{
    public WebAppTestBase(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
    }

    public ITestOutputHelper OutputHelper { get; set; }

    protected override IHostBuilder CreateHostBuilder()
    {
        return base.CreateHostBuilder()
            .ConfigureLogging(builder =>
            {
                builder
                    .AddXUnit(this)
                    .SetMinimumLevel(LogLevel.None);
            });
    }

    protected async Task<T> GetResponseAsObjectAsync<T>(string url, string version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
    {
        var strResponse = await GetResponseAsStringAsync(url, version, expectedStatusCode);
        return JsonConvert.DeserializeObject<T>(strResponse, new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });
    }

    protected async Task<string> GetResponseAsStringAsync(string url, string version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
    {
        var response = await GetResponseAsync(url, version, expectedStatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    protected async Task<HttpResponseMessage> GetResponseAsync(string url, string version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
    {
        version = !string.IsNullOrWhiteSpace(version) ? $";v={version}" : string.Empty;
        Client.DefaultRequestHeaders.Accept.Clear();
        Client.DefaultRequestHeaders.Accept.Add(
            MediaTypeWithQualityHeaderValue.Parse($"application/json{version}"));

        var response = await Client.GetAsync(url);
        response.StatusCode.ShouldBe(expectedStatusCode);
        return response;
    }

    protected async Task<T> PostResponseAsObjectAsync<T>(string url, Dictionary<string, string> parameters,
        string version = null, HttpStatusCode expectedStatusCode = HttpStatusCode.OK, BasicAuth basicAuth = null)
    {
        var strResponse = await PostResponseAsStringAsync(url, parameters, version, expectedStatusCode, basicAuth);
        return JsonConvert.DeserializeObject<T>(strResponse, new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });
    }

    protected async Task<string> PostResponseAsStringAsync(string url, Dictionary<string, string> parameters,
        string version = null, HttpStatusCode expectedStatusCode = HttpStatusCode.OK, BasicAuth basicAuth = null)
    {
        var response = await PostResponseAsync(url, parameters, version, expectedStatusCode, basicAuth);
        return await response.Content.ReadAsStringAsync();
    }

    protected async Task<HttpResponseMessage> PostResponseAsync(string url, Dictionary<string, string> parameters,
        string version = null, HttpStatusCode expectedStatusCode = HttpStatusCode.OK, BasicAuth basicAuth = null,
        string reason = null)
    {
        version = !string.IsNullOrWhiteSpace(version) ? $";v={version}" : string.Empty;
        if (basicAuth != null)
        {
            var byteArray = Encoding.ASCII.GetBytes($"{basicAuth.UserName}:{basicAuth.Password}");
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var paramsStr = JsonConvert.SerializeObject(parameters);
        var content = new StringContent(paramsStr, Encoding.UTF8, "application/json");
        content.Headers.ContentType = MediaTypeHeaderValue.Parse($"application/json{version}");

        var response = await Client.PostAsync(url, content);
        response.StatusCode.ShouldBe(expectedStatusCode);
        if (reason != null) response.ReasonPhrase.ShouldBe(reason);
        return response;
    }

    protected async Task<T> DeleteResponseAsObjectAsync<T>(string url, string version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK, BasicAuth basicAuth = null)
    {
        var strResponse = await DeleteResponseAsStringAsync(url, version, expectedStatusCode, basicAuth);
        return JsonConvert.DeserializeObject<T>(strResponse, new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });
    }

    protected async Task<string> DeleteResponseAsStringAsync(string url, string version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK, BasicAuth basicAuth = null)
    {
        var response = await DeleteResponseAsync(url, version, expectedStatusCode, basicAuth);
        return await response.Content.ReadAsStringAsync();
    }

    protected async Task<HttpResponseMessage> DeleteResponseAsync(string url, string version = null,
        HttpStatusCode expectedStatusCode = HttpStatusCode.OK, BasicAuth basicAuth = null, string reason = null)
    {
        version = !string.IsNullOrWhiteSpace(version) ? $";v={version}" : string.Empty;
        Client.DefaultRequestHeaders.Accept.Clear();
        Client.DefaultRequestHeaders.Accept.Add(
            MediaTypeWithQualityHeaderValue.Parse($"application/json{version}"));
        if (basicAuth != null)
        {
            var byteArray = Encoding.ASCII.GetBytes($"{basicAuth.UserName}:{basicAuth.Password}");
            Client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        var response = await Client.DeleteAsync(url);
        response.StatusCode.ShouldBe(expectedStatusCode);
        if (reason != null) response.ReasonPhrase.ShouldBe(reason);
        return response;
    }
}

public class BasicAuth
{
    public static readonly string DefaultUserName = "user";

    public static string DefaultPassword = "password";

    public static readonly BasicAuth Default = new()
    {
        UserName = DefaultUserName,
        Password = DefaultPassword
    };

    public string UserName { get; set; }

    public string Password { get; set; }
}