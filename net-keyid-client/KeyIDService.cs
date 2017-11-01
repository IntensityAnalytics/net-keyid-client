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

        /// <summary>
        /// KeyID services REST client.
        /// </summary>
        /// <param name="url">KeyID services URL.</param>
        /// <param name="license">KeyID services license key.</param>
        /// <param name="timeoutMs">REST web service timeout.</param>
        public KeyIDService(string url, string license, int timeoutMs = 1000)
        {
            this.url = new UriBuilder(url);
            this.license = license;
            client = new HttpClient();
        }

        /// <summary>
        /// URL encodes the properties of a JSON object
        /// </summary>
        /// <param name="obj">JSON object</param>
        /// <returns>URL encoded JSON object</returns>
        public JObject encodeJSONProperties(JObject data)
        {
            var objEncoded = new JObject();

            foreach (var jsonTuple in data)
            {
                string encodedData = WebUtility.UrlEncode(jsonTuple.Value.ToString());
                objEncoded[jsonTuple.Key.ToString()] = encodedData;
            }

            return objEncoded;
        }

        /// <summary>
        /// Performs a HTTP post to KeyID REST services.
        /// </summary>
        /// <param name="path">REST URI suffix.</param>
        /// <param name="data">Object that will be converted to JSON and sent in POST request.</param>
        /// <returns>REST request and response.</returns>
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

        /// <summary>
        /// Performs a HTTP get to KeyID REST services.
        /// </summary>
        /// <param name="path">REST URI suffix.</param>
        /// <param name="data">Object that will be converted to URL parameters and sent in GET request.</param>
        /// <returns>REST request and response.</returns>
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

        /// <summary>
        /// Log a typing mistake to KeyID REST services.
        /// </summary>
        /// <param name="entityID">Profile name.</param>
        /// <param name="mistype">Typing mistake.</param>
        /// <param name="sessionID">Session identifier for logging purposes.</param>
        /// <param name="source">Application name or identifier.</param>
        /// <param name="action">Action being performed at time of mistake.</param>
        /// <param name="tmplate"></param>
        /// <param name="page"></param>
        /// <returns>REST request and response.</returns>
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

        /// <summary>
        /// Evaluate typing sample.
        /// </summary>
        /// <param name="entityID">Profile name.</param>
        /// <param name="tsData">Typing sample to evaluate against profile.</param>
        /// <param name="nonce">Evaluation nonce.</param>
        /// <returns>REST request and response.</returns>
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

        /// <summary>
        /// Retrieve a nonce.
        /// </summary>
        /// <param name="nonceTime">Current time in .Net ticks.</param>
        /// <returns>REST request and response.</returns>
        public Task<HttpResponseMessage> Nonce(long nonceTime)
        {
            var data = new JObject();
            data["type"] = "nonce";
            string path = "/token/" + nonceTime;
            return Get(path, data);
        }

        /// <summary>
        /// Retrieve a profile removal security token.
        /// </summary>
        /// <param name="entityID">Profile name.</param>
        /// <param name="tsData">Optional typing sample for removal authorization.</param>
        /// <returns>REST request and response.</returns>
        public Task<HttpResponseMessage> RemoveToken(string entityID, string tsData)
        {
            var data = new JObject();
            data["Type"] = "remove";
            data["Return"] = "value";

            return Get("/token/" + entityID, data)
            .ContinueWith((response) => 
            {
              
                var postData = new JObject();
                postData["EntityID"] = entityID;
                postData["Token"] = response.Result.Content.ReadAsStringAsync().Result;
                postData["ReturnToken"] = "True";
                postData["ReturnValidation"] = tsData;
                postData["Type"] = "remove";
                postData["Return"] = "JSON";

                return Post("/token", postData);
            }).Unwrap();
        }

        /// <summary>
        /// Remove a profile.
        /// </summary>
        /// <param name="entityID">Profile name.</param>
        /// <param name="token">Profile removal security token.</param>
        /// <returns>REST request and response.</returns>
        public Task<HttpResponseMessage> RemoveProfile(string entityID, string token)
        {
            var data = new JObject();
            data["EntityID"] = entityID;
            data["Code"] = token;
            data["Action"] = "remove";
            data["Return"] = "JSON";

            return Post("/profile", data);
        }

        /// <summary>
        /// Retrieve a profile save security token.
        /// </summary>
        /// <param name="entityID">Profile name.</param>
        /// <param name="tsData">Optional typing sample for save authorization.</param>
        /// <returns>REST request and response.</returns>
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
                postData["Token"] = response.Result.Content.ReadAsStringAsync().Result;
                postData["ReturnToken"] = "True";
                postData["ReturnValidation"] = tsData;
                postData["Type"] = "enrollment";
                postData["Return"] = "JSON";

                return Post("/token", postData);
            }).Unwrap();
        }

        /// <summary>
        /// Save a profile.
        /// </summary>
        /// <param name="entityID">Profile name.</param>
        /// <param name="tsData">Typing sample to save.</param>
        /// <param name="code">Profile save security token.</param>
        /// <returns>REST request and response.</returns>
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

        /// <summary>
        /// Get profile information.
        /// </summary>
        /// <param name="entityID">Profile name.</param>
        /// <returns>REST request and response.</returns>
        public Task<HttpResponseMessage> GetProfileInfo(string entityID)
        {
            var data = new JObject();
            string path = "/profile/" + entityID;
            return Get(path, data);
        }
    }
}
