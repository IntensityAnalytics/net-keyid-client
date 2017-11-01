# net-keyid-client

A .NET standard 2.0 library to allow you to peform second factor authentication using Intensity Analytics TickStream.KeyID. Compatible with .NET core and .NET framework.

## Install

Include dll and reference in your project as any other .NET library.

## Usage

The keyid-client library provides several asynchronous functions that return TPL tasks.

```cs
using System;
using Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using net_keyid_client;

void main(){
	var settings = new KeyIDSettings
    {
        license = "yourlicense",
        url = "http://yourkeyidserver",
        passiveEnrollment = false,
        passiveValidation = false,
        customThreshold = false,
        thresholdConfidence = 70.0,
        thresholdFidelity = 50.0,
        timeout = 1000
    };

    string username = "someuser";

    var client = new KeyIDClient(settings);

    client.GetProfileInfo(username).Wait();

    client.RemoveProfile(username).Wait()

    double confidence;
    double fidelity;

	client.SaveProfile(username, "tsdata typing behavior")
	.ContinueWith((data) =>
	{
		confidence = data.Result.Value<double>("Confidence");
		fidelity = data.Result.Value<double>("Fidelity");
	})
	.Wait();
}
```