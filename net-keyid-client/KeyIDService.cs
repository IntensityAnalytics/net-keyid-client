using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;

namespace net_keyid_client
{
    public class KeyIDService
    {
        private UriBuilder url;
        private string license;
        private HttpClient client;

        public KeyIDService(string url, string license, int timeoutMs = 1000)
        {
            this.url = new UriBuilder(url);
            this.license = license;
            client = new HttpClient();
        }

        private Task<HttpResponseMessage> Get(string path, JObject data)
        {
            string query = "";

            foreach (var jsonTuple in data)
            {
                query = QueryHelpers.AddQueryString(query, jsonTuple.Key, jsonTuple.Value.ToString());
            }

            UriBuilder request = url;
            request.Path = path;
            request.Query = query;

            return client.GetAsync(request.Uri);
        }
    }
}
