using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitterIllustSearch
{
    public class Config
    {
        public Twitterapi TwitterAPI { get; set; }
        public string WebHookURL { get; set; }
        public string DbFileName { get; set; }
        public string LogFileName { get; set; }
        public string keyword { get; set; }
        public int min_faves { get; set; }
        public int fooCount { get; set; }
        public int min_retweets { get; set; }
        public string[] ngWordsProfile { get; set; }
        public string[] ngWords { get; set; }
        public string[] ngScreenName { get; set; }
        public Ngimagesize[] ngImageSize { get; set; }
    }

    public class Twitterapi
    {
        public string APIKey { get; set; }
        public string APISecret { get; set; }
        public string AccessToken { get; set; }
        public string AccessSecret { get; set; }
    }

    public class Ngimagesize
    {
        public int w { get; set; }
        public int h { get; set; }
    }
}
