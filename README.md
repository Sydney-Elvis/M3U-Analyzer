# M3U Analyzer

**M3U Analyzer is a read-only IPTV catalog intelligence tool.**

It helps you explore, understand, and compare IPTV providers by analyzing their playlists, content structure, and guide data.

---

## 🎯 The Problem

Most IPTV tools focus on:
- playing streams  
- managing playlists  
- filtering or proxying channels  

But none clearly answer:

> **What does this provider actually contain?**  
> **How is it different from another provider?**

Playlists are large (10,000–50,000+ channels), inconsistent, and difficult to understand:
- group names are unstructured  
- channel names are inconsistent  
- duplicates and regional variants are common  
- EPG quality varies widely  

Existing tools help you *use* IPTV.  
M3U Analyzer helps you *understand* it.

---

## 🧠 The Goal

M3U Analyzer is intentionally focused and read-only.

It provides:

### 1. Provider Visualization

Understand a single provider’s content:
- Live TV groups and channels
- Movies (VOD)
- Series
- Group/category structure
- Channel distribution and density

Quickly answer:
- What content is actually here?
- How is it organized?
- Is this provider clean or noisy?

---

### 2. Search & Discovery

Find content across large catalogs:
- Search by channel name or keyword
- Filter by group/category
- Identify if a provider contains specific content
- Explore variations and duplicates

Because:
> Finding a channel in 20,000 entries shouldn’t be guesswork.

---

### 3. Provider Comparison

Compare two providers side-by-side:
- Channels present in both providers
- Channels unique to each provider
- Likely equivalent channels (even with inconsistent naming)
- Group/category differences
- Content coverage differences

This answers:
- What am I missing if I switch providers?
- Which provider is stronger for specific content?

---

### 4. EPG Analysis

Evaluate guide quality across providers:
- Guide duration (24 hours vs multiple days/weeks)
- Coverage completeness
- Missing or sparse guide data
- Alignment between channels and EPG entries

Because:
> A provider without a usable guide is often unusable in practice.

---

## 🚫 What This Project Is NOT

M3U Analyzer is **not**:
- a stream proxy  
- a playlist editor  
- a media player  
- a DVR backend  
- a provider management system  

It does **not**:
- stream content  
- rebroadcast streams  
- modify playlists  

It is strictly:
> **a read-only analysis and comparison tool**

---

## 🧩 Why This Exists

Current tools focus on:
- playlist management  
- stream delivery  
- playback  

Some tools analyze playlists, but only for:
- stream health  
- broken links  
- technical validation :contentReference[oaicite:0]{index=0}  

Others provide utilities like:
- playlist creation  
- EPG generation  
- API-based management :contentReference[oaicite:1]{index=1}  

But none provide:
- provider-to-provider comparison  
- catalog-level insights  
- meaningful summaries of content differences  

M3U Analyzer fills that gap.

---

## 🏗️ Core Principles

- **Read-only first** — no risk, no side effects  
- **Clarity over automation** — explain what exists, don’t guess silently  
- **Deterministic results** — avoid “magic” unless clearly labeled  
- **Handle real-world data** — messy names, duplicates, inconsistent metadata  
- **Focus on insight, not plumbing**

---

## 🚀 Project Scope (Initial)

The first version focuses on:

- Importing M3U playlists (URL or file)
- Parsing and normalizing channel/group data
- Visualizing provider structure
- Searching and filtering large catalogs
- Comparing two providers
- Basic EPG analysis

Future enhancements may include:
- improved matching heuristics
- better duplicate detection
- optional AI-assisted search and summaries

---

## 🔍 Example Questions M3U Analyzer Helps Answer

- Does this provider actually have the channels I care about?
- Which provider has better sports coverage?
- How much overlap exists between providers?
- Which provider has a better EPG?
- How noisy or duplicated is this catalog?

---

## 🧠 Relationship to M3Undle

M3U Analyzer is a **companion project** to M3Undle.

- **M3Undle** → manage, normalize, and publish lineups  
- **M3U Analyzer** → explore and compare provider catalogs  

Shared components (planned):
- playlist parsing
- normalization logic
- matching and comparison engine

---

## ⚠️ Disclaimer

M3U Analyzer does not provide or distribute any IPTV content.

Users are responsible for their own data sources and compliance with applicable laws.

---

## 📌 Status

Early development.