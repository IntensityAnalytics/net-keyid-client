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

        Task<JObject> RemoveProfile(string entityID, string tsData, string sessionID)
        {
            // get a removal token
            return service.RemoveToken(entityID, tsData)
            .ContinueWith((response) =>
            {
                var data = ParseResponse(response.Result);

                // remove profile
                if (data["Token"] != null)
                {
                    return service.RemoveProfile(entityID, data["Token"].ToString())
                    .ContinueWith((removeResponse) =>
                    {
                        var removeData = ParseResponse(removeResponse.Result);
                        return Task.FromResult(removeData);
                    }).Unwrap();
                }
                else
                    return Task.FromResult(data);
            }).Unwrap();
        }

        Task<JObject> EvaluateProfile(string entityID, string tsData, string sessionID)
        {
            long nonceTime = DateTime.Now.Ticks;

            return service.Nonce(nonceTime)
            .ContinueWith((response) =>
            {
                return service.EvaluateSample(entityID, tsData, response.Result.ToString());
            }).Unwrap()
            .ContinueWith((response) => 
            {
                var data = ParseResponse(response.Result);

                // check for error before continuing
                // todo check "error" exists first?
                if (data["Error"].ToString() == "")
                {
                    // coerce string to boolea
                    data["Match"] = AlphaToBool(data["Match"].ToString());
                    data["IsReady"] = AlphaToBool(data["IsReady"].ToString());

                    // set match to true and return early if using passive validatio
                    if (settings.passiveValidation)
                    {
                        data["Match"] = true;
                        return data;
                    }
                    // evaluate match value using custom threshold if enabled
                    else if (settings.customThreshold)
                    {
                        // todo debug this line might be problematic getting doubles
                        data["Match"] = EvalThreshold(data.Value<double>("Confidence"), data.Value<double>("Fidelity"));
                    }
                }

                return data;
            });
        }

        bool EvalThreshold(double confidence, double fidelity)
        {
            if (confidence >= settings.thresholdConfidence &&
                fidelity >= settings.thresholdFidelity)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        bool AlphaToBool(string input)
        {
            input = input.ToUpper();

            if (input == "TRUE")
                return true;
            else
                return false;
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
