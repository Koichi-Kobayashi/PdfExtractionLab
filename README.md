# PdfExtractionLab

ScanSnapの検索可能PDFを念頭に、同じ入力に対するPdfPigとOnePDFの抽出結果をWindows上で比較するWPFアプリです。処方箋項目の推定、OCR、QRの読み取り、ページ全体のレンダリングは対象外です。PDFの解析・差分・保存はローカルで完結します。入力を読み取り専用で開きます。

## 起動

Windows 11 x64、日本語フォント（試料の再生成はMS ゴシック）、.NET 10 SDKを使用します。初回のパッケージ復元にはNuGetへの接続または取得済みキャッシュが必要です。実行時にPDFや抽出結果を送信する機能はありません。検証環境はSDK 10.0.404、Desktop Runtime 10.0.15です。global.jsonはインストール済みの10系SDKを選びます。

~~~powershell
cd C:\Users\kobayashi\source\repos\PdfExtractionLab
dotnet restore PdfExtractionLab.slnx --locked-mode
dotnet build PdfExtractionLab.slnx -c Release --no-restore
dotnet test PdfExtractionLab.Tests -c Release --no-build
dotnet run --project PdfExtractionLab.App -c Release --no-build
~~~

Visual Studioでは `PdfExtractionLab.slnx` を開き、Appをスタートアッププロジェクトに指定します。

## 比較する

1. `fixtures/japanese-ocr-jpeg.pdf` をドロップするか、参照ボタンまたは直接入力で選び、「比較実行」を押します。左がPdfPig、右がOnePDFです。上部にエンジンのバージョンと計測値を表示します。
2. 既定はコンテンツ順です。読み取り順を選んで再実行すると、PdfPigのDocstrumと読み取り順検出、OnePDFの読み取り順解析を使います。出力文字列は変更せず、原文の選択・コピー、フォントサイズ変更、スクロール同期、ページ切り替えと全文表示ができます。全文表示のページ境界にはアプリがFFを挿入します。集計文字数からは除きます。
3. 「差分」は対象ページの変化箇所をUnicodeスカラー位置とともに示します。左から右を見て追加、欠落、置換を区別します。並び替えは削除・挿入として現れます。既定は完全一致です。横空白を無視するとタブとUnicodeのSpaceSeparatorを取り除き、改行は保持します。NFCは比較用コピーだけに適用します。正解テキストには同名のUTF-8のtxtを指定し、比較するエンジンを選べます。
4. 「座標」でエンジンと文字・単語・行・ブロックを選ぶと、DataGridとCanvasに同じ結果を表示します。RawはAPIの矩形を包む軸平行矩形、X・Yはページ左上基準のptです。ログと比較資料に取得範囲を記載しました。
5. 「画像」ではページ内の描画配置を選択し、元ストリームと表示用データを個別に保存できます。JPEG原本はJPEGファイルとして開けます。Flate原本のbinにはPDF辞書が別途必要です。画像保存のボタンは画像内容そのものの明示出力です。
6. エクスポートはJSON・CSV・Markdown・テキストに対応します。既定では集計値、バージョン、OS、CPU、物理メモリ、条件、エラー型、警告数を記録します。本文・座標の文字・診断の詳細はチェックボックスを選んだときだけ含まれます。画像バイト列は画像タブから保存します。エラーの内容はログで確認できます。

## 測定する

WPFではベンチマークタブで条件を指定して実行します。既定は100回・メモリ・開き直し・1ワーカー・ウォームアップありです。Fileは両エンジンが同じファイルを読み取り専用で開き、Memoryは同一バイト列を使います。Reuseはワーカーごとに文書を保持します。2・4ワーカーでは各ワーカーが独立した文書を開き、ページ番号を分担します。エンジン間は常に逐次実行します。キャンセルはページ間と、対応するライブラリ呼び出し内で確認します。

正式測定はデバッガーを外したReleaseで実行します。以下は同じ日本語試料の条件違いです。

~~~powershell
dotnet run --project PdfExtractionLab.Benchmarks -c Release -- fixtures/japanese-ocr-jpeg.pdf --iterationTime 100
dotnet run --project PdfExtractionLab.Benchmarks -c Release -- fixtures/japanese-ocr-jpeg.pdf --file --reopen --filter "*Total*" --iterationTime 200
dotnet run --project PdfExtractionLab.Benchmarks -c Release -- fixtures/japanese-ocr-jpeg.pdf --cold --file --reopen --filter "*Total*"
~~~

