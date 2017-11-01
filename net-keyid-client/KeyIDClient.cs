﻿using System;
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

        /// <summary>
        /// KeyID services client.
        /// </summary>
        /// <param name="settings"> KeyID settings struct</param>
        public KeyIDClient(KeyIDSettings settings)
        {
            this.settings = settings;
            this.service = new KeyIDService(settings.url, settings.license, settings.timeout);
        }

        public KeyIDClient()
        {
            this.service = new KeyIDService(settings.url, settings.license, settings.timeout);
        }

        /// <summary>
        /// Saves a given KeyID profile entry.
        /// </summary>
        /// <param name="entityID">Profile name to save.</param>
        /// <param name="tsData">Typing sample data to save.</param>
        /// <param name="sessionID">Session identifier for logging purposes.</param>
        /// <returns>JSON value (task)</returns>
        public Task<JObject> SaveProfile(string entityID, string tsData, string sessionID = "")
        {
            // try to save profile without a toke
            return service.SaveProfile(entityID, tsData)
            .ContinueWith((response) =>
            {
                var data = ParseResponse(response.Result);

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

        /// <summary>
        /// Removes a KeyID profile.
        /// </summary>
        /// <param name="entityID">Profile name to remove.</param>
        /// <param name="tsData">Optional typing sample for removal authorization.</param>
        /// <param name="sessionID">Session identifier for logging purposes.</param>
        /// <returns>JSON value (task)</returns
        public Task<JObject> RemoveProfile(string entityID, string tsData = "", string sessionID = "")
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

        /// <summary>
        /// Evaluates a KeyID profile.
        /// </summary>
        /// <param name="entityID">Profile name to evaluate.</param>
        /// <param name="tsData">Typing sample to evaluate against profile.</param>
        /// <param name="sessionID">Session identifier for logging purposes.</param>
        /// <returns></returns
        public Task<JObject> EvaluateProfile(string entityID, string tsData, string sessionID = "")
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

        /// <summary>
        /// Evaluates a given profile and adds typing sample to profile.
        /// </summary>
        /// <param name="entityID">Profile to evaluate.</param>
        /// <param name="tsData">Typing sample to evaluate and save.</param>
        /// <param name="sessionID">Session identifier for logging purposes.</param>
        /// <returns></returns
        public Task<JObject> LoginPassiveEnrollment(string entityID, string tsData, string sessionID = "")
        {
            return EvaluateProfile(entityID, tsData, sessionID)
            .ContinueWith((data) =>
            {
                // in base case that no profile exists save profile async and return earl
                if (data.Result["Error"].ToString() == "EntityID does not exist." ||
                    data.Result["Error"].ToString() == "The profile has too little data for a valid evaluation." ||
                    data.Result["Error"].ToString() == "The entry varied so much from the model, no evaluation is possible.")
                {
                    return SaveProfile(entityID, tsData, sessionID)
                    .ContinueWith((saveData) =>
                    {
                        var evalData = data.Result;
                        evalData["Match"] = true;
                        evalData["IsReady"] = false;
                        evalData["Confidence"] = 100.0;
                        evalData["Fidelity"] = 100.0;
                        return evalData;
                    });
                }

                // if profile is not ready save profile async and return early
                if (data.Result["Error"].ToString() == "" && data.Result.Value<bool>("IsReady") == false)
                {
                    return SaveProfile(entityID, tsData, sessionID)
                    .ContinueWith((saveData) =>
                    {
                        var evalData = data.Result;
                        evalData["Match"] = true;
                        return evalData;
                    });
                }

                return Task.FromResult(data.Result);
            }).Unwrap();
        }

        /// <summary>
        /// Returns profile information without modifying the profile.
        /// </summary>
        /// <param name="entityID">Profile to inspect.</param>
        /// <returns></returns
        public Task<JObject> GetProfileInfo(string entityID)
        {
            return service.GetProfileInfo(entityID)
            .ContinueWith((response) =>
            {
                var data = ParseGetProfileResponse(response.Result);
                return Task.FromResult(data);
            }).Unwrap();
        }

        /// <summary>
        /// Compares a given confidence and fidelity against pre-determined thresholds.
        /// </summary>
        /// <param name="confidence">KeyID evaluation confidence.</param>
        /// <param name="fidelity">KeyID evaluation fidelity.</param>
        /// <returns>Whether confidence and fidelity meet thresholds.</returns>
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

        /// <summary>
        /// Converts a string value like 'true' to a boolean object.
        /// </summary>
        /// <param name="input">String to convert to boolean.</param>
        /// <returns>Boolean value.</returns
        bool AlphaToBool(string input)
        {
            input = input.ToUpper();

            if (input == "TRUE")
                return true;
            else
                return false;
        }

        /// <summary>
        /// Extracts a JSON value from a http_response
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <returns>JSON value</returns
        JObject ParseResponse(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.OK)
            {
                string content = response.Content.ReadAsStringAsync().Result;
                var obj = JObject.Parse(content);
                return obj;
            }
            else
            {
                throw new HttpRequestException("HTTP response not 200 OK.");
            }
        }

        /// <summary>
        /// Extracts a JSON value from a http_response
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <returns>JSON value</returns>
        JObject ParseGetProfileResponse(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.OK)
            {
                string content = response.Content.ReadAsStringAsync().Result;
                var obj = JArray.Parse(content);
                return (JObject)obj[0];
            }
            else
            {
                throw new HttpRequestException("HTTP response not 200 OK.");
            }
        }
    }
}
