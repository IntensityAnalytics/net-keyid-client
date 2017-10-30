using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;

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

        public JObject encodeJSONProperties(JObject data)
        {
            var objEncoded = new JObject();

            foreach (var jsonTuple in data)
            {
                string encodedData = WebUtility.UrlDecode(jsonTuple.Value.ToString());
                objEncoded[jsonTuple.Key.ToString()] = encodedData;
            }

            return objEncoded;
        }

        public Task<HttpResponseMessage> Post(string path, JObject data)
        {
            data["License"] = license;
            var dataEncoded = encodeJSONProperties(data);
            string dataEncodedJSON = dataEncoded.ToString();

            UriBuilder request = url;
            request.Path = path;

            var content = new StringContent("=[" + dataEncodedJSON + "]", Encoding.UTF8, "application/x-www-form-urlencoded");

            return client.PostAsync(request.Uri, content);
        }

        public Task<HttpResponseMessage> Get(string path, JObject data)
        {
            string query = "";

            foreach (var jsonTuple in data)
            {
                query = QueryHelpers.AddQueryString(query, jsonTuple.Key, jsonTuple.Value.ToString());
            }

            UriBuilder request = url;
            request.Path = path;

            // UriBuilder prepends '?' to Query every time you set it, so prevent duplication.
            if (query.Length > 1)
            {
                request.Query = query.Substring(1);
            }

            return client.GetAsync(request.Uri);
        }
    }
}
