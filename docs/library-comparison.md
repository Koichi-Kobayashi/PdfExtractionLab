# ライブラリ比較

2026年11月25日にNuGet v3の安定版索引、実パッケージのnuspec、同コミットの公開ソース、実機ビルドを照合しました。OnePDFの移行記事のコード例は1.24.1向けです。本実装は1.25.0を使用します。[移行ガイド](https://recursiveai.org/onepdf/articles/migrate-from-pdfpig/)・[OnePDF 1.25.0](https://www.nuget.org/packages/RecursiveAI.OnePdf/1.25.0)・[PdfPig 0.1.16](https://www.nuget.org/packages/PdfPig/0.1.16)

| 観点 | PdfPig | OnePDF |
|---|---|---|
| 対象と依存 | .NET 10ではnet9.0資産を使用。該当TFMに外部パッケージ依存なし。複数の旧TFMにも対応 | net10.0。本体の外部パッケージ依存なし |
| ライセンス | Apache-2.0 | MIT OR Apache-2.0 |
| ページ番号 | GetPageは1始まり | Pagesは0始まり。アダプターで1始まりに統一 |
| 既定の比較 | Page.Text。コンテンツ中の文字を連結 | ExtractTextのContentOrder。テキスト生成時の区切り推定がある |
| 読み取り順モード | 既定GetWords → Docstrum（並列度1）→ UnsupervisedReadingOrderDetector。行をLF、ブロックをLF2個で接続 | ReadingOrder。構造ツリーまたはレイアウトで決定 |
| 字・単語 | Letterは1個以上のUnicode文字列になり得る。GetWordsで区切る。描画字形に対応するBoundingBox | Glyphも複数スカラーを保持可能。Wordsは解析した行内の単位。フォントメトリクスのBounds |
| 行・ブロック | Docstrumで推定 | ExtractTextのBlocks/Lines |
| 透明性 | Letter.RenderingMode。Tr=3、7を識別可能。重ね刷りや透明度の総合判定は未対応 | Glyph.RenderModeとIsVisible。単語のIsVisibleも取得 |
| 画像の探索 | GetImages。RawBytes。非コンテナ形式はTryGetPngでプレビューを試行 | InterpretのDrawImage。GetEncodedDataで原本。必要時にGetDataでサンプルを取得 |
| 観察できる診断 | ParsingOptions.Loggerの警告・エラー。同一メッセージを集約 | ページのテキスト診断と文書のDiagnostics |
| 保守 | 0.xのためAPI差をアダプター内に隔離。旧.NETでも選択可能 | .NET 10を前提に採用可能。テキスト・画像・可視性が同じライブラリ内にある |

## 座標

両エンジンのテキストAPIはCropBoxのオフセットとRotateを適用済みの表示ページでY軸が上向きです。PdfPigは四隅の最小・最大を取り、OnePDFはBoundsを利用します。RawはこのAPI座標の軸平行外接矩形です。元のコンテンツ演算子のCTMや傾斜した四辺形はUIに再現しません。共通座標はXが左端、Yが表示ページ高さから矩形上端を引いた値、単位はptです。ページのCropBox・MediaBoxは各APIの報告値を別に保持します。PdfPigの公開CropBoxは有効な表示領域へクリップ済みの場合があります。

PdfPigの初期行列はUserUnitを拡大しないため、アダプターが矩形とページ寸法を倍率補正します。OnePDFの画像デバイスは最初からY軸下向きです。画像単位正方形の4頂点をCTMで変換した外接矩形を使います。テキストの式を画像に再適用しません。[PdfPig初期行列](https://github.com/UglyToad/PdfPig/blob/a7bb35662bbbf405efddad50aedc9bcdcf515afc/src/UglyToad.PdfPig/Graphics/OperationContextHelper.cs)・[OnePDFのグリフ空間](https://github.com/recursive-ai-oss/onepdf-csharp/blob/dbc166816ebae57ed6e130d87d4453b2a7aef67c/src/OnePdf/Text/Extraction/PdfTextGlyph.cs)・[画像のデバイス座標](https://github.com/recursive-ai-oss/onepdf-csharp/blob/dbc166816ebae57ed6e130d87d4453b2a7aef67c/src/OnePdf/Rendering/PdfDeviceTypes.cs)

## 埋め込み画像

ページに描画された配置ごとに数えます。同じXObjectを複数回描けば件数に含まれます。未使用のリソースの棚卸し、ページ全体の描画、QRの読み取りは行いません。原本はPDFのフィルターを適用する前のストリームです。文書暗号がある場合、ライブラリによる復号後の値を指します。

JPEGは両アダプターとも原本を保持し、表示時にWPFがデコードします。Flate試料では元ストリームを別に保持し、PdfPigはPNG、OnePDFはデコード後の8-bit Gray/RGBからBMPを生成します。このプレビュー用バイト列の生成は画像抽出の計測に含み、WPFのビットマップ表示や保存は含みません。アルファ、CMYK、ICC、マスク付きJPEGなどで表示用の合成を網羅していません。原本JPEGのプレビューでは別マスクやPDFのDecode配列が反映されない場合があります。プレビューできない形式も原本を保存できます。

今回のJPEG・Flate試料はOnePDF本体のAPIで処理できました。追加候補のRecursiveAI.OneImage.Pdf 1.11.2はMIT OR Apache-2.0で、OneImage 1.11.2、OnePdfおよびOnePdf.Fonts 1.18.0以上に依存します。ページ描画や追加コーデックの必要が生じた時点で評価します。今回の測定には含みません。PdfPigも追加フィルターパッケージを導入していません。[OnePDF画像API](https://github.com/recursive-ai-oss/onepdf-csharp/blob/dbc166816ebae57ed6e130d87d4453b2a7aef67c/src/OnePdf/Images/PdfImage.cs)・[追加パッケージ](https://www.nuget.org/packages/RecursiveAI.OneImage.Pdf/1.11.2)・[PdfPig画像ガイド](https://github.com/UglyToad/PdfPig/wiki/Images)

## 比較条件と制限

ページ数、UTF-16コード単位数、Unicodeスカラー列挙数、エンジン別単語数、空でないページの論理行数を区別します。行数は原文改行による集計で、レイアウト推定の行数は座標タブに現れます。原文・座標・画像は文書を破棄する前に実体化します。画面には選択ページを出し、全ページの結果と画像をメモリに保持するため、画像量に応じてピーク使用量が増えます。巨大な単一ページではキャンセル待ちやCanvas描画負荷があり得ます。PdfPigの同期API呼び出し中は中断できません。

ライブラリの機能、フォント、対象TFM、読み取り順の定義が異なります。OfficeIMOの公開測定やOnePDF著者のコーパスと今回の合成試料は入力と作業内容が異なるため、数値を混在させていません。[参考ベンチマーク](https://officeimo.com/benchmarks/pdf/)
