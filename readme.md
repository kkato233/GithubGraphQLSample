## Github で リポジトリ一覧を Github API を使って取得する

github で 自分のリポジトリの一覧を Github API を使って取得します。

### APIキーを 準備

[Creating a personal access token for the command line](https://help.github.com/en/github/authenticating-to-github/creating-a-personal-access-token-for-the-command-line)

のページを参考に `Generate Token` ボタンをクリックして API アクセス用の Token を生成する。

一回だけしか表示されないので どこかにコピー＆ペーストしてなくさないようにしておく。

※ 古いキーを削除して新しいキーを作成してもよい。

### 取得したキーを 設定に保存する

```
dotnet user-secrets set Token "取得したTokenToken"
```

とするか

```
set TOKEN=取得したトークン
```

とします。

```
dotnet run
```

で GitHub での リポジトリ一覧が表示されます。

### 改造

[Github GraphAPI のドキュメント](https://developer.github.com/v4/)や
[実際に GraphAPI をブラウザ上で 実行できるページ](https://developer.github.com/v4/explorer/)

があるので それを参考に GraphQL の クエリーや 応答された Json の解析部分を修正します。
