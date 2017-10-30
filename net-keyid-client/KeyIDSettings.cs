using System;
using System.Collections.Generic;
using System.Text;

namespace net_keyid_client
{
    public class KeyIDSettings
    {
        public string license { get; set; } = "";
        public string url { get; set; } = "http://invalid.ivalid";
        public bool passiveValidation { get; set; } = false;
        public bool passiveEnrollment { get; set; } = false;
        public double thresholdConfidence { get; set; } = 70.0;
        public double thresholdFidelity { get; set; } = 50.0;
        public int timeout { get; set; } = 0;
        public bool strictSSL { get; set; } = true;
    }
}
