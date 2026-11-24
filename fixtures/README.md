# 合成試料

各PDFと同名のUTF-8テキストが目視・意味上の正解です。複数ページはフォームフィード（U+000C）で区切ります。差が出たときのエンジン出力は `.actual.txt`、全試料の結果は `observations.md` にあります。破損・暗号化試料の評価対象はエラー処理です。暗号化試料のテスト用パスワードは `sample-only` です。

`japanese-ocr-jpeg.pdf` はC#とWPFで架空の票面を描画し、JPEGとして埋め込み、同じ文字を描画モード3で重ねたものです。QRには合成識別子を格納しました。`scan.pdf`、`searchable.pdf`、`fictional-qr.pdf` はFlate圧縮のグレー画像を使います。`unicode-map.pdf` は可視グリフABCと抽出用のUnicodeマップを意図的に分けています。`vertical.pdf` は埋め込みType 0フォントにIdentity-Vを使用します。

WindowsのMS ゴシックをフォント埋め込み制限に従ってサブセット化します。フォントファイル単体は同梱していません。再生成手順はリポジトリ直下のREADMEにあります。暗号化の乱数、Windowsのフォント・JPEGエンコーダーによって再生成後のバイト列が変わることがあります。
