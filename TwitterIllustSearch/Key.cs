using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;

namespace TwitterIllustSearch
{
    class Key
    {
        public static readonly string APIKey = ConfigurationManager.AppSettings["APIKey"];

        public static readonly string APISecret = ConfigurationManager.AppSettings["APISecret"];

        public static readonly string AccessToken = ConfigurationManager.AppSettings["AccessToken"];

        public static readonly string AccessSecret = ConfigurationManager.AppSettings["AccessSecret"];


        public static readonly string WebHookURL = ConfigurationManager.AppSettings["WebHookURL"];
    }
}
