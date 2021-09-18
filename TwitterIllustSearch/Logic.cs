using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using CoreTweet;
using System.Net;
using System.Data;
using System.Net.Http;
using System.IO;
using System.Data.SQLite;
using System.Text.Json;

namespace TwitterIllustSearch
{
    public class Logic
    {
        /// <summary>
        /// 設定
        /// </summary>
        private Config config;

        /// <summary>
        /// メインの処理
        /// </summary>
        public void IllustSearch()
        {
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Config.json")));

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            var tokens = Tokens.Create(config.TwitterAPI.APIKey, config.TwitterAPI.APISecret, config.TwitterAPI.AccessToken, config.TwitterAPI.AccessSecret);

            string query = $"\"{config.keyword}\" min_faves:{config.min_faves} min_retweets:{config.min_retweets} filter:images -\"#{config.keyword}\"";

            var result = tokens.Search.Tweets(count => config.fooCount, q => query);

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
                        sb.AppendLine().Append(tweet.ExtendedEntities.Media[i].MediaUrlHttps);
                    }

                    this.PostDiscord(sb.ToString());

                    // いいねに追加
                    // tokens.Favorites.Create(id => tweet.Id);

                    // DBに追加
                    this.InsertTweetId(tweet.Id);

                    // ログのようなもの
                    File.AppendAllText(Path.Combine(AppContext.BaseDirectory, config.LogFileName), LinkToTweet + Environment.NewLine);
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

            return !config.ngWords.Any(wd => tweet.Text.Contains(wd)) 
                && !config.ngWordsProfile.Any(wd => tweet.User.Description.Contains(wd))
                && !config.ngScreenName.Any(Id => tweet.User.ScreenName == Id)
                && !config.ngImageSize.Any(size => tweet.ExtendedEntities.Media.Any(media => media.Sizes.Large.Width == size.w && media.Sizes.Large.Height == size.h))
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
                var response = client.PostAsync(config.WebHookURL, content).GetAwaiter();
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
            var sqlConnectionSb = new SQLiteConnectionStringBuilder { DataSource = Path.Combine(AppContext.BaseDirectory, config.DbFileName) };

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
            var sqlConnectionSb = new SQLiteConnectionStringBuilder { DataSource = Path.Combine(AppContext.BaseDirectory, config.DbFileName) };

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
