using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace KeePassProtonfile
{
    class ApiFile
    {
        public string file_uid;
        public string filename;
        public string location;
        public string extension;
        public string folder_uid;
        public string user_sub;
        public string size;
        public string last_updated;
        public string server_uid;
    }
    class ApiFolder
    {
        public string folder_uid;
        public string title;
        public string user_sub;
        public string parent_folder;
    }
    class DashboardResponse
    {
        public List<ApiFile> files;
        public List<ApiFolder> folders;
    }
    class LoginData { public string email, password; };
    class LoginResponse { public string token; };

    internal class ProtonfileApi
    {
        private string username;
        private string password;
        private HttpClient httpClient;
        private bool authenticated;
        private string accessToken;
        public ProtonfileApi(string username, string password)
        {
            this.username = username;
            this.password = password;
            this.httpClient = new HttpClient();
            this.authenticated = false;
        }
        public string NormalizeApiUrl(params string[] paths)
        {
            return Path.Combine(Properties.Resources.API_URL, Path.Combine(paths)).Replace('\\', '/');
        }
        public string NormalizeUrlParams(string url, params string[] parameters)
        {
            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);

            if (parameters.Length % 2 != 0) throw new ArgumentException("Parameters length must be multiple of 2");

            for (int i = 0; i < parameters.Length; i += 2)
            {
                if (parameters[i] != null && parameters[i + 1] != null)
                {
                    query[parameters[i]] = parameters[i + 1];
                }
            }

            uriBuilder.Query = query.ToString();
            return uriBuilder.ToString();
        }
        private async Task<string> authenticate()
        {
            this.authenticated = false;
            // https://stackoverflow.com/questions/32994464/could-not-create-ssl-tls-secure-channel-despite-setting-servercertificatevalida
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            LoginData data = new LoginData();
            data.email = this.username;
            data.password = this.password;

            var json = JsonConvert.SerializeObject(data);
            var dataString = new StringContent(json, Encoding.UTF8, "application/json");
            var r = await httpClient.PostAsync(NormalizeApiUrl("auth/login"), dataString);
            try
            {
                r.EnsureSuccessStatusCode();
            } catch (HttpRequestException err) {
                this.authenticated = false;
                throw err;
            }
            var responseString = await r.Content.ReadAsStringAsync();
            LoginResponse obj = JsonConvert.DeserializeObject<LoginResponse>(responseString);
            this.accessToken = obj.token;
            this.authenticated = true;
            return this.accessToken;
        }
        public async Task<DashboardResponse> getDb()
        {
            if (!authenticated) await authenticate();

            var request = new HttpRequestMessage(HttpMethod.Get, NormalizeApiUrl());
            request.Headers.Add("x-access-token", this.accessToken);
            var res = await httpClient.SendAsync(request);
            try
            {
                res.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException err)
            {
                this.authenticated = false;
                throw err;
            }
            var responseString = await res.Content.ReadAsStringAsync();
            var deserialized = JsonConvert.DeserializeObject<DashboardResponse>(responseString);
            return deserialized;
        }
        public async Task<ApiFolder> createFolder(string name, string parentFolder = null)
        {
            if (!authenticated) await authenticate();

            var request = new HttpRequestMessage(HttpMethod.Post, NormalizeUrlParams(
                            NormalizeApiUrl("folder/create"), "title", name, "parent_folder", parentFolder));
            request.Headers.Add("x-access-token", this.accessToken);
            var res = await httpClient.SendAsync(request);
            try
            {
                res.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException err)
            {
                this.authenticated = false;
                throw err;
            }
            var responseString = await res.Content.ReadAsStringAsync();
            var deserialized = JsonConvert.DeserializeObject<ApiFolder>(responseString);
            return deserialized;
        }
        public async Task renameFile(string file_uid, string newName) {
            if (!authenticated) await authenticate();

            var request = new HttpRequestMessage(HttpMethod.Put, NormalizeUrlParams(
                                NormalizeApiUrl("rename", file_uid), "name", newName));
            request.Headers.Add("x-access-token", this.accessToken);
            var res = await httpClient.SendAsync(request);
            try
            {
                res.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException err)
            {
                this.authenticated = false;
                throw err;
            }
            return;
        }
        public async Task deleteFile(string file_uid) {
            if (!authenticated) await authenticate();

            var request = new HttpRequestMessage(HttpMethod.Delete, NormalizeUrlParams(NormalizeApiUrl("file", file_uid)));
            request.Headers.Add("x-access-token", this.accessToken);
            var res = await httpClient.SendAsync(request);
            try
            {
                res.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException err)
            {
                this.authenticated = false;
                throw err;
            }
            return;
        }
        public void uploadFile(string fileSource, string parentFolder = null)
        {
            WebClient client = new WebClient();
            client.Credentials = CredentialCache.DefaultCredentials;
            client.Headers.Add("x-access-token", this.accessToken);
            client.UploadFile(NormalizeUrlParams(NormalizeApiUrl("file"), "folder_uid", parentFolder), "POST", fileSource);
            client.Dispose();
        }
        public async Task Dispose()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, NormalizeApiUrl("auth/logout"));
            await httpClient.SendAsync(request);
            httpClient.Dispose();
        }
        public void updateCredentials(string username, string password)
        {
            this.username = username;
            this.password = password;
            this.authenticated = false;
        }
    }
}
