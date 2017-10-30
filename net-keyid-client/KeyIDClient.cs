using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net;

namespace net_keyid_client
{
    public class KeyIDClient
    {
        public KeyIDSettings settings { get; set; }
        private KeyIDService service;

        public KeyIDClient(KeyIDSettings settings)
        {
            this.settings = settings;
            this.service = new KeyIDService(settings.url, settings.license, settings.timeout);
        }

        public KeyIDClient()
        {
            this.service = new KeyIDService(settings.url, settings.license, settings.timeout);
        }

        Task<JObject> SaveProfile(string entityID, string tsData, string sessionID)
        {
            // try to save profile without a toke
            return service.SaveProfile(entityID, tsData)
            .ContinueWith((response) =>
            {
                var data = new JObject();

                // token is required
                if (data["Error"].ToString() == "New enrollment code required.")
                {
                    // get a save token
                    return service.SaveToken(entityID, tsData)
                    .ContinueWith((tokenResponse) =>
                    {
                        var tokenData = ParseResponse(tokenResponse.Result);
                        // try to save profile with a token
                        return service.SaveProfile(entityID, tsData, tokenData["Token"].ToString());
                    })
                    .ContinueWith((tokenResponse) =>
                    {
                        var tokenData = new JObject();
                        //todo this isn't a task?
                        return tokenData;
                    });
                }

                return Task.FromResult(data);
            }).Unwrap();
        }

        JObject ParseResponse(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return JObject.Parse(response.Content.ToString());
            }
            else
            {
                throw new HttpRequestException("HTTP response not 200 OK.");
            }
        }
    }
}
