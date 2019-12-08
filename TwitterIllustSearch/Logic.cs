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
    public class Logic
    {
        private string keyword = ConfigurationManager.AppSettings["keyword"] 
                                + " min_faves:" + ConfigurationManager.AppSettings["min_faves"];

        private int fooCount = Convert.ToInt32(ConfigurationManager.AppSettings["fooCount"]);

        private string[] ngWordsProfile = ConfigurationManager.AppSettings["ngWordsProfile"].Split(',');

        private string[] ngWords = ConfigurationManager.AppSettings["ngWords"].Split(',');

        private string[] ngScreenName = ConfigurationManager.AppSettings["ngScreenName"].Split(',');

        /// <summary>
        /// メインの処理
        /// </summary>
        public void IllustSearch()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            var tokens = Tokens.Create(Key.APIKey, Key.APISecret, Key.AccessToken, Key.AccessSecret);

            var result = tokens.Search.Tweets(count => fooCount, q => keyword).Where(tw => tw.Entities.Media != null);

            foreach (var tweet in result)
            {
                // いいねじゃなければDiscordに投稿
                // if (!(bool)tokens.Statuses.Show(id => tweet.Id).IsFavorited && this.Filter(tweet))

                // DBに存在していなければDiscordに投稿
                if (!this.IsExistDb(tweet.Id) && this.Filter(tweet))
                {
                    string LinkToTweet = $"https://twitter.com/{tweet.User.ScreenName}/status/{tweet.Id}";

                    this.PostDiscord($"@{tweet.User.ScreenName} {tweet.CreatedAt} : {LinkToTweet}");

                    // 2枚目以降のイラストをPost
                    for (int i = 1; i < tweet.ExtendedEntities.Media.Length; i++)
                    {
                        this.PostDiscord(tweet.ExtendedEntities.Media[i].MediaUrlHttps);
                    }

                    // いいねに追加
                    // tokens.Favorites.Create(id => tweet.Id);

                    // DBに追加
                    this.InsertTweetId(tweet.Id);

                    // ログのようなもの
                    File.AppendAllText(Directory.GetCurrentDirectory() + @"\log.txt", LinkToTweet + "\n");
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
            return !ngWords.Any(wd => tweet.Text.Contains(wd)) 
                && !ngWordsProfile.Any(wd => tweet.User.Description.Contains(wd))
                && !ngScreenName.Any(Id => tweet.User.ScreenName == Id);
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

        private bool IsExistDb(long tweetId)
        {
            var sqlConnectionSb = new SQLiteConnectionStringBuilder { DataSource = $"{Directory.GetCurrentDirectory()}\\postedToDiscord.db" };

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

        private int InsertTweetId(long tweetId)
        {
            var sqlConnectionSb = new SQLiteConnectionStringBuilder { DataSource = $"{Directory.GetCurrentDirectory()}\\postedToDiscord.db" };

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
