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
            string dataEncodedJSON = dataEncoded.ToString(Formatting.None);

            UriBuilder request = url;
            request.Path = path;

            var content = new StringContent("=[" + dataEncodedJSON + "]", Encoding.UTF8,  "application/x-www-form-urlencoded");

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

        public Task<HttpResponseMessage> TypingMistake(string entityID, string mistype = "", string sessionID = "", string source = "", string action = "", string tmplate = "", string page = "")
        {
            var data = new JObject();
            data["EntityID"] = entityID;
            data["Mistype"] = mistype;
            data["SessionID"] = sessionID;
            data["Source"] = source;
            data["Action"] = action;
            data["Template"] = tmplate;
            data["Page"] = page;

            return Post("/typingmistake", data);
        }

        public Task<HttpResponseMessage> EvaluateSample(string entityID, string tsData, string nonce)
        {
            var data = new JObject();
            data["EntityID"] = entityID;
            data["tsData"] = tsData;
            data["Nonce"] = nonce;
            data["Return"] = "JSON";
            data["Statistics"] = "extended";

            return Post("/evaluate", data);
        }

        public Task<HttpResponseMessage> Nonce(ulong nonceTime)
        {
            var data = new JObject();
            data["type"] = "nonce";
            string path = "/token/" + nonceTime;
            return Get(path, data);
        }

        public Task<HttpResponseMessage> RemoveToken(string entityID, string tsData)
        {
            var data = new JObject();
            data["Type"] = "remove";
            data["Return"] = "value";

            return Get("/token" + entityID, data)
            .ContinueWith((response) => 
            {
                var postData = new JObject();
                postData["EntityID"] = entityID;
                postData["Token"] = response.Result.Content.ToString();
                postData["ReturnToken"] = "True";
                postData["ReturnValidation"] = tsData;
                postData["Type"] = "remove";
                postData["Return"] = "JSON";

                return Post("/token", postData);
            }).Unwrap();
        }

        public Task<HttpResponseMessage> RemoveProfile(string entityID, string token)
        {
            var data = new JObject();
            data["EntityID"] = entityID;
            data["Code"] = token;
            data["Action"] = "remove";
            data["Return"] = "JSON";

            return Post("/profile", data);
        }

        public Task<HttpResponseMessage> SaveToken(string entityID, string tsData)
        {
            var data = new JObject();
            data["Type"] = "enrollment";
            data["Return"] = "value";

            return Get("/token" + entityID, data)
            .ContinueWith((response) =>
            {
                var postData = new JObject();
                postData["EntityID"] = entityID;
                postData["Token"] = response.Result.Content.ToString();
                postData["ReturnToken"] = "True";
                postData["ReturnValidation"] = tsData;
                postData["Type"] = "enrollment";
                postData["Return"] = "JSON";

                return Post("/token", postData);
            }).Unwrap();
        }

        public Task<HttpResponseMessage> SaveProfile(string entityID, string tsData, string code = "")
        {
            var data = new JObject();
            data["EntityID"] = entityID;
            data["tsData"] = tsData;
            data["Return"] = "JSON";
            data["Action"] = "v2";
            data["Statistics"] = "extended";

            if (code != "")
                data["Code"] = code;

            return Post("/profile", data);
        }

        public Task<HttpResponseMessage> GetProfileInfo(string entityID)
        {
            var data = new JObject();
            string path = "/profile/" + entityID;
            return Get(path, data);
        }
    }
}
