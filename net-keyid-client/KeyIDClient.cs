using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net;

namespace IntensityAnalytics
{
    namespace KeyID
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
                long nonceTime = DateTime.UtcNow.Ticks;

                return service.Nonce(nonceTime)
                .ContinueWith((response) =>
                {
                    return service.EvaluateSample(entityID, tsData, response.Result.Content.ReadAsStringAsync().Result);
                }).Unwrap()
                .ContinueWith((response) =>
                {
                    var data = ParseResponse(response.Result);

                // check for error before continuing
                if (data["Error"].ToString() == "")
                    {
                    // coerce string to boolea
                    data["Match"] = AlphaToBool(data["Match"].ToString());
                        data["IsReady"] = AlphaToBool(data["IsReady"].ToString());

                    // evaluate match value using custom threshold if enabled
                    if (settings.customThreshold)
                        {
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
            public Task<JObject> EvaluateEnrollProfile(string entityID, string tsData, string sessionID = "")
            {
                return EvaluateProfile(entityID, tsData, sessionID)
                .ContinueWith((data) =>
                {
                // in base case that no profile exists save profile async and return earl
                if (data.Result["Error"].ToString() == "EntityID does not exist.")
                    {
                        return SaveProfile(entityID, tsData, sessionID)
                        .ContinueWith((saveData) =>
                        {
                            if (saveData.Result["Error"].ToString() == "")
                            {
                                var evalData = data.Result;
                                evalData["Error"] = "";
                                evalData["Match"] = true;
                                evalData["IsReady"] = false;
                                evalData["Confidence"] = 100.0;
                                evalData["Fidelity"] = 100.0;
                                evalData["Profiles"] = 0;
                                return evalData;
                            }
                            else
                                return SaveErrorResult();
                        });
                    }

                // if profile is not ready save profile async and return early
                if (data.Result.Value<bool>("IsReady") == false)
                    {
                    // profile is not ready and evaluation is free of error
                    if (data.Result["Error"].ToString() == "")
                        {
                            return SaveProfile(entityID, tsData, sessionID)
                            .ContinueWith((saveData) =>
                            {
                                if (saveData.Result["Error"].ToString() == "")
                                {
                                    var evalData = data.Result;
                                    evalData["Error"] = "";
                                    evalData["Match"] = true;
                                    return evalData;
                                }
                                else
                                    return SaveErrorResult();
                            });
                        }

                        else if (data.Result["Error"].ToString() == "The profile has too little data for a valid evaluation." ||
                                 data.Result["Error"].ToString() == "The entry varied so much from the model, no evaluation is possible.")
                        {
                            return SaveProfile(entityID, tsData, sessionID)
                            .ContinueWith((saveData) =>
                            {
                                if (saveData.Result["Error"].ToString() == "")
                                {
                                    var evalData = data.Result;
                                    evalData["Error"] = "";
                                    evalData["Match"] = true;
                                    evalData["Confidence"] = 100.0;
                                    evalData["Fidelity"] = 100.0;
                                    return evalData;
                                }
                                else
                                    return SaveErrorResult();
                            });
                        }
                    }

                    return Task.FromResult(data.Result);
                }).Unwrap();
            }

            /// <summary>
            /// JSON Object to return when saving a profile is encountered.
            /// </summary>
            /// <returns></returns>
            private JObject SaveErrorResult()
            {
                JObject result = new JObject();
                result["Error"] = "Error saving profile.";
                result["Match"] = false;
                result["IsReady"] = false;
                result["Confidence"] = 0;
                result["Fidelity"] = 0;
                result["Profiles"] = 0;
                return result;
            }

            /// <summary>
            /// Courtesy function that choses normal evaluation or passive enrollment to simplify calling code.
            /// </summary>
            /// <param name="entityID"></param>
            /// <param name="tsData"></param>
            /// <param name="sessionID"></param>
            /// <returns></returns>
            public Task<JObject> Login(string entityID, string tsData, string sessionID = "")
            {
                if (settings.loginEnrollment)
                    return EvaluateEnrollProfile(entityID, tsData, sessionID);
                else
                    return EvaluateProfile(entityID, tsData, sessionID);
            }

            /// <summary>
            /// Returns profile information without modifying the profile.
            /// </summary>
            /// <param name="entityID">Profile to inspect.</param>
            /// <returns></returns>
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
            /// Store profile measurements without returning an evaluation result.
            /// </summary>
            /// <param name="entityID">Profile to inspect.</param>
            /// <param name="tsData">Typing sample to save.</param>
            /// <param name="entityNotes">Additional notes/context to save.</param>
            /// <returns></returns>
            public Task<JObject> Monitor(string entityID, string tsData, string entityNotes = "")
            {
                return service.Monitor(entityID, tsData, entityNotes)
                .ContinueWith((response) =>
                {
                    var data = ParseResponse(response.Result);
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
                    var obj = JToken.Parse(content);

                    if (obj.Type == JTokenType.Array)
                        return (JObject)obj[0];
                    return
                        (JObject)obj;
                }
                else
                {
                    throw new HttpRequestException("HTTP response not 200 OK.");
                }
            }
        }
    }
}