BDNの既定はMemory・Reuse・単一ワーカー・ウォームアップ3反復・実測100反復です。各反復内の呼び出し回数はBDNが調整します。`--quick` は実測5反復、`--cold` は新規プロセス5回で各1回です。初回のディスクキャッシュの状態はOSに依存します。生の測定成果物は `BenchmarkDotNet.Artifacts` に生成します。

## プロジェクト

| プロジェクト | 役割 |
|---|---|
| PdfExtractionLab.App | WPF UI、CommunityToolkit.Mvvm、ファイル操作、表示 |
| PdfExtractionLab.Core | エンジンと文書セッションのインターフェース、ページ・座標・画像モデル、差分、測定、エクスポート |
| PdfExtractionLab.Engines.PdfPig | PdfPig専用アダプター、レイアウト解析・ログ捕捉 |
| PdfExtractionLab.Engines.OnePdf | OnePDF専用アダプター、グリフ・画像デバイス |
| PdfExtractionLab.Benchmarks | 独立プロセスのBenchmarkDotNetジョブ |
| PdfExtractionLab.Tests | 単体・結合・実ウィンドウ試験、C#による合成PDF生成 |

新しいエンジンは `IPdfExtractionEngine` と `IPdfSession` を実装して登録します。画像モデルは抽出元のバイト列と表示用のバイト列を分けており、将来のローカルQRデコーダーに渡せます。

## テストデータの再生成

同梱の14種類はすべて合成データです。環境変数を設定したときだけ、生成テストが指定フォルダーの同名の試料と観察結果を更新します。別フォルダーでも実行できます。32 MiBの負荷試験用PDFはテスト用一時フォルダーに生成し試験後に削除します。

~~~powershell
$env:PDFLAB_FIXTURES = Join-Path $PWD fixtures
dotnet test PdfExtractionLab.Tests -c Release --filter "FullyQualifiedName~CheckSyntheticCorpus"
Remove-Item Env:PDFLAB_FIXTURES
~~~

## 実行確認

各PhaseのReleaseビルドと、段階ごとの2件、7件、10件、最終21件のテストを実行しました。最終ビルドの警告・エラーは0件です。WPFの実ウィンドウで300ページを比較し、32 MiB超の32ページ画像PDFの解析中にDispatcherタイマーが動くこと、解析開始後のキャンセルと後続処理を確認しました。生成したexeを別途起動し、パス入力→比較→画像タブのJPEG表示→テーマ切替→終了をUI Automationから確認しました。

4形式で本文・警告詳細が既定出力に含まれないこと、明示選択で含まれること、ファイルの排他再オープン、読み取り順、回転4方向、CropBox、UserUnit=2、読めないPDFの失敗数をテストしています。長期のリーク試験、複雑な透明合成、実機ScanSnap由来のPDFは未検証です。取得できた図形や画像はページ単位で実体化するため、数百枚の高解像度カラー画像ではメモリ量を別に評価してください。

## パッケージと検証結果

| 直接参照 | 安定版 | NuGetのライセンス |
|---|---:|---|
| PdfPig | 0.1.16 | Apache-2.0 |
| RecursiveAI.OnePdf | 1.25.0 | MIT OR Apache-2.0 |
| WPF-UI | 4.3.0 | MIT |
| CommunityToolkit.Mvvm | 8.4.2 | MIT |
| DiffPlex | 1.9.0 | Apache-2.0 |
| BenchmarkDotNet | 0.15.8 | MIT |
| Microsoft.NET.Test.Sdk | 18.12.0 | MIT |
| xunit / xunit.runner.visualstudio | 2.9.3 / 4.0.1 | Apache-2.0 |
| QRCoder（試料生成のみ） | 1.9.0 | MIT |

日本語横書き・縦書き、透明テキスト、元JPEGのバイト一致、破損・暗号化入力のエラーを検証しました。実機ScanSnap、患者文書、QRデコードは未検証です。完全一致は成功試料12種中OnePDF 10種、PdfPig 9種でした。正解との差は試料のフォントやマッピング、改行、空白に依存します。全角空白を保持する要件とIVSを保持する要件で評価が分かれます。

- [ライブラリ・座標・画像・計測範囲の違い](docs/library-comparison.md)
- [日本語の原文差分と採用判断](docs/japanese-text-extraction.md)
- [実測値と再現条件](docs/benchmark-results.md)
- [サンプル別の実行結果](fixtures/observations.md)
