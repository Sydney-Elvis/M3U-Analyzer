# Test fixtures

Rules: hosts are `example.invalid`; credentials are fake (`alice` / `s3cret`). Never add real provider data.
Do not edit a fixture without updating every expectation below and every test that uses it.
Reference "now" for EPG tests: `2026-10-15T10:00:00Z`.

## m3u/defects-small.m3u

20 entries (0-based index), 1 preamble line, 19 URLs.

| # | Title | Group | Expected type / basis | Defect being exercised |
| --- | --- | --- | --- | --- |
| 0 | US: CNN HD | News | Live / UrlInferred | baseline; tvg-id cnn.us |
| 1 | UK: BBC News | News | Live / UrlInferred | baseline |
| 2 | US: CNN HD | News | Live / UrlInferred | exact duplicate of #0 (same StreamKey) |
| 3 | No URL Channel | News | Unknown / Defaulted | entry without URL |
| 4 | ESPN | Sports | Live / UrlInferred | comma inside a quoted attribute |
| 5 | Orphan Channel | (none) | Live / UrlInferred | no group |
| 6 | Sky Sports Main Event FHD | Sports (from #EXTGRP) | Live / UrlInferred | group only via #EXTGRP |
| 7 | (empty) | News | Live / UrlInferred | empty title; tvg-name "Empty Title Fallback" |
| 8 | (empty) | News | Live / UrlInferred | empty title, no tvg-name |
| 9 | The Matrix (1999) | Movies \| Action | Vod / UrlInferred | — |
| 10 | Good, the Bad and the Ugly, The (1966) | Movies \| Drama | Vod / UrlInferred | commas in title |
| 11 | Breaking Point S01 E01 | Series \| Drama | Series / UrlInferred | — |
| 12 | Breaking Point S01 E02 | Series \| Drama | Series / UrlInferred | blank line before entry |
| 13 | Local Access | Local | Live / Defaulted | no type evidence in URL |
| 14 | HBO East | Movies Channels | Live / Defaulted | Xtream short URL without extension |
| 15 | PBS HD | News | Live / UrlInferred | signed `token=` query |
| 16 | TSN 1 CA | Sports | Live / UrlInferred | extra attributes (tvg-country, tvg-language, tvg-chno, catchup, catchup-days) |
| 17 | NFL 01: Bears vs Packers 8:20 PM ET | PPV EVENTS | Live / UrlInferred | event channel (Core EventChannelClassifier: IsEvent = true) |
| 18 | Upper Case Attrs | News | Live / UrlInferred | upper-case attribute names |
| 19 | Speed (1994) | Movies \| Action | Vod / UrlInferred | type from `?type=vod` |

Totals: Live 14 (Defaulted 2), Vod 3, Series 2, Unknown 1.
Distinct (content type, group) pairs: 9 — Live: News, Sports, Local, Movies Channels, PPV EVENTS; Vod: Movies | Action, Movies | Drama; Series: Series | Drama; Unknown: News.
Entries without a group: 1 (#5). Empty titles: 2 (#7, #8). Distinct stream keys: 18 (19 URLs, #0 and #2 identical).
Live entries whose tvg-id exists in guide-small.xml: 8 (#0, #1, #2, #4, #6, #14, #15, #16).

## xmltv/guide-small.xml

Raw `<channel>` elements 9 → parsed 8 (one has no id). Raw `<programme>` elements 28 → parsed 26 (Core drops the empty title on sky.sports and the bad timestamp on tsn1.ca).

| Channel | Parsed programmes | First start (UTC) | Last stop (UTC) | Feature |
| --- | --- | --- | --- | --- |
| cnn.us | 12 | 2026-10-15 06:00 | 2026-10-16 06:00 | continuous 24 h; two display names (first wins: "CNN") |
| bbcnews.uk | 5 | 2026-10-15 10:00 | 2026-10-15 22:00 | gap 14:00–16:00 |
| espn.us | 3 | 2026-10-15 10:00 | 2026-10-15 15:00 | -0500 offsets; overlap 11:00–12:00 |
| sky.sports | 1 (of 2) | 2026-10-15 11:00 | 2026-10-15 12:00 | empty-title programme dropped |
| tsn1.ca | 1 (of 2) | 2026-10-15 10:00 | 2026-10-15 12:00 | bad-timestamp programme dropped |
| pbs.us | 2 | 2026-10-14 08:00 | 2026-10-14 12:00 | stale (ended before "now") |
| hbo.east | 0 | — | — | channel without programmes |
| nomatch.channel | 1 | 2026-10-15 10:00 | 2026-10-15 12:00 | no playlist entry |
| ghost.channel (undeclared) | 1 | 2026-10-15 10:00 | 2026-10-15 11:00 | programme for an undeclared channel |

## xmltv/guide-truncated.xml

Ends mid-element. Raw counts before the XML error: 3 channels, 3 programmes. Core parses 3 channels and 2 programmes.

## xtream/*.json

Fake Xtream panel responses (credentials alice/s3cret appear only in account_info.json and one icon path, to prove redaction).
Live: 5 elements → 4 valid streams (one has no stream_id), category 3 "Empty Category" has no items, stream 103 has an empty name and unknown category 99, stream 102 has category_ids [2,1].
VOD: 2 items. Series: 1 item (301) with 2 episodes in series_info_301.json.
