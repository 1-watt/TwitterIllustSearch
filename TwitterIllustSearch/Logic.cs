using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using CoreTweet;
using System.Net;
using System.Data;
using System.Net.Http;
using System.IO;
using System.Configuration;
using System.Data.SQLite;

namespace TwitterIllustSearch
{
    struct ImageSize
    {
        public int w;
        public int h;
    }

    public class Logic
    {
        private string baseDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');

        private string DbFileName = ConfigurationManager.AppSettings["DbFileName"];

        private string LogFileName = ConfigurationManager.AppSettings["LogFileName"];

        private string keyword = ConfigurationManager.AppSettings["keyword"];

        private int min_faves = Convert.ToInt32(ConfigurationManager.AppSettings["min_faves"]);

        private int min_retweets = Convert.ToInt32(ConfigurationManager.AppSettings["min_retweets"]);

        private int fooCount = Convert.ToInt32(ConfigurationManager.AppSettings["fooCount"]);

        private string[] ngWordsProfile = ConfigurationManager.AppSettings["ngWordsProfile"].Split(',');

        private string[] ngWords = ConfigurationManager.AppSettings["ngWords"].Split(',');

        private string[] ngScreenName = ConfigurationManager.AppSettings["ngScreenName"].Split(',');

        private IEnumerable<ImageSize> ngImageSize = ConfigurationManager.AppSettings["ngImageSize"].Split(',')
            .Select(wh => new ImageSize{w = Convert.ToInt32(wh.Split('*')[0]), h = Convert.ToInt32(wh.Split('*')[1])});

        /// <summary>
        /// メインの処理
        /// </summary>
        public void IllustSearch()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            var tokens = Tokens.Create(Key.APIKey, Key.APISecret, Key.AccessToken, Key.AccessSecret);

            string query = $"\"{keyword}\" min_faves:{min_faves} min_retweets:{min_retweets} filter:images -\"#{keyword}\"";

            var result = tokens.Search.Tweets(count => fooCount, q => query);

            foreach (var tweet in result)
            {
                // いいねじゃなければDiscordに投稿
                // if (!(bool)tokens.Statuses.Show(id => tweet.Id).IsFavorited && this.Filter(tweet))

                // DBに存在していなければDiscordに投稿
                if (!this.IsExistDb(tweet.Id) && this.Filter(tweet))
                {
                    string LinkToTweet = $"https://twitter.com/{tweet.User.ScreenName}/status/{tweet.Id}";

                    StringBuilder sb = new StringBuilder();

                    sb.Append($"@{tweet.User.ScreenName} {tweet.CreatedAt} : {LinkToTweet}");

                    // 2枚目以降のイラスト
                    for (int i = 1; i < tweet.ExtendedEntities.Media.Length; i++)
                    {
                        sb.Append(@"\n").Append(tweet.ExtendedEntities.Media[i].MediaUrlHttps);
                    }

                    this.PostDiscord(sb.ToString());

                    // いいねに追加
                    // tokens.Favorites.Create(id => tweet.Id);

                    // DBに追加
                    this.InsertTweetId(tweet.Id);

                    // ログのようなもの
                    File.AppendAllText($"{baseDirectory}\\{LogFileName}", LinkToTweet + "\n");
                }

            }
        }

        /// <summary>
        /// NGワードが含まれていないか（含まれない：true、含まれる：false）
        /// </summary>
        /// <param name="tweet"></param>
        /// <returns></returns>
        private bool Filter(Status tweet)
        {
            // 16:9のアスペクト比
            double ngAspectRatio_16_9 = 1.77;

            return !ngWords.Any(wd => tweet.Text.Contains(wd)) 
                && !ngWordsProfile.Any(wd => tweet.User.Description.Contains(wd))
                && !ngScreenName.Any(Id => tweet.User.ScreenName == Id)
                && !ngImageSize.Any(size => tweet.ExtendedEntities.Media.Any(media => media.Sizes.Large.Width == size.w && media.Sizes.Large.Height == size.h))
                && !tweet.ExtendedEntities.Media.Any(media => Math.Floor((double)media.Sizes.Large.Width / (double)media.Sizes.Large.Height * 100) / 100 == ngAspectRatio_16_9);
        }

        /// <summary>
        /// 引数の文字列をDiscordに投稿する
        /// </summary>
        /// <param name="message"></param>
        private void PostDiscord(string message)
        {
            string json = $"{{ \"content\" : \" {message} \" }}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using (var client = new HttpClient())
            {
                var response = client.PostAsync(Key.WebHookURL, content).GetAwaiter();
                response.GetResult();
            }
        }

        /// <summary>
        /// DBに存在するかどうか
        /// </summary>
        /// <param name="tweetId"></param>
        /// <returns></returns>
        private bool IsExistDb(long tweetId)
        {
            var sqlConnectionSb = new SQLiteConnectionStringBuilder { DataSource = $"{baseDirectory}\\{DbFileName}" };

            using (var cn = new SQLiteConnection(sqlConnectionSb.ToString()))
            {
                cn.Open();

                using (var cmd = new SQLiteCommand(cn))
                {
                    // テーブルがなければ作成
                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS IdLog(Id TEXT NOT NULL PRIMARY KEY)";
                    cmd.ExecuteNonQuery();

                    // 取得
                    var dataTable = new DataTable();
                    var adapter = new SQLiteDataAdapter($"SELECT * FROM IdLog WHERE Id = '{tweetId}'", cn);
                    adapter.Fill(dataTable);

                    // 既に存在していればtrue
                    return dataTable.AsEnumerable().Any(); 
                }
            }
        }

        /// <summary>
        /// DBにツイートIDを追加
        /// </summary>
        /// <param name="tweetId"></param>
        /// <returns></returns>
        private int InsertTweetId(long tweetId)
        {
            var sqlConnectionSb = new SQLiteConnectionStringBuilder { DataSource = $"{baseDirectory}\\{DbFileName}" };

            using (var cn = new SQLiteConnection(sqlConnectionSb.ToString()))
            {
                cn.Open();

                using (var cmd = new SQLiteCommand(cn))
                {
                    // 追加
                    cmd.CommandText = $"INSERT INTO IdLog (Id) VALUES ('{tweetId}')";
                    return cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
