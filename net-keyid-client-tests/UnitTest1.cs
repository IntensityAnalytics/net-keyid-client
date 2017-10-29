using System;
using Xunit;
using Newtonsoft.Json.Linq;
using net_keyid_client;

namespace net_keyid_client_tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var service = new KeyIDService("http://keyidservices.tickstream.com", "");
            var jo = JObject.Parse("{\"key1\":\"value1\"}");
            jo = JObject.Parse("{}");

            service.Get("/profile", ref jo).ContinueWith((response) =>
            {

                string body = response.Result.Content.ToString();
                Console.WriteLine(body);
            }).Wait();

            //service.Get("/profile", ref jo).Wait();
            service.blah().Wait();
        }
    }
}
