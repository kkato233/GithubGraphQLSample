using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace GithubGraphQL
{
    class Program
    {
        public static IConfigurationRoot Configuration { get; set; }

        static async Task Main(string[] args)
        {
            InitConfig();

            var list = await GetGithubRepositoies();

            // リポジトリの所有者による絞り込み
            string ownerFilter = Configuration["ownerFilter"];
            if (ownerFilter != null)
            {
                if (!ownerFilter.StartsWith("/"))
                {
                    ownerFilter = "/" + ownerFilter;
                }
                list = list
                    .Where(r => r.Owner.ResourcePath == ownerFilter)
                    .ToList();
            }

            foreach (var item in list)
            {
                Console.WriteLine($"{item.Name}");
            }
        }

        /// <summary>
        /// Github GraphQL 
        /// query { viewer { repositories(first:100)  で 応答されるデータの結果 を格納する領域
        /// </summary>
        public class RepostoryResult
        {
            public List<Repostory> Nodes { get; set; }
            public PageInfo PageInfo { get; set; }
        }

        /// <summary>
        /// リポジトリ
        /// https://developer.github.com/v4/object/repository/
        /// </summary>
        public class Repostory
        {
            public string Name { get; set; }
            public string Url { get; set; }

            /// <summary>
            /// /kkato233
            /// </summary>
            public Owner Owner { get; set; }
        }

        public class Owner
        {
            public string Url { get; set; }
            public string ResourcePath { get; set; }
        }
        public class PageInfo
        {
            public string EndCursor { get; set; }
            public bool HasNextPage { get; set; }
            public bool HasPreviousPage { get; set; }
            public string StartCursor { get; set; }
        }


        /// <summary>
        /// GitHub のリポジトリ一覧を取得する
        /// </summary>
        /// <returns></returns>PageInfo
        public static async Task<List<Repostory>> GetGithubRepositoies()
        {
            // github では 1回の取得件数に最大100件という制限があるため 
            // https://developer.github.com/v4/guides/resource-limitations/
            // 100 件以上のデータを取得するときは ページング処理を行う必要がある
            // https://graphql.org/learn/pagination/
            // https://github.community/t5/GitHub-API-Development-and/GraphQL-API-Pagination/td-p/22188

            string firstQuery = @"
query {
  viewer {
    repositories(first: 100) {
        nodes {
            url
            name
            owner {
              url
              resourcePath
            }
        }
        pageInfo {
            endCursor
            hasNextPage
            hasPreviousPage
            startCursor
        }
    }
  }
}";
            string nextQuery = @"
query {
  viewer {
    repositories(first: 100 after:$after ) {
        nodes {
            url
            name
            owner {
              url
              resourcePath
            }
        }
        pageInfo {
            endCursor
            hasNextPage
            hasPreviousPage
            startCursor
        }
    }
  }
}";
            List<Repostory> list = new List<Repostory>();

            // 1回目の取得
            string strResult1 = await GitHubApiExec(firstQuery);
            Debug.WriteLine(strResult1);
            RepostoryResult result1 = ParseRepositoriesResult(strResult1);

            if (result1?.Nodes != null)
            {
                list.AddRange(result1.Nodes);
            }

            // 続きの取得
            while (result1?.PageInfo?.HasNextPage ?? false)
            {
                string cursor = result1.PageInfo.EndCursor;

                string strResult2 = await GitHubApiExec(nextQuery.Replace("$after", "\"" + cursor + "\""));
                Debug.WriteLine(strResult2);
                result1 = ParseRepositoriesResult(strResult2);
                if (result1?.Nodes != null)
                {
                    list.AddRange(result1.Nodes);
                }
            }

            return list;
        }

        /// <summary>
        /// 応答電文の解析
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private static RepostoryResult ParseRepositoriesResult(string result)
        {
/* 受信データ（正常時）
{
    "data": {
    "viewer": {
        "repositories": {
        "nodes": [
            {
            "url": "https://github.com/kkato233/GithubGraphQLSample",
            "name": "GithubGraphQLSample"
            }]}}}
*/

/* 受信データ（異常時）
{
  "errors": [
    {
      "type": "MISSING_PAGINATION_BOUNDARIES",
      "path": [
        "viewer",
        "repositories"
      ],
      "locations": [
        {
          "line": 8,
          "column": 5
        }
      ],
      "message": "You must provide a `first` or `last` value to properly paginate the `repositories` connection."
    }
  ]
}
*/

            dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(result);

            // エラー発生チェック
            if (json.errors != null)
            {
                string errorMsg = json.errors[0].message;
                Debug.WriteLine(result);
                throw new ApplicationException(errorMsg);
            }

            // Dynamic 型を使って 取り出す Json データの絞り込み
            dynamic repos = json.data.viewer.repositories;

            var repositoryResult = Newtonsoft.Json.JsonConvert.DeserializeObject<RepostoryResult>(repos.ToString());

            return repositoryResult;
        }

        /// <summary>
        /// 定義の修正
        /// </summary>
        private static void InitConfig()
        {
            var builder = new ConfigurationBuilder();

            // 後ろほど優先度が高い
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            builder.AddUserSecrets<Program>();
            builder.AddEnvironmentVariables();

            Configuration = builder.Build();
        }


        /// <summary>
        /// GitHub GraphQL を実行してその結果を文字列として返す
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static async Task<string> GitHubApiExec(string query)
        {
            // TOKEN を 取得する
            string token = Configuration["Token"];
            if (token == null || token.Length < 40)
            {
                throw new ApplicationException("GitHUB API の Token を 設定してください。");
            }
            string url = "https://api.github.com/graphql";

            var queryJson = "{ \"query\": \"" + query
                .Replace("\r", " ") // 改行は空白に置き換え
                .Replace("\n", " ") 
                .Replace("\"", "\\\"") // " は \" に置き換え
                + "\"}";

            var content = new StringContent(queryJson, Encoding.UTF8, @"application/json");

            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("User-Agent", @"System.Net.Http.HttpClient"); // User-Agent を未指定だと Github 側がエラーを返すため
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
            var result = await client.PostAsync(url, content);

            if (result.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                // https://graphql.org/learn/best-practices/ で zip 圧縮をするようにアドバイスがあったため
                // ZIP 圧縮 
                var stream = await result.Content.ReadAsStreamAsync();
                using (GZipStream zipStream = new GZipStream(stream, CompressionMode.Decompress))
                {
                    using (System.IO.StreamReader sr = new System.IO.StreamReader(zipStream))
                    {
                        string s = await sr.ReadToEndAsync();
                        return s;
                    }
                }
            }
            else
            {
                string s = await result.Content.ReadAsStringAsync();
                return s;
            }
        }
    }


    
}
